namespace WormRush.Core
{
    /// <summary>Plain 2D point. Core has no UnityEngine, so it carries its own.</summary>
    public readonly struct Vec2
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public static float SqrDistance(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
