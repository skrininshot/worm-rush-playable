using System;

namespace WormRush.Core
{
    /// <summary>
    /// Maps a pointer position to the heading the player's worm should turn toward.
    /// The player is always at the screen centre, so the heading is just the direction
    /// from the centre to the pointer (screen Y up, same as the world).
    /// </summary>
    public static class Steering
    {
        /// <summary>
        /// <paramref name="dx"/>, <paramref name="dy"/>: pointer minus screen centre, in pixels.
        /// Returns false when the pointer is inside the dead zone; the worm then keeps its last heading.
        /// </summary>
        public static bool TryGetHeading(float dx, float dy, float deadZone, out float angle)
        {
            angle = 0f;
            if (dx * dx + dy * dy <= deadZone * deadZone) return false;
            angle = MathF.Atan2(dy, dx);
            return true;
        }
    }
}
