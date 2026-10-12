using UnityEngine;
using WormRush.Ad;
using WormRush.Config;
using WormRush.Core;
using WormRush.View;

namespace WormRush.Flow
{
    /// <summary>
    /// The playable's driver: owns the sim, the PlayableFlow beat sheet, the world and UI views and the ad network.
    /// Each frame: input, sim step, flow update, draw. What a tap means depends on where it lands and on the flow:
    /// the CTA button or anywhere on the end card goes to the store, everywhere else it steers (and the very
    /// first one starts the run).
    /// </summary>
    public sealed class PlayableRunner : MonoBehaviour
    {
        PlayableConfig _config;
        WormSim _sim;
        PlayableFlow _flow;
        WorldView _world;
        CameraFollow _camera;
        PointerInput _input;
        PlayableUi _ui;
        IAdNetwork _ad;
        bool _endReported;
        int _runCount;
        int _storeOpens;

        public WormSim Sim
        {
            get { return _sim; }
        }

        public PlayableFlow Flow
        {
            get { return _flow; }
        }

        public PlayableConfig Config
        {
            get { return _config; }
        }

        public int RunCount
        {
            get { return _runCount; }
        }

        /// <summary>How many times the player asked for the store this session.</summary>
        public int StoreOpens
        {
            get { return _storeOpens; }
        }

        void Awake()
        {
            _config = PlayableConfig.Load();
            Application.targetFrameRate = _config.TargetFrameRate;

            _world = WorldView.Create(_config);
            if (_world == null)
            {
                enabled = false;
                return;
            }

            Camera camera = FindOrCreateCamera();
            _camera = new CameraFollow(camera, _config);
            _input = new PointerInput();
            _ui = PlayableUi.Create(_config, camera);
            _ad = AdNetworks.Create(_config);
            NewRun();
            _ad.GameReady();
        }

        void OnDestroy()
        {
            if (_world != null) _world.Dispose();
            if (_ui != null) _ui.Dispose();
        }

        void Update()
        {
            _input.Update();

            bool ended = _flow.State == FlowState.EndCard;
            bool overCta = !ended && _ui.IsOverCta(_input.Position);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Not part of the ad: a quick way to run the beat sheet again while testing.
            if (_input.RestartKeyPressed) NewRun();
            ended = _flow.State == FlowState.EndCard;
#endif

            if (_input.PressedThisFrame && (ended || overCta)) OpenStore();
            else if (!ended && !overCta) HandleSteering();

            _sim.Step(Time.deltaTime);
            _flow.Update();

            if (_flow.State == FlowState.EndCard && !_endReported)
            {
                _endReported = true;
                _ad.GameEnd();
            }

            ViewBounds view = _camera.Apply(_sim.Player);
            _world.Draw(_sim, view);
            _ui.Present(_flow, _sim, Time.time, Screen.width, Screen.height);
        }

        void HandleSteering()
        {
            if (!_sim.Player.Alive) return;
            if (_input.PressedThisFrame) _sim.StartPlay();
            if (!_sim.Started || !(_input.PressedThisFrame || _input.MovedThisFrame)) return;

            float dx = _input.Position.x - Screen.width * 0.5f;
            float dy = _input.Position.y - Screen.height * 0.5f;
            if (Steering.TryGetHeading(dx, dy, _config.PointerDeadZonePixels, out float angle))
                _sim.SetPlayerHeading(angle);
        }

        void OpenStore()
        {
            _storeOpens++;
            _ad.OpenStore();
        }

        void NewRun()
        {
            int seed = _config.Seed != 0 ? _config.Seed : System.Environment.TickCount;
            _sim = new WormSim(_config.Sim, seed);
            _flow = new PlayableFlow(_sim);
            _endReported = false;
            _runCount++;
        }

        static Camera FindOrCreateCamera()
        {
            Camera camera = Camera.main;
            if (camera != null) return camera;

            GameObject go = new GameObject("Main Camera") { tag = "MainCamera" };
            return go.AddComponent<Camera>();
        }
    }
}
