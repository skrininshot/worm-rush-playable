using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>The tutorial finger's loop: lands, drags one circle, lifts, rests, repeats.</summary>
    public class TutorialGestureTests
    {
        static TutorialGesture NewGesture(UiJuiceConfig config = null)
        {
            return new TutorialGesture(config ?? new UiJuiceConfig());
        }

        static HandPose At(TutorialGesture gesture, float time)
        {
            gesture.Sample(time, out HandPose pose);
            return pose;
        }

        [Test]
        public void LoopRepeatsEveryCycle()
        {
            TutorialGesture gesture = NewGesture();
            for (float t = 0f; t < gesture.CycleSeconds; t += 0.07f)
            {
                HandPose a = At(gesture, t);
                HandPose b = At(gesture, t + gesture.CycleSeconds * 3f);
                Assert.That(b.X, Is.EqualTo(a.X).Within(1e-2f));
                Assert.That(b.Y, Is.EqualTo(a.Y).Within(1e-2f));
                Assert.That(b.Alpha, Is.EqualTo(a.Alpha).Within(1e-3f));
            }
        }

        [Test]
        public void FingerStaysOnTheCircleAtTheConfiguredRadius()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);
            for (float t = 0f; t < gesture.CycleSeconds; t += 0.05f)
            {
                HandPose pose = At(gesture, t);
                float radius = MathF.Sqrt(pose.X * pose.X + pose.Y * pose.Y);
                Assert.That(radius, Is.EqualTo(config.TutorialRadius).Within(1e-2f), "t=" + t);
            }
        }

        [Test]
        public void ItLandsAtTheStartPointThenGoesAllTheWayRound()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);
            float dragStart = config.TutorialLandSeconds + config.TutorialHoldSeconds;

            HandPose landed = At(gesture, dragStart);
            Assert.That(landed.X, Is.EqualTo(-config.TutorialRadius).Within(1e-2f), "starts on the left");
            Assert.That(landed.Y, Is.EqualTo(0f).Within(1e-2f));

            // Halfway round the circle is the opposite side; clockwise from the left goes over the top first.
            HandPose quarter = At(gesture, dragStart + config.TutorialDragSeconds * 0.25f);
            Assert.That(quarter.Y, Is.GreaterThan(0f), "clockwise from the left goes up first");

            HandPose half = At(gesture, dragStart + config.TutorialDragSeconds * 0.5f);
            Assert.That(half.X, Is.EqualTo(config.TutorialRadius).Within(1e-2f));

            HandPose done = At(gesture, dragStart + config.TutorialDragSeconds);
            Assert.That(done.X, Is.EqualTo(-config.TutorialRadius).Within(1e-2f), "a full circle closes on its start");
        }

        [Test]
        public void ItIsPressedWhileDraggingAndUpAndGoneBeforeTheNextRound()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);
            float dragMid = config.TutorialLandSeconds + config.TutorialHoldSeconds + config.TutorialDragSeconds * 0.5f;

            HandPose dragging = At(gesture, dragMid);
            Assert.That(dragging.Press, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(dragging.Alpha, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(dragging.Scale, Is.EqualTo(config.TutorialPressScale).Within(1e-3f));

            HandPose resting = At(gesture, gesture.CycleSeconds - 1e-3f);
            Assert.That(resting.Press, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(resting.Alpha, Is.EqualTo(0f).Within(1e-2f));

            HandPose arriving = At(gesture, 0f);
            Assert.That(arriving.Alpha, Is.EqualTo(0f).Within(1e-3f), "fades in from nothing, so the loop has no pop");
        }

        [Test]
        public void ItPopsBackUpWhenItLetsGo()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);
            float dragEnd = config.TutorialLandSeconds + config.TutorialHoldSeconds + config.TutorialDragSeconds;

            float peak = 0f;
            for (float t = dragEnd; t <= dragEnd + config.TutorialLiftSeconds; t += 0.01f)
                peak = Math.Max(peak, At(gesture, t).Scale);

            Assert.That(peak, Is.GreaterThan(1f), "overshoots the resting size as it lifts");
        }

        [Test]
        public void TrailFollowsTheFingertipAndOnlyExistsWhileDragging()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);
            float dragStart = config.TutorialLandSeconds + config.TutorialHoldSeconds;

            Assert.That(gesture.TrailPoint(0.1f, 0, out _, out _, out _), Is.False, "nothing before the drag");
            Assert.That(gesture.TrailPoint(dragStart, gesture.TrailDots, out _, out _, out _), Is.False, "index past the end");

            float mid = dragStart + config.TutorialDragSeconds * 0.5f;
            HandPose pose = At(gesture, mid);
            Assert.That(gesture.TrailPoint(mid, 0, out float x, out float y, out float newest), Is.True);
            Assert.That(x, Is.EqualTo(pose.X).Within(1e-2f));
            Assert.That(y, Is.EqualTo(pose.Y).Within(1e-2f));
            Assert.That(newest, Is.EqualTo(1f).Within(1e-5f));

            // Older dots are dimmer, and every dot stays on the circle.
            float previous = newest;
            for (int i = 1; i < gesture.TrailDots; i++)
            {
                Assert.That(gesture.TrailPoint(mid, i, out x, out y, out float strength), Is.True);
                Assert.That(strength, Is.LessThan(previous));
                Assert.That(MathF.Sqrt(x * x + y * y), Is.EqualTo(config.TutorialRadius).Within(1e-2f));
                previous = strength;
            }
        }

        [Test]
        public void TrailFadesOutAsTheFingerLifts()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);
            float dragEnd = config.TutorialLandSeconds + config.TutorialHoldSeconds + config.TutorialDragSeconds;

            Assert.That(At(gesture, dragEnd - 0.1f).TrailFade, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(At(gesture, dragEnd + config.TutorialLiftSeconds).TrailFade, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void RippleRunsOnceOnLanding()
        {
            UiJuiceConfig config = new UiJuiceConfig();
            TutorialGesture gesture = NewGesture(config);

            int frames = 0;
            float last = -1f;
            for (float t = 0f; t < gesture.CycleSeconds; t += 0.01f)
            {
                float ring = At(gesture, t).Ring;
                if (ring < 0f) continue;
                Assert.That(ring, Is.InRange(0f, 1.0001f));
                Assert.That(ring, Is.GreaterThanOrEqualTo(last));
                last = ring;
                frames++;
            }

            Assert.That(frames, Is.GreaterThan(10));
            Assert.That(At(gesture, config.TutorialLandSeconds + config.TutorialHoldSeconds + 1f).Ring, Is.EqualTo(-1f));
        }

        [Test]
        public void ZeroLengthPhasesDoNotBreakIt()
        {
            UiJuiceConfig config = new UiJuiceConfig
            {
                TutorialLandSeconds = 0f,
                TutorialHoldSeconds = 0f,
                TutorialLiftSeconds = 0f,
                TutorialRestSeconds = 0f,
                TutorialRingSeconds = 0f,
                TutorialTrailDots = 0,
            };
            TutorialGesture gesture = NewGesture(config);
            for (float t = 0f; t < 5f; t += 0.1f)
            {
                HandPose pose = At(gesture, t);
                Assert.That(float.IsNaN(pose.X) || float.IsNaN(pose.Y) || float.IsNaN(pose.Alpha) || float.IsNaN(pose.Scale), Is.False);
            }
        }
    }
}
