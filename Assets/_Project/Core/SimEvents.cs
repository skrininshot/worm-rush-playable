namespace WormRush.Core
{
    public enum SimEventType : byte
    {
        /// <summary>WormId ate a pellet of Value at (X, Y).</summary>
        Ate,

        /// <summary>WormId died at (X, Y). OtherId is the worm whose body it hit, or -1 for the arena edge.</summary>
        Killed,

        /// <summary>The player died (also reported as Killed). OtherId as in Killed.</summary>
        PlayerDied,
    }

    /// <summary>What happened during the last WormSim.Step. The View reads these for juice and sounds.</summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly int WormId;
        public readonly int OtherId;
        public readonly int Value;
        public readonly float X;
        public readonly float Y;

        public SimEvent(SimEventType type, int wormId, int otherId, int value, float x, float y)
        {
            Type = type;
            WormId = wormId;
            OtherId = otherId;
            Value = value;
            X = x;
            Y = y;
        }
    }
}
