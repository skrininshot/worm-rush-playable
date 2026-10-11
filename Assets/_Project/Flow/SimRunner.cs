using UnityEngine;
using WormRush.Config;
using WormRush.Core;
using WormRush.View;

namespace WormRush.Flow
{
    /// <summary>
    /// Stage 2 driver: owns the sim, feeds it input, draws it. The PlayableFlow state machine
    /// (intro, tutorial, climax, end card) replaces the run/restart logic here in stage 4.
    /// </summary>
    public sealed class SimRunner : MonoBehaviour
    {
        PlayableConfig _config;
        WormSim _sim;
        WorldView _world;
        CameraFollow _camera;
        PointerInput _input;
        float _deathTime;
        int _runCount;

        public WormSim Sim
        {
            get { return _sim; }
        }

        public PlayableConfig Config
        {
            get { return _config; }
        }

        public int RunCount
        {
            get { return _runCount; }
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

            _camera = new CameraFollow(FindOrCreateCamera(), _config);
            _input = new PointerInput();
            NewRun();
        }

        void OnDestroy()
        {
            if (_world != null) _world.Dispose();
        }

        void Update()
        {
            _input.Update();

            if (!_sim.Player.Alive)
            {
                bool wait = _sim.Time - _deathTime < _config.RestartDelaySeconds;
                if (!wait && (_input.PressedThisFrame || _input.RestartKeyPressed)) NewRun();
            }

            HandleSteering();
            _sim.Step(Time.deltaTime);

            if (!_sim.Player.Alive && _deathTime < 0f) _deathTime = _sim.Time;

            ViewBounds view = _camera.Apply(_sim.Player);
            _world.Draw(_sim, view);
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

        void NewRun()
        {
            int seed = _config.Seed != 0 ? _config.Seed : System.Environment.TickCount;
            _sim = new WormSim(_config.Sim, seed);
            _deathTime = -1f;
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
