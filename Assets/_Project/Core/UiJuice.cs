using System;
using System.Collections.Generic;

namespace WormRush.Core
{
    /// <summary>Which button a press landed on.</summary>
    public enum UiTarget : byte
    {
        /// <summary>The persistent "PLAY NOW" at the bottom of the screen.</summary>
        Cta,

        /// <summary>The big button of the end card.</summary>
        EndButton,
    }

    /// <summary>Where something sits on screen, in UI units from the centre (y up), with its half size.</summary>
    public struct UiAnchor
    {
        public float X;
        public float Y;
        public float HalfWidth;
        public float HalfHeight;
    }

    /// <summary>How a button is drawn this frame.</summary>
    public struct ButtonPose
    {
        public float ScaleX;
        public float ScaleY;
        public float Rotation;     // degrees
        public float GlowAlpha;    // 0..1, the halo behind it
        public float Shine;        // 0..1 across the face while the beam sweeps, -1 while it rests
    }

    /// <summary>How one letter of the end card title is drawn this frame.</summary>
    public struct LetterPose
    {
        public float Scale;
        public float Rotation;     // degrees
        public float Y;            // offset from its resting place
        public float Alpha;
        public float Gold;         // 0..1, how far it is tinted toward gold (a highlight running along the title)
    }

    /// <summary>
    /// The life of the screen-space UI, with no drawing: springs that squash and stretch the buttons, the idle
    /// heartbeat and shine, the end card's timeline (flash, rays, letters, subtitle, button spring-in), the
    /// confetti pool and the sounds. The View puts <see cref="Cta"/>, <see cref="EndButton"/> and <see cref="Title"/>
    /// where it laid them out, calls <see cref="Step"/> once per frame and draws what the pose methods and
    /// <see cref="Fx"/> say.
    ///
    /// Reactions: a press on a button squashes it flat and bursts confetti, stars and a ring out of the finger;
    /// eating and killing kick the HUD; the end card opens with a flash, a blast out of the title and the button
    /// springing in with sparkles, then keeps hopping and throwing sparks so it never sits still.
    /// </summary>
    public sealed class UiJuice
    {
        const float Tau = MathF.PI * 2f;
        const float FullTurnDegrees = 360f;
        const float StarSpreadX = 1.15f;     // sparkles land this far past the button's half width
        const float StarSpreadY = 1.7f;      // ... and half height
        const float StarSpinDegrees = 120f;
        const float TapStarRadius = 36f;
        const float StarVxFraction = 0.3f;
        const float WinTitleStars = 10f;
        const float LetterSettleSeconds = 0.3f;   // the idle wave fades in over this long after a letter lands
        const float GoldPower = 4f;
        const float BallisticBias = 0.35f;        // fraction of confetti speed added upward so a cloud rises before it falls

        readonly UiJuiceConfig _c;
        readonly SimRandom _rng;

        Spring _cta;
        Spring _end;
        Spring _counter;
        Spring _hover;

        float _time;
        bool _endActive;
        bool _won;
        bool _buttonPopped;
        float _endElapsed;
        float _endHopAt;
        float _ctaHopAt;
        float _endSparkleCarry;
        float _ctaSparkleCarry;

        public UiAnchor Cta;
        public UiAnchor EndButton;
        public UiAnchor Title;

        public readonly UiFxPool Fx;

        /// <summary>Sounds the UI wants. The caller plays and clears them.</summary>
        public readonly List<SoundRequest> Sounds = new List<SoundRequest>(4);

        public UiJuice(UiJuiceConfig config, int seed)
        {
            _c = config;
            _rng = new SimRandom(seed);
            Fx = new UiFxPool(config.FxCapacity);
            _hover.Value = 1f;
            _hover.Target = 1f;
            _ctaHopAt = config.CtaHopSeconds;
        }

        public bool EndActive
        {
            get { return _endActive; }
        }

        /// <summary>Seconds since the end card appeared (0 while it is not up).</summary>
        public float EndElapsed
        {
            get { return _endElapsed; }
        }

        /// <summary>The length counter's scale: a springy bump on each pellet.</summary>
        public float CounterScale
        {
            get { return 1f + _counter.Value * _c.SquashY; }
        }

        /// <summary>0..1, how lit up the counter is (it flashes gold on a bump).</summary>
        public float CounterGlow
        {
            get { return UiMotion.Clamp01(_counter.Value); }
        }

        // ---------------- time ----------------

