using UnityEngine;

namespace WormRush.View
{
    /// <summary>
    /// One pointer for mouse and touch (the legacy Input class: the Input System package pulls UIElements and the
    /// whole Physics module into the Web player, about a megabyte of wasm, and this playable reads one pointer
    /// and one key). Mirrors the reference's pointerdown/pointermove: a press and any movement report a position,
    /// standing still does not, so lifting a finger keeps the last heading.
    /// </summary>
    public sealed class PointerInput
    {
        Vector2 _lastPosition;
        bool _hasLast;
        bool _touching;

        public bool PressedThisFrame { get; private set; }
        public bool MovedThisFrame { get; private set; }

        /// <summary>Screen pixels, origin bottom-left (same orientation as the world).</summary>
        public Vector2 Position { get; private set; }

        /// <summary>True for a mouse; a touch screen has no hover, so nothing should react to where a finger last was.</summary>
        public bool IsMouse
        {
            get { return !_touching; }
        }

        public bool RestartKeyPressed
        {
            get { return Input.GetKeyDown(KeyCode.R); }
        }

        public void Update()
        {
            PressedThisFrame = false;
            MovedThisFrame = false;

            Vector2 position;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                position = touch.position;
                PressedThisFrame = touch.phase == TouchPhase.Began;
                _touching = true;
            }
            else
            {
                position = Input.mousePosition;
                PressedThisFrame = Input.GetMouseButtonDown(0);
                // After a finger lifts the mouse sits where the finger was; only a mouse that moved or pressed
                // hands the pointer back to the mouse.
                if (_touching && (PressedThisFrame || (_hasLast && position != _lastPosition))) _touching = false;
            }

            Position = position;
            MovedThisFrame = _hasLast && Position != _lastPosition;
            _lastPosition = Position;
            _hasLast = true;
        }
    }
}
