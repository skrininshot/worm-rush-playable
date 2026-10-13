using UnityEngine;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>Burst particles from a JuiceState: flat circles in the worm's colour that fade as their life runs out.</summary>
    public sealed class ParticleRenderer
    {
        readonly PlayableConfig _config;
        readonly ShaderPalette _palette;
        readonly CircleBatch _batch;

        public ParticleRenderer(PlayableConfig config, ShaderPalette palette, CircleBatch batch)
        {
            _config = config;
            _palette = palette;
            _batch = batch;
        }

        public void Draw(JuiceState juice, in ViewBounds view)
        {
            ParticlePool pool = juice.Particles;
            float radius = _config.Juice.ParticleRadius;
            float margin = _config.CullMargin;
            int colors = _palette.Primary.Length;

            for (int i = 0; i < pool.Count; i++)
            {
                ref Particle p = ref pool.Items[i];
                if (!view.Contains(p.X, p.Y, margin)) continue;

                Vector4 color = _palette.Primary[p.ColorId % colors];
                color.w *= Mathf.Clamp01(p.Life);
                _batch.Add(p.X, p.Y, radius, color);
            }
        }
    }
}