        /// <summary>Advances springs, the end card timeline, idle sparkles and the pieces by one frame.</summary>
        public void Step(float dt, bool ctaVisible)
        {
            if (dt <= 0f) return;
            _time += dt;

            StepPunch(ref _cta, dt);
            StepPunch(ref _end, dt);
            StepPunch(ref _counter, dt);
            _hover.Step(dt, _c.HoverStiffness, _c.HoverDamping);

            if (_endActive)
            {
                _endElapsed += dt;
                if (!_buttonPopped && _endElapsed >= _c.ButtonDelaySeconds) PopEndButton();
                if (_buttonPopped) EndIdle(dt);
            }
            else if (ctaVisible)
            {
                CtaIdle(dt);
            }

            Fx.Step(dt);
        }

        void StepPunch(ref Spring spring, float dt)
        {
            spring.Step(dt, _c.SpringStiffness, _c.SpringDamping);
            spring.Limit(_c.PunchLimit);
        }

        // ---------------- end card ----------------

        /// <summary>The end card has just appeared: set the anchors first, then call this.</summary>
        public void BeginEndCard(bool won)
        {
            _endActive = true;
            _won = won;
            _endElapsed = 0f;
            _buttonPopped = false;
            _end = default;
            _hover.Target = 1f;
            _endSparkleCarry = 0f;

            Confetti(Title.X, Title.Y, won ? _c.OpenConfettiWin : _c.OpenConfettiLose, _c.ConfettiSpeedMin, _c.ConfettiSpeedMax);
            Ring(Title.X, Title.Y);
            if (won) Stars(Title, (int)WinTitleStars);
        }

        /// <summary>The end card went away (a new run).</summary>
        public void EndEndCard()
        {
            _endActive = false;
            _buttonPopped = false;
            _endElapsed = 0f;
            _hover.Target = 1f;
            Fx.Clear();
        }

        void PopEndButton()
        {
            _buttonPopped = true;
            _end.Velocity += _c.EndHopKick;
            Stars(EndButton, _c.PopStars);
            Ring(EndButton.X, EndButton.Y);
            Sounds.Add(new SoundRequest(SoundId.UiPop));
            _endHopAt = _endElapsed + _c.EndHopSeconds;
        }

        void EndIdle(float dt)
        {
            _endSparkleCarry += dt * _c.EndSparklesPerSecond;
            while (_endSparkleCarry >= 1f)
            {
                _endSparkleCarry -= 1f;
                Stars(EndButton, 1);
            }

            if (_c.EndHopSeconds > 0f && _endElapsed >= _endHopAt)
            {
                _endHopAt = _endElapsed + _c.EndHopSeconds;
                _end.Velocity += _c.EndHopKick;
                Stars(EndButton, _c.HopStars);
                Ring(EndButton.X, EndButton.Y);
            }
        }

        void CtaIdle(float dt)
        {
            _ctaSparkleCarry += dt * _c.CtaSparklesPerSecond;
            while (_ctaSparkleCarry >= 1f)
            {
                _ctaSparkleCarry -= 1f;
                Stars(Cta, 1);
            }

            if (_c.CtaHopSeconds > 0f && _time >= _ctaHopAt)
            {
                _ctaHopAt = _time + _c.CtaHopSeconds;
                _cta.Velocity += _c.CtaHopKick;
                Stars(Cta, _c.HopStars);
            }
        }

        // ---------------- reactions ----------------

        /// <summary>The mouse is over the end button (desktop only; touch has no hover).</summary>
        public void SetHover(bool over)
        {
            _hover.Target = over && _endActive && _buttonPopped ? _c.HoverScale : 1f;
        }

        /// <summary>A press at (x, y) in UI units: squash the button, burst out of the finger, play the tap.</summary>
        public void Press(float x, float y, UiTarget target)
        {
            if (target == UiTarget.Cta) _cta = new Spring { Value = _c.PressPunch };
            else _end = new Spring { Value = _c.PressPunch };

            Confetti(x, y, _c.TapConfetti, _c.TapSpeedMin, _c.TapSpeedMax);
            StarsAround(x, y, _c.TapStars, TapStarRadius);
            Ring(x, y);
            Sounds.Add(new SoundRequest(SoundId.UiTap));
        }

        /// <summary>What the game did (the sound it asked for says it): the HUD bumps with it.</summary>
        public void React(SoundId id, int step)
        {
            if (_endActive) return;

            switch (id)
            {
                case SoundId.Eat:
                    _cta.Velocity += _c.EatKick;
                    _counter.Velocity += _c.EatKick;
                    if (_c.ComboStarEvery > 0 && step > 0 && step % _c.ComboStarEvery == 0)
                    {
                        Ring(Cta.X, Cta.Y);
                        Stars(Cta, _c.HopStars);
                    }
                    break;
                case SoundId.Kill:
                    _cta.Velocity += _c.KillKick;
                    _counter.Velocity += _c.CounterKick;
                    Ring(Cta.X, Cta.Y);
                    Stars(Cta, _c.PopStars);
                    Confetti(Cta.X, Cta.Y, _c.TapConfetti, _c.TapSpeedMin, _c.TapSpeedMax);
                    break;
                case SoundId.Death:
                    _counter = new Spring { Value = _c.PressPunch };
                    break;
            }
        }

