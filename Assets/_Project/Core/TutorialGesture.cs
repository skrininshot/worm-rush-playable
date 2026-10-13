using System;

namespace WormRush.Core
{
    /// <summary>Where the tutorial finger is and how it looks at one moment of its loop. Positions are UI units from the circle's centre.</summary>
    public struct HandPose
    {
        public float X;
        public float Y;
        /// <summary>0 = hovering above the glass, 1 = pressed on it.</summary>
        public float Press;
        /// <summary>Scale of the hand around its fingertip: big as it arrives, small when pressed.</summary>
        public float Scale;
        public float Alpha;
        /// <summary>Ripple under the fingertip: 0..1 progress, or -1 when there is none.</summary>
        public float Ring;
        /// <summary>How visible the trail is (1 while dragging, fading out as the finger lifts).</summary>
        public float TrailFade;
    }

    /// <summary>
    /// The "how to steer" demonstration: a finger lands, drags once round a circle with a trail behind it, lifts,
    /// and starts again. Pure function of time (no state), so the loop is the same wherever it is sampled.
    /// </summary>
    public sealed class TutorialGesture
    {
        readonly UiJuiceConfig _c;
        readonly float _cycle;

        public TutorialGesture(UiJuiceConfig config)
        {
            _c = config;
            _cycle = Math.Max(1e-3f, config.TutorialLandSeconds + config.TutorialHoldSeconds + config.TutorialDragSeconds +
                                     config.TutorialLiftSeconds + config.TutorialRestSeconds);
        }

        public float CycleSeconds
        {
            get { return _cycle; }
        }

        public int TrailDots
        {
            get { return Math.Max(0, _c.TutorialTrailDots); }
        }

        float DragStart
        {
            get { return _c.TutorialLandSeconds + _c.TutorialHoldSeconds; }
        }

        float DragEnd
        {
            get { return DragStart + _c.TutorialDragSeconds; }
        }

        /// <summary>The finger at <paramref name="time"/> seconds since the tutorial appeared.</summary>
        public void Sample(float time, out HandPose pose)
        {
            float t = Wrap(time);
            PointAt(t, out pose.X, out pose.Y);

            float land = _c.TutorialLandSeconds > 0f ? UiMotion.Clamp01(t / _c.TutorialLandSeconds) : 1f;
            float lift = _c.TutorialLiftSeconds > 0f ? UiMotion.Clamp01((t - DragEnd) / _c.TutorialLiftSeconds) : (t >= DragEnd ? 1f : 0f);
            float rest = _c.TutorialRestSeconds > 0f ? UiMotion.Clamp01((t - DragEnd - _c.TutorialLiftSeconds) / _c.TutorialRestSeconds) : 0f;

            float landEase = UiMotion.EaseOut(land);
            // Down on the glass from the landing's end until the finger lets go; the lift springs it back up.
            float press = landEase * (1f - UiMotion.EaseOut(lift));
            pose.Press = press;
            pose.Scale = _c.TutorialLandScale + (_c.TutorialPressScale - _c.TutorialLandScale) * landEase;
            pose.Scale += (1f - _c.TutorialPressScale) * UiMotion.EaseOutBack(lift, 2.2f);
            // Fades in as it lands, and out over the rest.
            pose.Alpha = landEase * (1f - UiMotion.EaseOut(rest));

            float ringSeconds = _c.TutorialRingSeconds;
            pose.Ring = ringSeconds > 0f && t < ringSeconds + _c.TutorialLandSeconds * 0.5f && t >= _c.TutorialLandSeconds * 0.5f
                ? (t - _c.TutorialLandSeconds * 0.5f) / ringSeconds
                : -1f;
            pose.TrailFade = 1f - UiMotion.EaseOut(lift);
        }

        /// <summary>
        /// Dot <paramref name="index"/> of the trail (0 = nearest the fingertip). False when that part of the path has
        /// not been dragged yet in this round. <paramref name="strength"/> is 1 for the newest dot, towards 0 for the oldest.
        /// </summary>
        public bool TrailPoint(float time, int index, out float x, out float y, out float strength)
        {
            x = 0f;
            y = 0f;
            strength = 0f;
            int dots = TrailDots;
            if (index < 0 || index >= dots) return false;

            float t = Wrap(time);
            float at = Math.Min(t, DragEnd) - index * _c.TutorialTrailSpacingSeconds;
            if (at < DragStart || t < DragStart) return false;

            PointAt(at, out x, out y);
            strength = 1f - (float)index / dots;
            return true;
        }

        /// <summary>The fingertip at a time inside the cycle: parked at the start of the circle, riding it while dragging.</summary>
        void PointAt(float t, out float x, out float y)
        {
            float progress = _c.TutorialDragSeconds > 0f ? UiMotion.Clamp01((t - DragStart) / _c.TutorialDragSeconds) : 1f;
            // Slow at both ends: pressing and letting go read as deliberate, the middle reads as a sweep.
            float eased = progress * progress * (3f - 2f * progress);
            float angle = _c.TutorialStartDegrees * (MathF.PI / 180f) - eased * 2f * MathF.PI;
            x = MathF.Cos(angle) * _c.TutorialRadius;
            y = MathF.Sin(angle) * _c.TutorialRadius;
        }

        float Wrap(float time)
        {
            if (time < 0f) return 0f;
            return time - MathF.Floor(time / _cycle) * _cycle;
        }
    }
}
