using UnityEngine;
using UnityEngine.InputSystem;

namespace WormRush.View
{
    /// <summary>
    /// One pointer for mouse and touch (Input System's Pointer.current). Mirrors the reference's
    /// pointerdown/pointermove: a press and any movement report a position, standing still does not,
    /// so lifting a finger keeps the last heading.
    /// </summary>
    public sealed class PointerInput
    {
        Vector2 _lastPosition;
        bool _hasLast;

        public bool PressedThisFrame { get; private set; }
        public bool MovedThisFrame { get; private set; }

        /// <summary>Screen pixels, origin bottom-left (same orientation as the world).</summary>
        public Vector2 Position { get; private set; }

        /// <summary>True for a mouse; a touch screen has no hover, so nothing should react to where a finger last was.</summary>
        public bool IsMouse
        {
            get { return Pointer.current is Mouse; }
        }

        public bool RestartKeyPressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.rKey.wasPressedThisFrame;
            }
        }

        public void Update()
        {
            PressedThisFrame = false;
            MovedThisFrame = false;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            Position = pointer.position.ReadValue();
            PressedThisFrame = pointer.press.wasPressedThisFrame;
            MovedThisFrame = _hasLast && Position != _lastPosition;
            _lastPosition = Position;
            _hasLast = true;
        }
    }
}
