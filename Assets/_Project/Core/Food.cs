namespace WormRush.Core
{
    /// <summary>One food pellet. A struct in a flat array: no allocation when food is eaten and respawned.</summary>
    public struct Food
    {
        public float X;
        public float Y;
        public int Value;
        public float Radius;
        public int ColorId;
        public float Phase;     // for the view's pulse animation

        /// <summary>Bookkeeping inside one sim step; never true when the View reads the array.</summary>
        public bool Eaten;
    }
}
