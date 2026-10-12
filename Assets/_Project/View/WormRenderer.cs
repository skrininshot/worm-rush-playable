using UnityEngine;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>
    /// Worms as stripes of circles: shadow pass, body pass (tail to head), eyes.
    /// Worms are drawn from the last bot down to the player, so the player is always on top.
    /// </summary>
    public sealed class WormRenderer
    {
        readonly PlayableConfig _config;
        readonly ShaderPalette _palette;
        readonly CircleBatch _batch;

        public WormRenderer(PlayableConfig config, ShaderPalette palette, CircleBatch batch)
        {
            _config = config;
            _palette = palette;
            _batch = batch;
        }

        public void Draw(WormSim sim, in ViewBounds view)
        {
            for (int w = sim.Worms.Count - 1; w >= 0; w--)
            {
                Worm worm = sim.Worms[w];
                if (worm.Alive) DrawWorm(worm, view);
            }
        }

        void DrawWorm(Worm worm, in ViewBounds view)
        {
            int colorIndex = worm.ColorId % _palette.Primary.Length;
            Vector4 primary = _palette.Primary[colorIndex];
            Vector4 secondary = _palette.Secondary[colorIndex];
            float radius = worm.Radius;
            float margin = _config.CullMargin + radius;
            Vector2 shadow = _config.ShadowOffset;
            int stripe = _config.StripeLength;

            for (int s = worm.SegmentCount - 1; s >= 0; s--)
            {
                Vec2 p = worm.Segments[s];
                if (view.Contains(p.X, p.Y, margin))
                    _batch.Add(p.X + shadow.x, p.Y + shadow.y, radius, _palette.Shadow);
            }

            for (int s = worm.SegmentCount - 1; s >= 0; s--)
            {
                Vec2 p = worm.Segments[s];
                if (!view.Contains(p.X, p.Y, margin)) continue;
                _batch.Add(p.X, p.Y, radius, (s / stripe & 1) != 0 ? secondary : primary);
            }

            DrawEyes(worm, radius);
        }

        void DrawEyes(Worm worm, float radius)
        {
            Vec2 head = worm.Head;
            float cos = Mathf.Cos(worm.Angle);
            float sin = Mathf.Sin(worm.Angle);
            float forward = radius * _config.EyeForward;
            float side = radius * _config.EyeSide;
            float pupilForward = radius * _config.PupilForward;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                float ex = head.X + cos * forward - sin * side * sign;
                float ey = head.Y + sin * forward + cos * side * sign;
                _batch.Add(ex, ey, radius * _config.EyeRadius, _palette.EyeWhite);
                _batch.Add(ex + cos * pupilForward, ey + sin * pupilForward, radius * _config.PupilRadius, _palette.Pupil);
            }
        }
    }
}
