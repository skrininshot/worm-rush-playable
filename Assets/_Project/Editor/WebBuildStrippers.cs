using UnityEditor.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WormRush.EditorTools
{
    /// <summary>
    /// URP keeps every post-processing resource (10 film grain textures, SMAA lookup tables, the uber post shaders)
    /// in its global settings, so they ship in every player: about 2.5 MB of PNGs for a game that has no
    /// post-processing at all. Asking the pipeline to drop them takes them out of the Web build; only
    /// <see cref="WebBuild"/> switches this on, so an editor session or another build target is not touched.
    /// </summary>
    static class WebBuildStrip
    {
        public static bool Enabled;
    }

    class StripPostProcessTextures : IRenderPipelineGraphicsSettingsStripper<PostProcessData.TextureResources>
    {
        public bool active => WebBuildStrip.Enabled;
        public bool CanRemoveSettings(PostProcessData.TextureResources settings) => true;
    }
}
