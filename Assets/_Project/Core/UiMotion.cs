using System;

namespace WormRush.Core
{
    /// <summary>Easing curves and the heartbeat, as plain functions of time.</summary>
    public static class UiMotion
    {
        const float HeartbeatWidth = 0.16f;       // of the period, each beat
        const float HeartbeatSecondAt = 0.2f;     // of the period
        const float HeartbeatSecondWeight = 0.7f;

        public static float Clamp01(float t)
        {
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        /// <summary>Quadratic ease-out: fast start, soft landing. 0 to 1 over t in [0, 1].</summary>
        public static float EaseOut(float t)
        {
            float inv = 1f - Clamp01(t);
            return 1f - inv * inv;
        }

        /// <summary>Runs past 1 and comes back; <paramref name="overshoot"/> 1.7 is the classic, 2.5 is a big one.</summary>
        public static float EaseOutBack(float t, float overshoot)
        {
            float x = Clamp01(t) - 1f;
            return 1f + x * x * ((overshoot + 1f) * x + overshoot);
        }

        /// <summary>Rubber band: overshoots and rings a few times before settling on 1.</summary>
        public static float ElasticOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * (2f * MathF.PI / 3f)) + 1f;
        }

        /// <summary>0 at both ends of [0, 1], 1 in the middle (a smooth hump); 0 outside the range.</summary>
        public static float Bump(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            return MathF.Sin(t * MathF.PI);
        }

        public static float Repeat(float t)
        {
            return t - MathF.Floor(t);
        }

        /// <summary>
        /// Two quick beats and a rest, per period: 0 between them, up to <paramref name="amount"/> on the beat.
        /// Added to a scale of 1, it reads as a pulse that is alive rather than a metronome.
        /// </summary>
        public static float Heartbeat(float time, float period, float amount)
        {
            if (period <= 0f || amount == 0f) return 0f;
            float p = Repeat(time / period);
            return amount * (Beat(p, 0f) + HeartbeatSecondWeight * Beat(p, HeartbeatSecondAt));
        }

        static float Beat(float p, float start)
        {
            float x = (p - start) / HeartbeatWidth;
            if (x <= 0f || x >= 1f) return 0f;
            float s = MathF.Sin(x * MathF.PI);
            return s * s;
        }
    }

    /// <summary>
    /// A damped spring. Nudge <see cref="Velocity"/> for a kick, set <see cref="Value"/> for a snap; it rings
    /// round <see cref="Target"/> and settles. Steps in small slices, so a long frame cannot blow it up.
    /// </summary>
    public struct Spring
    {
        const float MaxSlice = 1f / 120f;

        public float Value;
        public float Velocity;
        public float Target;

        public void Step(float dt, float stiffness, float damping)
        {
            if (dt <= 0f) return;
            int slices = (int)MathF.Ceiling(dt / MaxSlice);
            float h = dt / slices;
            for (int i = 0; i < slices; i++)
            {
                Velocity += ((Target - Value) * stiffness - Velocity * damping) * h;
                Value += Velocity * h;
            }
        }

        public void Limit(float max)
        {
            if (Value > max) Value = max;
            else if (Value < -max) Value = -max;
        }
    }
}
