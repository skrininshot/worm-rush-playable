using System;

namespace WormRush.Core
{
    /// <summary>
    /// Tunables of the screen-space UI's juice: how the two buttons breathe, squash and shine, how the end card
    /// builds up, what bursts out of a tap. Lengths are UI units (the 390 x 844 reference px), times are seconds.
    /// Plain C# like JuiceConfig; PlayableConfig embeds one.
    /// </summary>
    [Serializable]
    public sealed class UiJuiceConfig
    {
        // ---- Springs (punch = squash and stretch of a button or the counter) ----
        public float SpringStiffness = 420f;
        public float SpringDamping = 14f;
        public float PunchLimit = 1.3f;            // |punch| never goes past this, however fast the events come
        public float PressPunch = -1f;             // punch a press snaps to (negative = squashed flat), then springs back
        public float SquashX = 0.16f;              // scale X = 1 - punch * this
        public float SquashY = 0.22f;              // scale Y = 1 + punch * this
        public float HoverScale = 1.07f;           // mouse over the end button
        public float HoverStiffness = 260f;
        public float HoverDamping = 20f;

        // ---- Idle life of both buttons ----
        public float HeartbeatSeconds = 1.25f;     // two quick beats, then a rest
        public float HeartbeatAmount = 0.07f;
        public float WobbleDegrees = 1.8f;
        public float WobbleSpeed = 2.3f;           // rad/s
        public float ShinePeriodSeconds = 2f;      // a white beam sweeps across the face once per period
        public float ShineSweepFraction = 0.4f;    // part of the period the beam is moving
        public float GlowBase = 0.5f;              // halo behind the button: alpha = base + pulse * sin + punch boost
        public float GlowPulse = 0.25f;
        public float GlowSpeed = 4f;
        public float GlowPunchBoost = 0.35f;

        // ---- HUD reactions to the game ----
        public float EatKick = 6f;                 // spring velocity added to the CTA and the counter per pellet
        public float KillKick = 14f;
        public float CounterKick = 7f;
        public int ComboStarEvery = 5;             // a ring and stars at the CTA every this many pellets in a streak (0 = off)
        public float CtaHopSeconds = 3f;           // the CTA jumps and throws sparks every this often to be noticed
        public float CtaHopKick = 12f;
        public float CtaSparklesPerSecond = 2f;
        public float HintPopSeconds = 0.35f;       // "Block their path!" pops in, then pulses
        public float HintOvershoot = 2.2f;
        public float HintPulseAmount = 0.05f;
        public float HintPulseSpeed = 7f;          // rad/s

        // ---- Tutorial finger: lands, drags once round a circle, lifts, rests, repeats ----
        public float TutorialRadius = 58f;
        public float TutorialStartDegrees = 180f;  // where the circle starts (0 = right, 90 = up); it runs clockwise
        public float TutorialLandSeconds = 0.35f;  // the finger comes down: fades in, shrinks onto the glass
        public float TutorialHoldSeconds = 0.2f;   // pressed, not moving yet
        public float TutorialDragSeconds = 1.7f;   // one full circle, eased at both ends
        public float TutorialLiftSeconds = 0.3f;   // lets go: pops back up, the trail fades
        public float TutorialRestSeconds = 0.55f;  // gone before the next round
        public float TutorialPressScale = 0.88f;   // the hand while pressed (1 = off the glass)
        public float TutorialLandScale = 1.18f;    // the hand as it arrives from above
        public float TutorialRingSeconds = 0.55f;  // ripple under the fingertip on landing
        public float TutorialRingSize = 70f;
        public int TutorialTrailDots = 9;
        public float TutorialTrailSpacingSeconds = 0.045f;

        // ---- End card timeline (seconds after the card appears) ----
        public float DimSeconds = 0.25f;
        public float FlashAlpha = 0.65f;           // white flash on the first frame, fading out
        public float FlashSeconds = 0.35f;
        public float RaysAlpha = 0.2f;             // sunburst behind the title
        public float RaysSize = 560f;
        public float RaysSpinDegrees = 16f;        // per second
        public float RaysGrowSeconds = 0.7f;
        public float RaysLoseFactor = 0.45f;       // SO CLOSE keeps the rays but quieter
        public float LetterDelaySeconds = 0.06f;   // between one letter of the title and the next
        public float LetterSeconds = 0.5f;
        public float LetterOvershoot = 2.4f;
        public float LetterDrop = 60f;             // letters fall in from this far above
        public float LetterTiltDegrees = 30f;
        public float TitleWaveAmplitude = 3.5f;    // once landed, the title keeps rippling
        public float TitleWaveSpeed = 5.5f;        // rad/s
        public float TitleWavePhase = 0.6f;        // rad between neighbouring letters
        public float TitleWaveScale = 0.05f;
        public float SubtitleDelaySeconds = 0.6f;
        public float SubtitleSeconds = 0.4f;
        public float SubtitleRise = 18f;
        public float ButtonDelaySeconds = 0.75f;   // the button springs in after the title has landed
        public float ButtonPopSeconds = 0.7f;
        public float EndHopSeconds = 2.4f;         // the button jumps and throws sparks every this often
        public float EndHopKick = 15f;
        public float EndSparklesPerSecond = 9f;

        // ---- Particles ----
        public int FxCapacity = 160;
        public int ColorSlots = 6;                 // confetti picks one of this many palette colours (PlayableConfig keeps it equal to the palette)
        public int OpenConfettiWin = 64;           // blast out of the title when the card appears
        public int OpenConfettiLose = 18;
        public int PopStars = 12;                  // when the button springs in
        public int HopStars = 6;
        public int TapConfetti = 22;               // out of the finger on every press
        public int TapStars = 7;
        public float ConfettiSpeedMin = 220f;
        public float ConfettiSpeedMax = 620f;
        public float ConfettiGravity = 950f;
        public float ConfettiDrag = 1.7f;
        public float ConfettiSizeMin = 7f;
        public float ConfettiSizeMax = 12f;
        public float ConfettiLifeMin = 1.5f;
        public float ConfettiLifeMax = 2.4f;
        public float ConfettiSpinDegrees = 540f;
        public float ConfettiFlipMin = 7f;         // rad/s
        public float ConfettiFlipMax = 14f;
        public float TapSpeedMin = 160f;
        public float TapSpeedMax = 460f;
        public float StarSizeMin = 12f;
        public float StarSizeMax = 26f;
        public float StarLifeMin = 0.45f;
        public float StarLifeMax = 0.85f;
        public float StarRise = 40f;               // sparkles drift up at up to this speed
        public float RingSize = 60f;
        public float RingGrow = 4.5f;              // final size = RingSize * this
        public float RingLife = 0.5f;

        // ---- Sounds (the synthesized blips of JuiceConfig, same SoundCue shape) ----
        public SoundCue[] TapCues =
        {
            new SoundCue(Waveform.Square, 784f, 0.07f, 0.05f),
            new SoundCue(Waveform.Square, 1175f, 0.12f, 0.05f, 0.05f),
            new SoundCue(Waveform.Triangle, 1568f, 0.22f, 0.06f, 0.1f),
        };
        public SoundCue[] PopCues =
        {
            new SoundCue(Waveform.Sine, 392f, 0.1f, 0.08f),
            new SoundCue(Waveform.Sine, 784f, 0.18f, 0.07f, 0.06f),
        };
    }
}
