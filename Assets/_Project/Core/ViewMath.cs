namespace WormRush.Core
{
    /// <summary>Camera zoom rule. Plain math so the same framing works for portrait and landscape and can be tested.</summary>
    public static class ViewMath
    {
        /// <summary>
        /// Half of the visible world height for an orthographic camera.
        /// The shorter screen side always shows <paramref name="viewSpan"/> world units (widened a little
        /// per unit of worm length, so the view zooms out as the worm grows); the longer side shows more.
        /// </summary>
        /// <param name="aspect">Screen width / height.</param>
        public static float OrthoSize(float aspect, float viewSpan, float wormLength, float zoomOutPerLength)
        {
            if (aspect <= 0f) aspect = 1f;
            float span = viewSpan * (1f + wormLength * zoomOutPerLength);
            // Portrait: the width is the short side, so the height is span / aspect. Landscape: the height is the short side.
            float height = aspect < 1f ? span / aspect : span;
            return height * 0.5f;
        }
    }
}
