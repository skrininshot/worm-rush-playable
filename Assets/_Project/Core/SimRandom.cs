namespace WormRush.Core
{
    /// <summary>
    /// Small seeded xorshift generator. Own implementation so a given seed produces
    /// the same run on every runtime (System.Random makes no such promise).
    /// </summary>
    public sealed class SimRandom
    {
        uint _state;

        public SimRandom(int seed)
        {
            _state = unchecked((uint)seed * 2654435761u + 0x9E3779B9u);
            if (_state == 0u) _state = 0x6D2B79F5u;
            for (int i = 0; i < 4; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint s = _state;
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            _state = s;
            return s;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            return (int)(NextFloat() * maxExclusive);
        }
    }
}
