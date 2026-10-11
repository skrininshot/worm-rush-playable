using UnityEngine;

namespace WormRush.View
{
    /// <summary>The world rectangle the camera sees; renderers skip what is clearly outside it.</summary>
    public readonly struct ViewBounds
    {
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly float HalfWidth;
        public readonly float HalfHeight;

        public ViewBounds(float centerX, float centerY, float halfWidth, float halfHeight)
        {
            CenterX = centerX;
            CenterY = centerY;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
        }

        public bool Contains(float x, float y, float margin)
        {
            return Mathf.Abs(x - CenterX) <= HalfWidth + margin && Mathf.Abs(y - CenterY) <= HalfHeight + margin;
        }
    }
}
