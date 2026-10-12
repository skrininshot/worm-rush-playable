using System;

namespace WormRush.Core
{
    /// <summary>One worm: a chain of segments pulled along by the head. Data plus its own movement.</summary>
    public sealed class Worm
    {
        public readonly int Id;
        public readonly bool IsPlayer;
        public readonly bool IsVictim;
        public readonly int ColorId;

        public bool Alive = true;
        public float Length;
        public float Radius;
        public float Angle;
        public float WantAngle;
        public float Speed;

        /// <summary>Segments[0] is the head. Only the first SegmentCount entries are valid.</summary>
        public Vec2[] Segments;
        public int SegmentCount;

        // Bot steering state (written by BotBrain).
        public Vec2 Target;
        public bool HasTarget;
        public float ThinkTimer;   // seconds until the bot picks a new food target
        public bool VictimScriptActive;

        readonly SimConfig _config;

        public Worm(int id, bool isPlayer, bool isVictim, int colorId, int startLength, SimConfig config)
        {
            Id = id;
            IsPlayer = isPlayer;
            IsVictim = isVictim;
            ColorId = colorId;
            _config = config;
            Length = startLength;
            Radius = config.WormRadius(Length);
            SegmentCount = Math.Max(1, startLength);
            Segments = new Vec2[Math.Max(SegmentCount * 2, 64)];
            Place(0f, 0f, 0f);
        }

        public Vec2 Head
        {
            get { return Segments[0]; }
        }

        /// <summary>Puts the head at (x, y) facing <paramref name="angle"/> with the body laid out straight behind it.</summary>
        public void Place(float x, float y, float angle)
        {
            Angle = angle;
            WantAngle = angle;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            for (int i = 0; i < SegmentCount; i++)
                Segments[i] = new Vec2(x - cos * i * _config.SegmentSpacing, y - sin * i * _config.SegmentSpacing);
        }

        /// <summary>Eating adds length; new segments appear stacked on the tail and unfold as the worm moves.</summary>
        public void Grow(int foodValue)
        {
            Length += foodValue * _config.LengthPerFoodValue;
            Radius = _config.WormRadius(Length);

            int target = (int)MathF.Floor(Length);
            if (target > Segments.Length)
                Array.Resize(ref Segments, Math.Max(target, Segments.Length * 2));
            Vec2 tail = Segments[SegmentCount - 1];
            while (SegmentCount < target)
                Segments[SegmentCount++] = tail;
        }

        /// <summary>Turns toward WantAngle (rate-limited), moves the head, then drags the chain after it.</summary>
        public void Advance(float dt)
        {
            float d = WantAngle - Angle;
            d = MathF.Atan2(MathF.Sin(d), MathF.Cos(d));
            float maxTurn = _config.TurnRate * dt;
            Angle += Math.Max(-maxTurn, Math.Min(maxTurn, d));

            Vec2 head = Segments[0];
            Segments[0] = new Vec2(
                head.X + MathF.Cos(Angle) * Speed * dt,
                head.Y + MathF.Sin(Angle) * Speed * dt);

            float spacing = _config.SegmentSpacing;
            Vec2 prev = Segments[0];
            for (int i = 1; i < SegmentCount; i++)
            {
                Vec2 seg = Segments[i];
                float dx = prev.X - seg.X;
                float dy = prev.Y - seg.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > spacing)
                {
                    float k = (dist - spacing) / dist;
                    seg = new Vec2(seg.X + dx * k, seg.Y + dy * k);
                    Segments[i] = seg;
                }
                prev = seg;
            }
        }
    }
}
