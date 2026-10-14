using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WormRush.Config;
using WormRush.Core;
using WormRush.View;

namespace WormRush.EditorTools
{
    /// <summary>
    /// Headless render check: builds a sim, runs it for a while, draws it through the real WorldView and
    /// saves a PNG. Needs a GPU, so run WITHOUT -nographics:
    ///   Unity -batchmode -quit -projectPath . -executeMethod WormRush.EditorTools.ScreenshotCapture.Run
    ///         -shotPath out.png -shotWidth 540 -shotHeight 960 -shotSeconds 6 -shotSeed 7
    ///         [-shotHeading 0.6] [-shotViewSpan 480] [-shotState play|tutorial|end|kill] [-shotAfter 0.25] [-shotPopups 1] [-shotSteer 1] [-shotUiSeconds 1.25] [-shotPressAt 2.0] -logFile log.txt
    /// -shotState: play = tapped, run -shotSeconds, then draw (HUD, "Block their path!" window);
    ///             tutorial = never tapped (the hand); end = run until the end card shows, then draw it;
    ///             kill = run until the player's first kill, then -shotAfter more seconds (burst, "+1 NICE!", shake).
    /// </summary>
    public static class ScreenshotCapture
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const float StepSeconds = 1f / 60f;
        const float EndSearchSeconds = 60f;   // -shotState end gives up after this much sim time
        const float UiSettleSeconds = 1.25f;  // default UI time after the end card appears: title landed, button mid sweep
        const float KillSearchSeconds = 60f;  // -shotState kill gives up after this much sim time

