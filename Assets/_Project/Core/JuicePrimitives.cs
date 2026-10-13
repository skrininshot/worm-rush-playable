using System;

namespace WormRush.Core
{
    /// <summary>Counts pellets eaten in a row. The streak starts over after a pause longer than the window.</summary>
    public sealed class ComboMeter
    {
        readonly float _windowSeconds;
        float _lastAt = float.NegativeInfinity;

        public ComboMeter(float windowSeconds)
        {
            _windowSeconds = windowSeconds;
        }

        public int Streak { get; private set; }

        /// <summary>Records one pellet at <paramref name="time"/> and returns the streak including it (1 for the first).</summary>
        public int Register(float time)
        {
            if (_windowSeconds > 0f && time - _lastAt > _windowSeconds) Streak = 0;
            Streak++;
            _lastAt = time;
            return Streak;
        }
    }

    /// <summary>Time left on a camera shake; the offset is random each frame and proportional to what is left.</summary>
    public sealed class ScreenShake
    {
        readonly float _amplitude;

        public ScreenShake(float amplitude)
        {
            _amplitude = amplitude;
        }

        public float Remaining { get; private set; }

        /// <summary>Starts or extends a shake; a shorter one never cuts a longer one short.</summary>
        public void Trigger(float seconds)
        {
            if (seconds > Remaining) Remaining = seconds;
        }

        public void Step(float dt)
        {
            Remaining = Math.Max(0f, Remaining - dt);
        }

        /// <summary>Offset for this frame, in the config's UI units, each axis in +-Remaining * amplitude.</summary>
        public void Sample(SimRandom rng, out float x, out float y)
        {
            if (Remaining <= 0f)
            {
                x = 0f;
                y = 0f;
                return;
            }

            float size = Remaining * _amplitude;
            x = rng.Range(-1f, 1f) * size;
            y = rng.Range(-1f, 1f) * size;
        }
    }

    public struct Particle
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Life;   // seconds left; also the alpha, as in the reference (it never starts above 1)
        public int ColorId;
    }

    /// <summary>Fixed-size pool of burst particles: no allocation while playing, extra bursts past capacity are dropped.</summary>
    public sealed class ParticlePool
    {
        public readonly Particle[] Items;
        readonly JuiceConfig _config;

        public ParticlePool(JuiceConfig config)
        {
            _config = config;
            Items = new Particle[Math.Max(1, config.ParticleCapacity)];
        }

        public int Count { get; private set; }

        public void Burst(float x, float y, int colorId, SimRandom rng)
        {
            for (int i = 0; i < _config.BurstCount && Count < Items.Length; i++)
            {
                float angle = rng.Range(0f, MathF.PI * 2f);
                float speed = rng.Range(_config.BurstSpeedMin, _config.BurstSpeedMax);
                Items[Count++] = new Particle
                {
                    X = x,
                    Y = y,
                    VX = MathF.Cos(angle) * speed,
                    VY = MathF.Sin(angle) * speed,
                    Life = rng.Range(_config.ParticleLifeMin, _config.ParticleLifeMax),
                    ColorId = colorId,
                };
            }
        }

        public void Step(float dt)
        {
            float damp = MathF.Exp(-_config.ParticleDrag * dt);
            int write = 0;
            for (int read = 0; read < Count; read++)
            {
                Particle p = Items[read];
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.VX *= damp;
                p.VY *= damp;
                p.Life -= dt;
                if (p.Life <= 0f) continue;
                Items[write++] = p;
            }
            Count = write;
        }
    }

    public enum PopupKind : byte
    {
        /// <summary>"+1 KILL!" over the head of a worm the player just killed.</summary>
        Kill,

        /// <summary>"+N" where the player ate a pellet worth N.</summary>
        Value,
    }

    public struct Popup
    {
        public float X;
        public float Y;
        public float Life;   // seconds left
        public float Scale;
        public PopupKind Kind;
        public int Value;
    }

    /// <summary>Few short-lived world-space texts, oldest first (so the newest draws on top).</summary>
    public sealed class PopupPool
    {
        public readonly Popup[] Items;
        readonly JuiceConfig _config;

        public PopupPool(JuiceConfig config)
        {
            _config = config;
            Items = new Popup[Math.Max(1, config.PopupCapacity)];
        }

        public int Count { get; private set; }

        public void Spawn(float x, float y, PopupKind kind, int value, float scale)
        {
            if (Count == Items.Length)
            {
                // Full: the oldest makes room.
                Array.Copy(Items, 1, Items, 0, Count - 1);
                Count--;
            }

            Items[Count++] = new Popup { X = x, Y = y, Life = _config.PopupLifeSeconds, Scale = scale, Kind = kind, Value = value };
        }

        public void Step(float dt)
        {
            int write = 0;
            for (int read = 0; read < Count; read++)
            {
                Popup p = Items[read];
                p.Y += _config.PopupRiseSpeed * dt;
                p.Life -= dt;
                if (p.Life <= 0f) continue;
                Items[write++] = p;
            }
            Count = write;
        }

        /// <summary>0..1 opacity: fully opaque, then fading over the last PopupFadeSeconds.</summary>
        public float Alpha(in Popup popup)
        {
            if (_config.PopupFadeSeconds <= 0f) return 1f;
            float a = popup.Life / _config.PopupFadeSeconds;
            return a < 0f ? 0f : (a > 1f ? 1f : a);
        }
    }

    /// <summary>Renders a SoundCue into samples. Same shape as the reference's oscillator + exponential gain ramp.</summary>
    public static class ToneSynth
    {
        const float SilenceGain = 0.0001f;   // WebAudio's exponentialRampToValueAtTime target in the reference
        const float AttackSeconds = 0.002f;  // short fade-in so a square wave does not click

        public static int SampleCount(float seconds, int sampleRate)
        {
            return Math.Max(1, (int)MathF.Round(seconds * sampleRate));
        }

        public static float[] Render(SoundCue cue, float frequency, int sampleRate)
        {
            float[] samples = new float[SampleCount(cue.Seconds, sampleRate)];
            float decayPerSample = MathF.Pow(SilenceGain, 1f / samples.Length);
            float gain = 1f;
            float phase = 0f;
            float phaseStep = frequency / sampleRate;
            int attack = Math.Max(1, (int)(AttackSeconds * sampleRate));

            for (int i = 0; i < samples.Length; i++)
            {
                float fade = i < attack ? (float)(i + 1) / attack : 1f;
                samples[i] = Wave(cue.Wave, phase) * gain * fade;
                gain *= decayPerSample;
                phase += phaseStep;
                if (phase >= 1f) phase -= 1f;
            }

            return samples;
        }

        /// <summary>One cycle of the waveform, in [-1, 1], at <paramref name="phase"/> in [0, 1).</summary>
        public static float Wave(Waveform wave, float phase)
        {
            switch (wave)
            {
                case Waveform.Square:
                    return phase < 0.5f ? 1f : -1f;
                case Waveform.Sawtooth:
                    return 2f * phase - 1f;
                case Waveform.Triangle:
                    return phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase;
                default:
                    return MathF.Sin(phase * MathF.PI * 2f);
            }
        }
    }
}
