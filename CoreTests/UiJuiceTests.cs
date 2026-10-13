using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>Springs, easing, the confetti pool and what presses, game events and the end card do to the UI.</summary>
    public class UiJuiceTests
    {
        const float Dt = 1f / 60f;

        static UiJuice NewUi(UiJuiceConfig config = null)
        {
            UiJuice ui = new UiJuice(config ?? new UiJuiceConfig(), 5);
            ui.Cta = new UiAnchor { X = 0f, Y = -380f, HalfWidth = 70f, HalfHeight = 23f };
            ui.EndButton = new UiAnchor { X = 0f, Y = -63f, HalfWidth = 90f, HalfHeight = 31f };
            ui.Title = new UiAnchor { X = 0f, Y = 67f, HalfWidth = 150f, HalfHeight = 30f };
            return ui;
        }

        static void Run(UiJuice ui, float seconds, bool ctaVisible = true)
        {
            for (float t = 0f; t < seconds; t += Dt) ui.Step(Dt, ctaVisible);
        }

        // ---- easing and spring ----

        [Test]
        public void EasingsStartAtZeroAndEndAtOne()
        {
            Assert.That(UiMotion.EaseOut(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(UiMotion.EaseOut(1f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(UiMotion.EaseOutBack(0f, 2f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(UiMotion.EaseOutBack(1f, 2f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(UiMotion.ElasticOut(0f), Is.EqualTo(0f));
            Assert.That(UiMotion.ElasticOut(1f), Is.EqualTo(1f));
        }

        [Test]
        public void BackAndElasticOvershootBeforeSettling()
        {
            float backPeak = 0f;
            float elasticPeak = 0f;
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                backPeak = Math.Max(backPeak, UiMotion.EaseOutBack(t, 2.4f));
                elasticPeak = Math.Max(elasticPeak, UiMotion.ElasticOut(t));
            }

            Assert.That(backPeak, Is.GreaterThan(1.1f));
            Assert.That(elasticPeak, Is.GreaterThan(1.05f));
        }

        [Test]
        public void HeartbeatBeatsTwiceAndRests()
        {
            const float period = 1f;
            const float amount = 0.1f;
            int peaks = 0;
            float prev = 0f;
            float prevPrev = 0f;
            float max = 0f;
            for (float t = 0f; t < period; t += 0.005f)
            {
                float v = UiMotion.Heartbeat(t, period, amount);
                if (prev > prevPrev && prev > v && prev > amount * 0.3f) peaks++;
                max = Math.Max(max, v);
                prevPrev = prev;
                prev = v;
            }

            Assert.That(peaks, Is.EqualTo(2));
            Assert.That(max, Is.LessThanOrEqualTo(amount + 1e-5f));
            Assert.That(UiMotion.Heartbeat(0.7f, period, amount), Is.EqualTo(0f), "the rest between beats");
            Assert.That(UiMotion.Heartbeat(0.5f, 0f, amount), Is.EqualTo(0f), "no period, no pulse");
        }

        [Test]
        public void SpringRingsThenSettlesOnItsTarget()
        {
            Spring spring = new Spring { Value = -1f };
            bool crossedZero = false;
            for (int i = 0; i < 600; i++)
            {
                spring.Step(Dt, 420f, 14f);
                if (spring.Value > 0.05f) crossedZero = true;
            }

            Assert.That(crossedZero, Is.True, "a jelly overshoots");
            Assert.That(spring.Value, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(spring.Velocity, Is.EqualTo(0f).Within(1e-2f));
        }

        [Test]
        public void SpringStaysStableOnALongFrame()
        {
            Spring spring = new Spring { Value = 1f };
            for (int i = 0; i < 100; i++) spring.Step(0.1f, 420f, 14f);
            Assert.That(Math.Abs(spring.Value), Is.LessThan(1f));
        }

        // ---- pool ----

        [Test]
        public void PoolMovesPiecesAndRemovesTheExpired()
        {
            UiFxPool pool = new UiFxPool(4);
            pool.Add(new UiPiece { Kind = UiFxKind.Confetti, VX = 100f, Life = 0.5f, MaxLife = 0.5f, Gravity = 0f });
            pool.Add(new UiPiece { Kind = UiFxKind.Star, Life = 0.1f, MaxLife = 0.1f });

            pool.Step(0.2f);
            Assert.That(pool.Count, Is.EqualTo(1));
            Assert.That(pool.Items[0].X, Is.GreaterThan(0f));

            pool.Step(0.4f);
            Assert.That(pool.Count, Is.EqualTo(0));
        }

        [Test]
        public void PoolDropsPiecesPastCapacity()
        {
            UiFxPool pool = new UiFxPool(2);
            UiPiece piece = new UiPiece { Life = 1f, MaxLife = 1f };
            Assert.That(pool.Add(piece), Is.True);
            Assert.That(pool.Add(piece), Is.True);
            Assert.That(pool.Add(piece), Is.False);
            Assert.That(pool.Count, Is.EqualTo(2));
        }

        [Test]
        public void GravityPullsPiecesDown()
        {
            UiFxPool pool = new UiFxPool(1);
            pool.Add(new UiPiece { Gravity = 900f, Life = 2f, MaxLife = 2f });
            pool.Step(0.5f);
            Assert.That(pool.Items[0].VY, Is.LessThan(0f));
            Assert.That(pool.Items[0].Y, Is.LessThan(0f));
        }

        [Test]
        public void LooksFadeAndShrinkOverLife()
        {
            UiPiece ring = new UiPiece { Kind = UiFxKind.Ring, Size = 50f, Grow = 4f, Life = 1f, MaxLife = 1f };
            UiFxPool.Look(ring, out float w0, out _, out float a0);
            ring.Life = 0.01f;
            UiFxPool.Look(ring, out float w1, out _, out float a1);
            Assert.That(w1, Is.GreaterThan(w0 * 3f));
            Assert.That(a1, Is.LessThan(a0 * 0.1f));

            UiPiece star = new UiPiece { Kind = UiFxKind.Star, Size = 20f, Life = 0.5f, MaxLife = 1f };
            UiFxPool.Look(star, out float mid, out _, out _);
            star.Life = 0.99f;
            UiFxPool.Look(star, out float start, out _, out _);
            Assert.That(mid, Is.EqualTo(20f).Within(0.01f), "a star is biggest halfway");
            Assert.That(start, Is.LessThan(2f));
        }

        // ---- reactions ----

        [Test]
        public void PressSquashesTheButtonBurstsAndPlaysTheTap()
        {
            UiJuice ui = NewUi();
            ui.Press(10f, 20f, UiTarget.Cta);

            ui.CtaPose(out ButtonPose pose);
            Assert.That(pose.ScaleX, Is.GreaterThan(1.1f), "squashed flat: wider");
            Assert.That(pose.ScaleY, Is.LessThan(0.9f), "... and shorter");
            Assert.That(ui.Fx.Count, Is.GreaterThan(10));
            Assert.That(ui.Sounds.Count, Is.EqualTo(1));
            Assert.That(ui.Sounds[0].Id, Is.EqualTo(SoundId.UiTap));

            Run(ui, 0.12f);
            ui.CtaPose(out pose);
            Assert.That(pose.ScaleY, Is.GreaterThan(1f), "it springs past rest and stretches tall");
        }

        [Test]
        public void ButtonSettlesBackToItsIdleSize()
        {
            UiJuice ui = NewUi();
            ui.Press(0f, 0f, UiTarget.Cta);
            Run(ui, 3f);

            ui.CtaPose(out ButtonPose pose);
            Assert.That(pose.ScaleX, Is.InRange(0.98f, 1.1f));
            Assert.That(pose.ScaleY, Is.InRange(0.98f, 1.1f));
        }

        [Test]
        public void EatingAndKillingKickTheHudHarderForAKill()
        {
            UiJuice eat = NewUi();
            eat.React(SoundId.Eat, 1);
            Run(eat, 0.05f);
            UiJuice kill = NewUi();
            kill.React(SoundId.Kill, 0);
            Run(kill, 0.05f);

            eat.CtaPose(out ButtonPose eatPose);
            kill.CtaPose(out ButtonPose killPose);
            Assert.That(killPose.ScaleY - 1f, Is.GreaterThan((eatPose.ScaleY - 1f) * 1.5f));
            Assert.That(kill.CounterScale, Is.GreaterThan(1f));
            Assert.That(kill.Fx.Count, Is.GreaterThan(0), "a kill throws confetti out of the CTA");
        }

        [Test]
        public void ComboStreaksThrowStarsEveryFifthPellet()
        {
            UiJuice ui = NewUi();
            for (int step = 1; step <= 4; step++) ui.React(SoundId.Eat, step);
            Assert.That(ui.Fx.Count, Is.EqualTo(0));

            ui.React(SoundId.Eat, 5);
            Assert.That(ui.Fx.Count, Is.GreaterThan(0));
        }

        [Test]
        public void DeathSquashesTheCounter()
        {
            UiJuice ui = NewUi();
            ui.React(SoundId.Death, 0);
            Assert.That(ui.CounterScale, Is.LessThan(1f));
        }

        [Test]
        public void IdleCtaHopsAndThrowsSparksOnItsOwn()
        {
            UiJuice ui = NewUi();
            Run(ui, 4f);

            bool sawParticles = false;
            UiJuice again = NewUi();
            for (float t = 0f; t < 4f; t += Dt)
            {
                again.Step(Dt, true);
                if (again.Fx.Count > 0) sawParticles = true;
            }

            Assert.That(sawParticles, Is.True);
        }

        [Test]
        public void HiddenCtaStaysQuiet()
        {
            UiJuice ui = NewUi();
            Run(ui, 4f, false);
            Assert.That(ui.Fx.Count, Is.EqualTo(0));
        }

        // ---- end card ----

        [Test]
        public void EndCardOpensWithAFlashAndABlastOutOfTheTitle()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);

            Assert.That(ui.EndActive, Is.True);
            Assert.That(ui.FlashAlpha, Is.GreaterThan(0.5f));
            Assert.That(ui.Fx.Count, Is.GreaterThanOrEqualTo(new UiJuiceConfig().OpenConfettiWin));

            Run(ui, 0.6f);
            Assert.That(ui.FlashAlpha, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void LosingCardIsQuieterThanWinning()
        {
            UiJuice win = NewUi();
            win.BeginEndCard(true);
            UiJuice lose = NewUi();
            lose.BeginEndCard(false);

            Run(win, 0.3f);
            Run(lose, 0.3f);
            win.RaysPose(out _, out _, out float winAlpha);
            lose.RaysPose(out _, out _, out float loseAlpha);

            Assert.That(lose.Fx.Count, Is.LessThan(win.Fx.Count));
            Assert.That(loseAlpha, Is.LessThan(winAlpha));
        }

        [Test]
        public void LettersLandOneAfterAnother()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);
            Run(ui, 0.12f);

            ui.LetterPose(0, out LetterPose first);
            ui.LetterPose(5, out LetterPose sixth);
            Assert.That(first.Scale, Is.GreaterThan(0f));
            Assert.That(sixth.Scale, Is.EqualTo(0f), "not started yet");

            Run(ui, 1.5f);
            ui.LetterPose(5, out sixth);
            Assert.That(sixth.Scale, Is.EqualTo(1f).Within(0.12f), "landed (plus its idle wave)");
            Assert.That(sixth.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void LetterOvershootsOnTheWayIn()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);

            float peak = 0f;
            for (float t = 0f; t < 0.5f; t += Dt)
            {
                ui.Step(Dt, false);
                ui.LetterPose(0, out LetterPose pose);
                peak = Math.Max(peak, pose.Scale);
            }

            Assert.That(peak, Is.GreaterThan(1.1f));
        }

        [Test]
        public void ButtonIsAbsentThenSpringsInWithSparklesAndASound()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);
            ui.EndButtonPose(out ButtonPose pose);
            Assert.That(pose.ScaleX, Is.EqualTo(0f));
            Assert.That(pose.GlowAlpha, Is.EqualTo(0f));

            Run(ui, new UiJuiceConfig().ButtonDelaySeconds + 0.05f);
            Assert.That(ui.Sounds.Exists(s => s.Id == SoundId.UiPop), Is.True);

            Run(ui, 1.5f);
            ui.EndButtonPose(out pose);
            Assert.That(pose.ScaleX, Is.InRange(0.9f, 1.2f));
            Assert.That(pose.GlowAlpha, Is.GreaterThan(0f));
        }

        [Test]
        public void ShineSweepsAcrossThenRests()
        {
            UiJuice ui = NewUi();
            bool swept = false;
            bool rested = false;
            for (float t = 0f; t < 3f; t += Dt)
            {
                ui.Step(Dt, true);
                ui.CtaPose(out ButtonPose pose);
                if (pose.Shine >= 0f) swept = true;
                else rested = true;
                Assert.That(pose.Shine, Is.LessThanOrEqualTo(1f));
            }

            Assert.That(swept && rested, Is.True);
        }

        [Test]
        public void HoverEnlargesTheEndButtonOnceItIsThere()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);
            Run(ui, 3f);
            ui.EndButtonPose(out ButtonPose rest);

            ui.SetHover(true);
            Run(ui, 0.5f);
            ui.EndButtonPose(out ButtonPose hovered);
            Assert.That(hovered.ScaleX, Is.GreaterThan(rest.ScaleX * 1.03f).Or.GreaterThan(1.05f));
        }

        [Test]
        public void PressOnTheEndCardKicksTheEndButton()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);
            Run(ui, 2f);
            ui.Sounds.Clear();

            ui.Press(40f, -100f, UiTarget.EndButton);
            ui.EndButtonPose(out ButtonPose pose);
            Assert.That(pose.ScaleY, Is.LessThan(0.9f));
            Assert.That(ui.Sounds[0].Id, Is.EqualTo(SoundId.UiTap));
        }

        [Test]
        public void GameEventsAreIgnoredUnderTheEndCard()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);
            Run(ui, 4f);
            int before = ui.Fx.Count;

            ui.React(SoundId.Kill, 0);
            Assert.That(ui.Fx.Count, Is.EqualTo(before));
        }

        [Test]
        public void SameSeedSameSparkles()
        {
            UiJuice a = NewUi();
            UiJuice b = NewUi();
            a.BeginEndCard(true);
            b.BeginEndCard(true);
            Run(a, 1.5f);
            Run(b, 1.5f);

            Assert.That(a.Fx.Count, Is.EqualTo(b.Fx.Count));
            for (int i = 0; i < a.Fx.Count; i++)
            {
                Assert.That(a.Fx.Items[i].X, Is.EqualTo(b.Fx.Items[i].X));
                Assert.That(a.Fx.Items[i].Y, Is.EqualTo(b.Fx.Items[i].Y));
            }
        }

        [Test]
        public void EndingTheCardClearsEverything()
        {
            UiJuice ui = NewUi();
            ui.BeginEndCard(true);
            Run(ui, 1f);
            ui.EndEndCard();

            Assert.That(ui.EndActive, Is.False);
            Assert.That(ui.Fx.Count, Is.EqualTo(0));
        }

        [Test]
        public void HintPopsInThenPulses()
        {
            UiJuice ui = NewUi();
            Assert.That(ui.HintScale(0f, 0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(ui.HintScale(5f, 0f), Is.EqualTo(1f).Within(0.01f));

            float peak = 0f;
            for (float t = 0f; t < 0.35f; t += 0.01f) peak = Math.Max(peak, ui.HintScale(t, 0f));
            Assert.That(peak, Is.GreaterThan(1.1f));
        }
    }
}