        // ---------------- poses ----------------

        public void CtaPose(out ButtonPose pose)
        {
            MakePose(_cta.Value, 1f, 1f, _time, out pose);
        }

        /// <summary>The end button: not there before its delay, springs in, then lives like the CTA.</summary>
        public void EndButtonPose(out ButtonPose pose)
        {
            float t = _endElapsed - _c.ButtonDelaySeconds;
            float entry = _c.ButtonPopSeconds > 0f ? UiMotion.ElasticOut(t / _c.ButtonPopSeconds) : (t >= 0f ? 1f : 0f);
            MakePose(_end.Value, entry, _hover.Value, MathF.Max(t, 0f), out pose);
        }

        void MakePose(float punch, float entry, float hover, float time, out ButtonPose pose)
        {
            float s = entry * hover * (1f + UiMotion.Heartbeat(time, _c.HeartbeatSeconds, _c.HeartbeatAmount));
            float present = UiMotion.Clamp01(entry);
            float glow = _c.GlowBase + _c.GlowPulse * MathF.Sin(time * _c.GlowSpeed) + MathF.Max(0f, punch) * _c.GlowPunchBoost;

            pose.ScaleX = s * (1f - punch * _c.SquashX);
            pose.ScaleY = s * (1f + punch * _c.SquashY);
            pose.Rotation = MathF.Sin(time * _c.WobbleSpeed) * _c.WobbleDegrees * present;
            pose.GlowAlpha = UiMotion.Clamp01(glow) * present;
            pose.Shine = -1f;

            if (_c.ShinePeriodSeconds > 0f && _c.ShineSweepFraction > 0f)
            {
                float f = UiMotion.Repeat(time / _c.ShinePeriodSeconds);
                if (f < _c.ShineSweepFraction) pose.Shine = f / _c.ShineSweepFraction;
            }
        }

        /// <summary>The dark veil: 0 to 1 over DimSeconds.</summary>
        public float DimAmount
        {
            get { return _c.DimSeconds > 0f ? UiMotion.Clamp01(_endElapsed / _c.DimSeconds) : 1f; }
        }

        /// <summary>White flash over the whole card on its first frames.</summary>
        public float FlashAlpha
        {
            get
            {
                if (_c.FlashSeconds <= 0f) return 0f;
                float inv = 1f - UiMotion.Clamp01(_endElapsed / _c.FlashSeconds);
                return _c.FlashAlpha * inv * inv;
            }
        }

        /// <summary>The sunburst behind the title: grows with a bounce, turns, fades in.</summary>
        public void RaysPose(out float scale, out float rotation, out float alpha)
        {
            float grow = _c.RaysGrowSeconds > 0f ? _endElapsed / _c.RaysGrowSeconds : 1f;
            scale = UiMotion.EaseOutBack(grow, _c.LetterOvershoot);
            rotation = _endElapsed * _c.RaysSpinDegrees;
            alpha = _c.RaysAlpha * UiMotion.Clamp01(grow * 2f) * (_won ? 1f : _c.RaysLoseFactor);
        }

        /// <summary>
        /// Letter <paramref name="index"/> of the title: drops in from above, tilted, with an overshoot, one after
        /// the other; once landed it joins a wave along the title, and a gold highlight runs through it.
        /// </summary>
        public void LetterPose(int index, out LetterPose pose)
        {
            float local = _endElapsed - index * _c.LetterDelaySeconds;
            float t = _c.LetterSeconds > 0f ? local / _c.LetterSeconds : (local >= 0f ? 1f : 0f);
            if (t <= 0f)
            {
                pose = default;
                return;
            }

            float settle = 1f - UiMotion.EaseOut(t);
            float side = (index & 1) == 0 ? 1f : -1f;
            pose.Scale = UiMotion.EaseOutBack(t, _c.LetterOvershoot);
            pose.Rotation = side * _c.LetterTiltDegrees * settle;
            pose.Y = _c.LetterDrop * settle;
            pose.Alpha = UiMotion.Clamp01(t * 4f);
            pose.Gold = 0f;

            float landed = UiMotion.Clamp01((local - _c.LetterSeconds) / LetterSettleSeconds);
            if (landed > 0f)
            {
                float wave = MathF.Sin(_endElapsed * _c.TitleWaveSpeed - index * _c.TitleWavePhase);
                pose.Y += wave * _c.TitleWaveAmplitude * landed;
                pose.Scale *= 1f + wave * _c.TitleWaveScale * landed;
                pose.Gold = MathF.Pow(0.5f + 0.5f * wave, GoldPower) * landed;
            }
        }

