using UnityEngine;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>
    /// Keeps the player's head at the screen centre (rigid follow, as the controls assume) and sizes the view
    /// so the shorter screen side shows the same amount of world in portrait and landscape.
    /// </summary>
    public sealed class CameraFollow
    {
        readonly Camera _camera;
        readonly PlayableConfig _config;

        public CameraFollow(Camera camera, PlayableConfig config)
        {
            _camera = camera;
            _config = config;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.OutsideColor;
            camera.transform.rotation = Quaternion.identity;
        }

        /// <summary>Moves the camera onto the player and returns the world rectangle it now sees.</summary>
        public ViewBounds Apply(Worm player)
        {
            return Apply(player, 0f, 0f);
        }

        /// <summary>
        /// Same, with the picture knocked off the player by a shake of (<paramref name="shakeX"/>, <paramref name="shakeY"/>)
        /// screen pixels. The returned rectangle is the shaken one, so culling follows what is really on screen.
        /// </summary>
        public ViewBounds Apply(Worm player, float shakeX, float shakeY)
        {
            Vec2 head = player.Head;
            _camera.orthographicSize = ViewMath.OrthoSize(_camera.aspect, _config.ViewSpan, player.Length, _config.ZoomOutPerLength);
            float halfHeight = _camera.orthographicSize;

            float worldPerPixel = _camera.pixelHeight > 0 ? 2f * halfHeight / _camera.pixelHeight : 0f;
            float x = head.X + shakeX * worldPerPixel;
            float y = head.Y + shakeY * worldPerPixel;
            _camera.transform.position = new Vector3(x, y, _config.CameraDepth);
            return new ViewBounds(x, y, halfHeight * _camera.aspect, halfHeight);
        }
    }
}
