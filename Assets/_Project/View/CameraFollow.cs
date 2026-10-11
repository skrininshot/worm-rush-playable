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
            Vec2 head = player.Head;
            _camera.transform.position = new Vector3(head.X, head.Y, _config.CameraDepth);
            _camera.orthographicSize = ViewMath.OrthoSize(_camera.aspect, _config.ViewSpan, player.Length, _config.ZoomOutPerLength);

            float halfHeight = _camera.orthographicSize;
            return new ViewBounds(head.X, head.Y, halfHeight * _camera.aspect, halfHeight);
        }
    }
}
