using UnityEngine;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>Food pellets: a soft glow circle under a solid core that pulses with its own phase.</summary>
    public sealed class FoodRenderer
    {
        readonly PlayableConfig _config;
        readonly ShaderPalette _palette;
        readonly CircleBatch _batch;

        public FoodRenderer(PlayableConfig config, ShaderPalette palette, CircleBatch batch)
        {
            _config = config;
            _palette = palette;
            _batch = batch;
        }

        public void Draw(WormSim sim, in ViewBounds view)
        {
            Food[] items = sim.FoodItems;
            int count = sim.FoodCount;
            int colors = _palette.FoodCore.Length;
            float margin = _config.CullMargin;
            float pulseTime = sim.Time * _config.FoodPulseSpeed;

            for (int i = 0; i < count; i++)
            {
                ref Food food = ref items[i];
                if (!view.Contains(food.X, food.Y, margin)) continue;

                float pulse = 1f + _config.FoodPulseAmount * Mathf.Sin(pulseTime + food.Phase);
                float radius = food.Radius * pulse;
                int color = food.ColorId % colors;
                _batch.Add(food.X, food.Y, radius * _config.FoodGlowScale, _palette.FoodGlow[color]);
                _batch.Add(food.X, food.Y, radius, _palette.FoodCore[color]);
            }
        }
    }
}