        public static void Run()
        {
            string path = Arg("-shotPath", "screenshot.png");
            int width = int.Parse(Arg("-shotWidth", "540"));
            int height = int.Parse(Arg("-shotHeight", "960"));
            float seconds = float.Parse(Arg("-shotSeconds", "6"), System.Globalization.CultureInfo.InvariantCulture);
            int seed = int.Parse(Arg("-shotSeed", "7"));
            float heading = float.Parse(Arg("-shotHeading", "0.6"), System.Globalization.CultureInfo.InvariantCulture);
            string state = Arg("-shotState", "play");
            float after = float.Parse(Arg("-shotAfter", "0.25"), System.Globalization.CultureInfo.InvariantCulture);
            float uiSeconds = float.Parse(Arg("-shotUiSeconds", UiSettleSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                System.Globalization.CultureInfo.InvariantCulture);
            float pressAt = float.Parse(Arg("-shotPressAt", "-1"), System.Globalization.CultureInfo.InvariantCulture);

            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No Main Camera in " + ScenePath);

            // A copy, so overriding values for the shot never touches the asset.
            PlayableConfig config = UnityEngine.Object.Instantiate(PlayableConfig.Load());
            config.ViewSpan = float.Parse(Arg("-shotViewSpan", config.ViewSpan.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                System.Globalization.CultureInfo.InvariantCulture);
            WorldView world = WorldView.Create(config);
            if (world == null) throw new InvalidOperationException("WorldView could not be created (shader missing?)");

            WormSim sim = new WormSim(config.Sim, seed);
            PlayableFlow flow = new PlayableFlow(sim);
            if (state != "tutorial")
            {
                sim.StartPlay();
                sim.SetPlayerHeading(heading);
            }

            JuiceState juice = new JuiceState(config.Juice, seed);
            float limit = state == "end" ? EndSearchSeconds : (state == "kill" ? KillSearchSeconds : seconds);
            float killedAt = -1f;
            bool steer = Arg("-shotSteer", "0") == "1";
            for (float t = 0f; t < limit; t += StepSeconds)
            {
                if (steer) SteerAtVictim(sim);
                sim.Step(StepSeconds);
                flow.Update();
                juice.Update(sim, flow, StepSeconds);
                if (state == "end" && flow.State == FlowState.EndCard) break;
                if (state == "kill")
                {
                    if (killedAt < 0f && flow.Kills > 0) killedAt = t;
                    if (killedAt >= 0f && t >= killedAt + after) break;
                }
            }

            // -shotPopups 1: a kill popup and a value popup beside the player, to look at their outline and text.
            if (Arg("-shotPopups", "0") == "1")
            {
                Vec2 head = sim.Player.Head;
                juice.Popups.Spawn(head.X + 30f, head.Y + config.Juice.PopupSpawnLift, PopupKind.Kill, 0, config.Juice.PopupKillScale);
                juice.Popups.Spawn(head.X - 30f, head.Y - 20f, PopupKind.Value, 3, config.Juice.PopupValueScale);
            }

            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            CameraFollow follow = new CameraFollow(camera, config);
            float shakeScale = PlayableUi.ScaleFor(width, height);
            ViewBounds view = follow.Apply(sim.Player, juice.ShakeX * shakeScale, juice.ShakeY * shakeScale);
            world.Draw(sim, view, juice);

            // The UI the same way the runner drives it: its animations are stepped in real frames for -shotUiSeconds
            // (the end card starts at the first Present), so springs, confetti and the sweep are where they would be.
            PlayableUi ui = PlayableUi.Create(config, camera);
            ui.Present(flow, sim, 0f, width, height);
            bool pressed = false;
            for (float t = StepSeconds; t <= uiSeconds + 1e-4f; t += StepSeconds)
            {
                if (!pressed && pressAt >= 0f && t >= pressAt)
                {
                    pressed = true;
                    ui.Press(new Vector2(width * 0.5f, height * 0.4f), width, height);
                }
                ui.Present(flow, sim, t, width, height);
            }
            ui.PresentPopups(juice, width, height);
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = target;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("Screenshot saved: " + path + "  (" + width + "x" + height + ", food " + sim.FoodCount + ", player length " + sim.Player.Length +
                      ", flow " + flow.State + ", end " + flow.EndKind + "/" + flow.EndReason + ", kills " + flow.Kills +
                      ", particles " + juice.Particles.Count + ", popups " + juice.Popups.Count + ", shake " + juice.ShakeRemaining.ToString("F2") + ")");
            ui.Dispose();
            world.Dispose();
        }

        /// <summary>-shotSteer 1: the player heads for the victim's head, so its body lies across the victim's path (for -shotState kill).</summary>
        static void SteerAtVictim(WormSim sim)
        {
            for (int i = 0; i < sim.Worms.Count; i++)
            {
                Worm worm = sim.Worms[i];
                if (!worm.IsVictim || !worm.Alive) continue;
                Vec2 from = sim.Player.Head;
                sim.SetPlayerHeading(MathF.Atan2(worm.Head.Y - from.Y, worm.Head.X - from.X));
                return;
            }
        }

        /// <summary>
        /// CPU cost of one frame of game code (sim step + camera + instance submission), averaged over
        /// -benchFrames frames. Measured in the editor (Mono), so it is a pessimistic bound for the Web build.
        /// Unity -batchmode -nographics -quit -projectPath . -executeMethod WormRush.EditorTools.ScreenshotCapture.Benchmark
        /// </summary>
        public static void Benchmark()
        {
            int frames = int.Parse(Arg("-benchFrames", "1200"));
            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = Camera.main;
            PlayableConfig config = PlayableConfig.Load();
            WorldView world = WorldView.Create(config);
            CameraFollow follow = new CameraFollow(camera, config);
            WormSim sim = new WormSim(config.Sim, 7);
            sim.StartPlay();

            // Warm up (JIT, buffer growth), then measure.
            for (int i = 0; i < 120; i++) FrameWork(sim, follow, world, i);

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            double worst = 0.0;
            double total = 0.0;
            for (int i = 0; i < frames; i++)
            {
                watch.Restart();
                FrameWork(sim, follow, world, i);
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > worst) worst = ms;
            }

            Debug.Log("Benchmark: " + frames + " frames, avg " + (total / frames).ToString("F3") + " ms, worst " + worst.ToString("F3") +
                      " ms, food " + sim.FoodCount + ", worms alive " + CountAlive(sim));
            world.Dispose();
        }

        static void FrameWork(WormSim sim, CameraFollow follow, WorldView world, int frame)
        {
            // Sweep the heading so the worm keeps moving through the arena instead of sitting on the edge.
            sim.SetPlayerHeading(frame * 0.02f);
            sim.Step(StepSeconds);
            world.Draw(sim, follow.Apply(sim.Player));
        }

        static int CountAlive(WormSim sim)
        {
            int alive = 0;
            for (int i = 0; i < sim.Worms.Count; i++)
                if (sim.Worms[i].Alive) alive++;
            return alive;
        }

        static string Arg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }
    }
}
