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
    ///         [-shotHeading 0.6] [-shotViewSpan 480] -logFile log.txt
    /// </summary>
    public static class ScreenshotCapture
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const float StepSeconds = 1f / 60f;

        public static void Run()
        {
            string path = Arg("-shotPath", "screenshot.png");
            int width = int.Parse(Arg("-shotWidth", "540"));
            int height = int.Parse(Arg("-shotHeight", "960"));
            float seconds = float.Parse(Arg("-shotSeconds", "6"), System.Globalization.CultureInfo.InvariantCulture);
            int seed = int.Parse(Arg("-shotSeed", "7"));
            float heading = float.Parse(Arg("-shotHeading", "0.6"), System.Globalization.CultureInfo.InvariantCulture);

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
            sim.StartPlay();
            sim.SetPlayerHeading(heading);
            for (float t = 0f; t < seconds; t += StepSeconds) sim.Step(StepSeconds);

            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            CameraFollow follow = new CameraFollow(camera, config);
            ViewBounds view = follow.Apply(sim.Player);
            world.Draw(sim, view);
            camera.Render();

            RenderTexture.active = target;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("Screenshot saved: " + path + "  (" + width + "x" + height + ", food " + sim.FoodCount + ", player length " + sim.Player.Length + ")");
            world.Dispose();
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