        /// <summary>Subtitle: fades in and rises into place after the title.</summary>
        public void SubtitlePose(out float alpha, out float yOffset)
        {
            float t = _c.SubtitleSeconds > 0f ? (_endElapsed - _c.SubtitleDelaySeconds) / _c.SubtitleSeconds : 1f;
            alpha = UiMotion.Clamp01(t);
            yOffset = _c.SubtitleRise * (1f - UiMotion.EaseOut(t));
        }

        /// <summary>The "Block their path!" hint: pops in when it appears, then pulses.</summary>
        public float HintScale(float shownFor, float time)
        {
            float pop = _c.HintPopSeconds > 0f ? UiMotion.EaseOutBack(shownFor / _c.HintPopSeconds, _c.HintOvershoot) : 1f;
            float pulse = shownFor >= _c.HintPopSeconds ? 1f + _c.HintPulseAmount * MathF.Sin(time * _c.HintPulseSpeed) : 1f;
            return pop * pulse;
        }

        // ---------------- emitters ----------------

        void Confetti(float x, float y, int count, float speedMin, float speedMax)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = _rng.Range(0f, Tau);
                float speed = _rng.Range(speedMin, speedMax);
                UiPiece piece = new UiPiece
                {
                    Kind = UiFxKind.Confetti,
                    X = x,
                    Y = y,
                    VX = MathF.Cos(angle) * speed,
                    VY = MathF.Sin(angle) * speed + speed * BallisticBias,
                    Drag = _c.ConfettiDrag,
                    Gravity = _c.ConfettiGravity,
                    Rotation = _rng.Range(0f, FullTurnDegrees),
                    Spin = _rng.Range(-_c.ConfettiSpinDegrees, _c.ConfettiSpinDegrees),
                    Phase = _rng.Range(0f, Tau),
                    FlipSpeed = _rng.Range(_c.ConfettiFlipMin, _c.ConfettiFlipMax),
                    Size = _rng.Range(_c.ConfettiSizeMin, _c.ConfettiSizeMax),
                    ColorSlot = _rng.Next(Math.Max(1, _c.ColorSlots)),
                };
                piece.Life = piece.MaxLife = _rng.Range(_c.ConfettiLifeMin, _c.ConfettiLifeMax);
                if (!Fx.Add(piece)) return;
            }
        }

        void Stars(in UiAnchor around, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = around.X + _rng.Range(-1f, 1f) * around.HalfWidth * StarSpreadX;
                float y = around.Y + _rng.Range(-1f, 1f) * around.HalfHeight * StarSpreadY;
                if (!Fx.Add(NewStar(x, y))) return;
            }
        }

        void StarsAround(float x, float y, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = _rng.Range(0f, Tau);
                float r = _rng.Range(0f, radius);
                if (!Fx.Add(NewStar(x + MathF.Cos(angle) * r, y + MathF.Sin(angle) * r))) return;
            }
        }

        UiPiece NewStar(float x, float y)
        {
            UiPiece piece = new UiPiece
            {
                Kind = UiFxKind.Star,
                X = x,
                Y = y,
                VX = _rng.Range(-_c.StarRise, _c.StarRise) * StarVxFraction,
                VY = _rng.Range(0f, _c.StarRise),
                Rotation = _rng.Range(0f, FullTurnDegrees * 0.25f),
                Spin = _rng.Range(-StarSpinDegrees, StarSpinDegrees),
                Size = _rng.Range(_c.StarSizeMin, _c.StarSizeMax),
                ColorSlot = _rng.Next(2) == 0 ? UiFxPool.ColorWhite : UiFxPool.ColorGold,
            };
            piece.Life = piece.MaxLife = _rng.Range(_c.StarLifeMin, _c.StarLifeMax);
            return piece;
        }

        void Ring(float x, float y)
        {
            Fx.Add(new UiPiece
            {
                Kind = UiFxKind.Ring,
                X = x,
                Y = y,
                Size = _c.RingSize,
                Grow = _c.RingGrow,
                Life = _c.RingLife,
                MaxLife = _c.RingLife,
                ColorSlot = UiFxPool.ColorWhite,
            });
        }
    }
}
