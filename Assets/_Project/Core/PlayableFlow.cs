namespace WormRush.Core
{
    public enum FlowState : byte
    {
        /// <summary>First instants: the worm circles, the tutorial hint fades in. A tap skips straight to Play.</summary>
        Intro,

        /// <summary>Hint fully visible, waiting for the first tap.</summary>
        Tutorial,

        /// <summary>After the first tap: eat and grow.</summary>
        Play,

        /// <summary>The victim bot is cutting across the player's body.</summary>
        Climax,

        /// <summary>Over. The sim may keep running underneath, but the ad is done and a tap goes to the store.</summary>
        EndCard,
    }

    /// <summary>Which end card to show. Decided at the moment the card appears.</summary>
    public enum EndKind : byte
    {
        None,

        /// <summary>Survived and killed someone.</summary>
        Awesome,

        /// <summary>Survived to the time limit without a kill.</summary>
        NiceMoves,

        /// <summary>The player died.</summary>
        SoClose,
    }

    public enum EndReason : byte
    {
        None,

        /// <summary>EndAfterKillSeconds after the player's first kill.</summary>
        Kill,

        /// <summary>MaxPlaySeconds after the first tap.</summary>
        TimeUp,

        /// <summary>EndAfterDeathSeconds after the player died.</summary>
        PlayerDied,
    }

    /// <summary>
    /// The creative's beat sheet as a state machine (Intro, Tutorial, Play, Climax, EndCard). Pure data: it reads the
    /// sim after each Step and says what the screen should be showing; the Flow/View layer draws it and decides
    /// what a tap does. Runs on sim time, so it is deterministic and unit-tested.
    ///
    /// End conditions, as in the reference: a kill ends it EndAfterKillSeconds later, the time limit ends it at
    /// once, a death ends it EndAfterDeathSeconds later and overrides a pending kill (the card then says SO CLOSE).
    /// Before the first tap nothing ends it: an idle ad stays on the tutorial.
    /// </summary>
    public sealed class PlayableFlow
    {
        readonly WormSim _sim;
        readonly SimConfig _config;

        float _endAt = -1f;
        EndReason _pendingReason;
        bool _playerDied;
        float _stateSince;

        public PlayableFlow(WormSim sim)
        {
            _sim = sim;
            _config = sim.Config;
        }

        public FlowState State { get; private set; }
        public EndKind EndKind { get; private set; }
        public EndReason EndReason { get; private set; }

        /// <summary>Kills made by the player so far.</summary>
        public int Kills
        {
            get { return _sim.PlayerKills; }
        }

        /// <summary>Seconds since the first tap, 0 before it.</summary>
        public float PlayTime
        {
            get { return _sim.Started ? _sim.Time - _sim.StartTime : 0f; }
        }

        /// <summary>Seconds spent in the current state.</summary>
        public float StateTime
        {
            get { return _sim.Time - _stateSince; }
        }

        /// <summary>The hand and "DRAG TO MOVE": visible until the first tap.</summary>
        public bool TutorialVisible
        {
            get { return State == FlowState.Intro || State == FlowState.Tutorial; }
        }

        /// <summary>0..1: the hint fades in during Intro and stays at 1 through Tutorial.</summary>
        public float TutorialAlpha
        {
            get
            {
                if (State == FlowState.Tutorial) return 1f;
                if (State != FlowState.Intro) return 0f;
                if (_config.IntroSeconds <= 0f) return 1f;
                float a = StateTime / _config.IntroSeconds;
                return a < 0f ? 0f : (a > 1f ? 1f : a);
            }
        }

        /// <summary>"Block their path!": a window after the first tap, until the first kill.</summary>
        public bool BlockHintVisible
        {
            get
            {
                if (State != FlowState.Play && State != FlowState.Climax) return false;
                if (_sim.PlayerKills > 0 || !_sim.Player.Alive) return false;
                float t = PlayTime;
                return t >= _config.BlockHintStartSeconds && t < _config.BlockHintEndSeconds;
            }
        }

        /// <summary>The CTA button at the bottom: there from the first frame, gone once the end card owns the screen.</summary>
        public bool CtaVisible
        {
            get { return State != FlowState.EndCard; }
        }

        /// <summary>Call once per frame, after WormSim.Step.</summary>
        public void Update()
        {
            if (State == FlowState.EndCard) return;

            ReadEvents();

            if (_endAt < 0f && _sim.Started && PlayTime > _config.MaxPlaySeconds)
                Schedule(_sim.Time, EndReason.TimeUp);

            if (_endAt >= 0f && _sim.Time >= _endAt)
            {
                Enter(FlowState.EndCard);
                EndReason = _pendingReason;
                EndKind = _playerDied ? EndKind.SoClose : (_sim.PlayerKills > 0 ? EndKind.Awesome : EndKind.NiceMoves);
                return;
            }

            if (!_sim.Started)
            {
                if (State == FlowState.Intro && StateTime >= _config.IntroSeconds) Enter(FlowState.Tutorial);
            }
            else if (State == FlowState.Intro || State == FlowState.Tutorial)
            {
                Enter(FlowState.Play);
            }

            if (State == FlowState.Play && VictimScriptActive()) Enter(FlowState.Climax);
        }

        void ReadEvents()
        {
            int playerId = _sim.Player.Id;
            for (int i = 0; i < _sim.Events.Count; i++)
            {
                SimEvent e = _sim.Events[i];
                if (e.Type != SimEventType.Killed) continue;

                if (e.WormId == playerId)
                {
                    // The player died: the end moves up to EndAfterDeathSeconds from now, even past a pending kill.
                    _playerDied = true;
                    Schedule(_sim.Time + _config.EndAfterDeathSeconds, EndReason.PlayerDied);
                }
                else if (e.OtherId == playerId && _endAt < 0f)
                {
                    Schedule(_sim.Time + _config.EndAfterKillSeconds, EndReason.Kill);
                }
            }
        }

        void Schedule(float at, EndReason reason)
        {
            _endAt = at;
            _pendingReason = reason;
        }

        bool VictimScriptActive()
        {
            for (int i = 1; i < _sim.Worms.Count; i++)
            {
                Worm worm = _sim.Worms[i];
                if (worm.IsVictim) return worm.Alive && worm.VictimScriptActive;
            }
            return false;
        }

        void Enter(FlowState state)
        {
            State = state;
            _stateSince = _sim.Time;
        }
    }
}
