using UnityEngine;
using WormRush.Config;

namespace WormRush.View
{
    /// <summary>Arena floor: border ring, floor disc and the staggered dot grid that shows movement.</summary>
    public sealed class ArenaRenderer
    {
        readonly PlayableConfig _config;
        readonly ShaderPalette _palette;
        readonly CircleBatch _batch;

        public ArenaRenderer(PlayableConfig config, ShaderPalette palette, CircleBatch batch)
        {
            _config = config;
            _palette = palette;
            _batch = batch;
        }

        public void Draw(in ViewBounds view)
        {
            float radius = _config.Sim.ArenaRadius;
            float half = _config.ArenaBorderWidth * 0.5f;
            _batch.Add(0f, 0f, radius + half, _palette.ArenaBorder);
            _batch.Add(0f, 0f, radius - half, _palette.Arena);

            float spacing = _config.GridSpacing;
            float margin = _config.CullMargin;
            float innerRadius = radius - _config.ArenaBorderWidth;
            float innerSqr = innerRadius * innerRadius;

            int rowFrom = Mathf.FloorToInt((view.CenterY - view.HalfHeight - margin) / spacing);
            int rowTo = Mathf.CeilToInt((view.CenterY + view.HalfHeight + margin) / spacing);
            int colFrom = Mathf.FloorToInt((view.CenterX - view.HalfWidth - margin) / spacing);
            int colTo = Mathf.CeilToInt((view.CenterX + view.HalfWidth + margin) / spacing);

            for (int row = rowFrom; row <= rowTo; row++)
            {
                float y = row * spacing;
                float stagger = (row & 1) != 0 ? spacing * 0.5f : 0f;
                for (int col = colFrom; col <= colTo; col++)
                {
                    float x = col * spacing + stagger;
                    if (x * x + y * y > innerSqr) continue;
                    _batch.Add(x, y, _config.GridDotRadius, _palette.GridDot);
                }
            }
        }
    }
}
