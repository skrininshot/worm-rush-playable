using System;

namespace WormRush.Core
{
    public enum Waveform : byte
    {
        Sine,
        Square,
        Sawtooth,
        Triangle,
    }

    /// <summary>One synthesized blip: a tone with an exponential decay, like the reference's WebAudio oscillator.</summary>
    [Serializable]
    public struct SoundCue
    {
        public Waveform Wave;
        public float Frequency;      // Hz
        public float Seconds;        // length; the envelope falls to silence over exactly this long
        public float Volume;         // AudioSource volume, 0..1
        public float DelaySeconds;   // from the moment the cue is requested

        public SoundCue(Waveform wave, float frequency, float seconds, float volume, float delaySeconds = 0f)
        {
            Wave = wave;
            Frequency = frequency;
            Seconds = seconds;
            Volume = volume;
            DelaySeconds = delaySeconds;
        }
    }

    /// <summary>
    /// Every juice tunable: shake, particles, popups, combo and the synthesized sounds. Plain C# like SimConfig;
    /// PlayableConfig embeds one. Defaults reproduce reference/worm-rush-playable.html.
    /// </summary>
    [Serializable]
    public sealed class JuiceConfig
    {
        // ---- Camera shake ----
        public float ShakeKillSeconds = 0.35f;
        public float ShakeDeathSeconds = 0.5f;
        public float ShakeAmplitude = 18f;          // UI units (390 x 844 reference px) per second of shake left

        // ---- Burst particles (a worm dying) ----
        public int ParticleCapacity = 256;
        public int BurstCount = 26;
        public float BurstSpeedMin = 60f;
        public float BurstSpeedMax = 260f;
        public float ParticleLifeMin = 0.4f;
        public float ParticleLifeMax = 0.9f;
        public float ParticleDrag = 5f;             // 1/s; the reference's x0.92 per 60 Hz frame
        public float ParticleRadius = 5f;

        // ---- Popups ("+1 KILL!", "+3") ----
        public int PopupCapacity = 8;
        public float PopupLifeSeconds = 1.2f;
        public float PopupFadeSeconds = 0.5f;       // alpha ramps down over the last this-many seconds
        public float PopupRiseSpeed = 40f;          // world units per second
        public float PopupSpawnLift = 20f;          // spawns this far above the head
        public float PopupFontSize = 26f;           // world units, scaled by PopupKillScale / PopupValueScale
        public float PopupKillScale = 1.6f;
        public float PopupValueScale = 0.8f;
        public int PopupMinFoodValue = 2;           // pellets worth less than this give no "+N"

        // ---- Combo (pitch of the eat sound) ----
        public float ComboWindowSeconds = 1.5f;     // seconds without eating before the streak starts over; 0 = never (the reference)
        public int ComboMaxSteps = 24;
        public float EatMinIntervalSeconds = 0.05f;

        // ---- Sounds ----
        public int SampleRate = 44100;
        public float MasterVolume = 1f;
        public SoundCue EatCue = new SoundCue(Waveform.Sine, 420f, 0.06f, 0.05f);   // + EatPitchStep per combo step
        public float EatPitchStep = 28f;
        public SoundCue[] KillCues =
        {
            new SoundCue(Waveform.Square, 660f, 0.12f, 0.06f),
            new SoundCue(Waveform.Square, 990f, 0.2f, 0.06f, 0.11f),
        };
        public SoundCue[] DeathCues =
        {
            new SoundCue(Waveform.Sawtooth, 140f, 0.45f, 0.08f),
        };
        public SoundCue[] EndCardCues =
        {
            new SoundCue(Waveform.Triangle, 523f, 0.15f, 0.07f),
            new SoundCue(Waveform.Triangle, 784f, 0.3f, 0.07f, 0.14f),
        };
    }
}
