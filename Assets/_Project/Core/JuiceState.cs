using System.Collections.Generic;

namespace WormRush.Core
{
    public enum SoundId : byte
    {
        /// <summary>The player ate; Step is the combo step (1 = first pellet of a streak).</summary>
        Eat,
        Kill,
        Death,
        EndCard,

        /// <summary>A press on a button: the rising three-note tap.</summary>
        UiTap,

        /// <summary>The end card button springing in.</summary>
        UiPop,
    }

    public readonly struct SoundRequest
    {
        public readonly SoundId Id;
        public readonly int Step;

        public SoundRequest(SoundId id, int step = 0)
        {
            Id = id;
            Step = step;
        }
    }

    /// <summary>
    /// The decisions behind the juice, with no drawing and no audio: which sim events cause which shake, burst,
    /// popup and sound, plus the pools those live in. The View reads <see cref="Particles"/>, <see cref="Popups"/>,
    /// the shake offset and <see cref="Sounds"/> after <see cref="Update"/>.
    ///
    /// Rules, as in the reference: every death bursts into particles; once the end card is up, nothing else
    /// reacts; the player's kill gives shake, "+1 NICE!" and a two-note blip; the player's death gives a
    /// bigger shake and a low buzz; eating gives a rising blip per combo step and "+N" for bigger pellets.
    /// </summary>
    public sealed class JuiceState
    {
        readonly JuiceConfig _config;
        readonly SimRandom _rng;
        readonly ComboMeter _combo;
        readonly ScreenShake _shake;

        float _lastEatSoundAt = float.NegativeInfinity;
        bool _endCardShown;

        public readonly ParticlePool Particles;
        public readonly PopupPool Popups;

        /// <summary>Sounds to start this frame. Cleared by each Update.</summary>
        public readonly List<SoundRequest> Sounds = new List<SoundRequest>(8);

        public JuiceState(JuiceConfig config, int seed)
        {
            _config = config;
            _rng = new SimRandom(seed);
            _combo = new ComboMeter(config.ComboWindowSeconds);
            _shake = new ScreenShake(config.ShakeAmplitude);
            Particles = new ParticlePool(config);
            Popups = new PopupPool(config);
        }

        /// <summary>Camera shake this frame in UI units (390 x 844 reference px); (0, 0) when idle.</summary>
        public float ShakeX { get; private set; }
        public float ShakeY { get; private set; }

        public float ShakeRemaining
        {
            get { return _shake.Remaining; }
        }

        public int ComboStreak
        {
            get { return _combo.Streak; }
        }

        /// <summary>Call once per frame after WormSim.Step and PlayableFlow.Update, with the same dt the sim was stepped by.</summary>
        public void Update(WormSim sim, PlayableFlow flow, float dt)
        {
            Sounds.Clear();
            int playerId = sim.Player.Id;

            for (int i = 0; i < sim.Events.Count; i++)
            {
                SimEvent e = sim.Events[i];
                switch (e.Type)
                {
                    case SimEventType.Ate:
                        if (e.WormId == playerId && !_endCardShown) OnPlayerAte(sim, e);
                        break;
                    case SimEventType.Killed:
                        OnKilled(sim, e, playerId);
                        break;
                }
            }

            Particles.Step(dt);
            Popups.Step(dt);
            _shake.Step(dt);
            _shake.Sample(_rng, out float x, out float y);
            ShakeX = x;
            ShakeY = y;

            bool endCard = flow.State == FlowState.EndCard;
            if (endCard && !_endCardShown) Sounds.Add(new SoundRequest(SoundId.EndCard));
            _endCardShown = endCard;
        }

        void OnPlayerAte(WormSim sim, SimEvent e)
        {
            int streak = _combo.Register(sim.Time);

            if (sim.Time - _lastEatSoundAt >= _config.EatMinIntervalSeconds)
            {
                _lastEatSoundAt = sim.Time;
                int step = streak < _config.ComboMaxSteps ? streak : _config.ComboMaxSteps;
                Sounds.Add(new SoundRequest(SoundId.Eat, step));
            }

            if (e.Value >= _config.PopupMinFoodValue)
                Popups.Spawn(e.X, e.Y, PopupKind.Value, e.Value, _config.PopupValueScale);
        }

        void OnKilled(WormSim sim, SimEvent e, int playerId)
        {
            Particles.Burst(e.X, e.Y, sim.Worms[e.WormId].ColorId, _rng);
            if (_endCardShown) return;

            if (e.WormId == playerId)
            {
                _shake.Trigger(_config.ShakeDeathSeconds);
                Sounds.Add(new SoundRequest(SoundId.Death));
            }
            else if (e.OtherId == playerId)
            {
                _shake.Trigger(_config.ShakeKillSeconds);
                Popups.Spawn(e.X, e.Y + _config.PopupSpawnLift, PopupKind.Kill, 0, _config.PopupKillScale);
                Sounds.Add(new SoundRequest(SoundId.Kill));
            }
        }
    }
}
