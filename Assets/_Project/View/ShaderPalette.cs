using UnityEngine;
using WormRush.Config;

namespace WormRush.View
{
    /// <summary>
    /// PlayableConfig colours converted once to what the instanced shader needs.
    /// Per-instance vectors bypass Unity's automatic colour-space conversion, so it is done here.
    /// </summary>
    public sealed class ShaderPalette
    {
        public readonly Vector4[] Primary;
        public readonly Vector4[] Secondary;
        public readonly Vector4[] FoodCore;
        public readonly Vector4[] FoodGlow;
        public readonly Vector4 Shadow;
        public readonly Vector4 Arena;
        public readonly Vector4 ArenaBorder;
        public readonly Vector4 GridDot;
        public readonly Vector4 EyeWhite;
        public readonly Vector4 Pupil;

        public ShaderPalette(PlayableConfig config)
        {
            int count = config.Palette.Length;
            Primary = new Vector4[count];
            Secondary = new Vector4[count];
            FoodCore = new Vector4[count];
            FoodGlow = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                Primary[i] = ToShader(config.PrimaryColor(i));
                Secondary[i] = ToShader(config.SecondaryColor(i));
                FoodCore[i] = Primary[i];
                Color glow = config.PrimaryColor(i);
                glow.a = config.FoodGlowAlpha;
                FoodGlow[i] = ToShader(glow);
            }

            Shadow = ToShader(config.ShadowColor);
            Arena = ToShader(config.ArenaColor);
            ArenaBorder = ToShader(config.ArenaBorderColor);
            GridDot = ToShader(config.GridDotColor);
            EyeWhite = ToShader(config.EyeColor);
            Pupil = ToShader(config.PupilColor);
        }

        static Vector4 ToShader(Color color)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            return new Vector4(c.r, c.g, c.b, c.a);
        }
    }
}
