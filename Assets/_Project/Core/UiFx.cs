using System;

namespace WormRush.Core
{
    public enum UiFxKind : byte
    {
        /// <summary>A small rectangle that tumbles, flips and falls.</summary>
        Confetti,

        /// <summary>A four-point sparkle that swells and shrinks.</summary>
        Star,

        /// <summary>A thin circle that expands and fades (a shockwave).</summary>
        Ring,

        /// <summary>A round speck that shrinks away.</summary>
        Dot,
    }

    /// <summary>One bit of screen-space confetti. Position is in UI units from the screen centre, y up.</summary>
    public struct UiPiece
    {
        public UiFxKind Kind;
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Drag;          // 1/s
        public float Gravity;       // UI units per s^2, pulls down
        public float Rotation;      // degrees
        public float Spin;          // degrees per second
        public float Phase;         // radians; confetti flips about its own axis as this advances
        public float FlipSpeed;     // rad/s
        public float Size;          // width of a confetti, diameter of the rest
        public float Grow;          // rings: size multiplier reached at the end of life
        public float Life;          // seconds left
        public float MaxLife;
        /// <summary>>= 0: index into the palette; -1: white; -2: gold.</summary>
        public int ColorSlot;
    }

    /// <summary>Fixed-size pool of UI pieces: nothing is allocated while playing, pieces past capacity are dropped.</summary>
    public sealed class UiFxPool
    {
        public const int ColorWhite = -1;
        public const int ColorGold = -2;
        const float ConfettiAspect = 0.55f;      // height / width
        const float ConfettiMinFlip = 0.2f;      // a confetti never goes fully edge-on
        const float ConfettiFadeFraction = 0.3f; // of the life, at the end
        const float RingFadePower = 1.5f;

        public readonly UiPiece[] Items;

        public UiFxPool(int capacity)
        {
            Items = new UiPiece[Math.Max(1, capacity)];
        }

        public int Count { get; private set; }

        public bool Add(in UiPiece piece)
        {
            if (Count == Items.Length) return false;
            Items[Count++] = piece;
            return true;
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Step(float dt)
        {
            int write = 0;
            for (int read = 0; read < Count; read++)
            {
                UiPiece p = Items[read];
                p.Life -= dt;
                if (p.Life <= 0f) continue;

                float damp = MathF.Exp(-p.Drag * dt);
                p.VX *= damp;
                p.VY = p.VY * damp - p.Gravity * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.Rotation += p.Spin * dt;
                p.Phase += p.FlipSpeed * dt;
                Items[write++] = p;
            }
            Count = write;
        }

        /// <summary>Drawn size and opacity of a piece right now.</summary>
        public static void Look(in UiPiece p, out float width, out float height, out float alpha)
        {
            float t = p.MaxLife > 0f ? 1f - p.Life / p.MaxLife : 1f;
            t = UiMotion.Clamp01(t);

            switch (p.Kind)
            {
                case UiFxKind.Confetti:
                    float flip = MathF.Max(MathF.Abs(MathF.Cos(p.Phase)), ConfettiMinFlip);
                    width = p.Size * flip;
                    height = p.Size * ConfettiAspect;
                    alpha = UiMotion.Clamp01(p.Life / (p.MaxLife * ConfettiFadeFraction));
                    break;
                case UiFxKind.Star:
                    width = p.Size * UiMotion.Bump(t);
                    height = width;
                    alpha = 1f;
                    break;
                case UiFxKind.Ring:
                    width = p.Size * (1f + (p.Grow - 1f) * UiMotion.EaseOut(t));
                    height = width;
                    alpha = MathF.Pow(1f - t, RingFadePower);
                    break;
                default:
                    width = p.Size * (1f - t);
                    height = width;
                    alpha = 1f;
                    break;
            }
        }
    }
}
