using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class ViewMathTests
    {
        const float Span = 480f;
        const float ZoomPerLength = 0.002f;

        [Test]
        public void PortraitShowsTheSpanAcrossTheWidth()
        {
            float aspect = 9f / 16f;
            float ortho = ViewMath.OrthoSize(aspect, Span, 0f, ZoomPerLength);
            float visibleWidth = ortho * 2f * aspect;
            Assert.That(visibleWidth, Is.EqualTo(Span).Within(0.01f));
        }

        [Test]
        public void LandscapeShowsTheSpanAcrossTheHeight()
        {
            float ortho = ViewMath.OrthoSize(16f / 9f, Span, 0f, ZoomPerLength);
            Assert.That(ortho * 2f, Is.EqualTo(Span).Within(0.01f));
        }

        [Test]
        public void LongerWormZoomsOut()
        {
            float shortWorm = ViewMath.OrthoSize(0.5625f, Span, 24f, ZoomPerLength);
            float longWorm = ViewMath.OrthoSize(0.5625f, Span, 240f, ZoomPerLength);
            Assert.That(longWorm, Is.GreaterThan(shortWorm));
            Assert.That(longWorm / shortWorm, Is.EqualTo((1f + 240f * ZoomPerLength) / (1f + 24f * ZoomPerLength)).Within(1e-4f));
        }

        [Test]
        public void ZeroAspectDoesNotDivideByZero()
        {
            Assert.That(float.IsFinite(ViewMath.OrthoSize(0f, Span, 0f, ZoomPerLength)), Is.True);
        }
    }

    public class SteeringTests
    {
        [Test]
        public void HeadingPointsFromCentreToPointer()
        {
            Assert.That(Steering.TryGetHeading(10f, 0f, 2f, out float right), Is.True);
            Assert.That(right, Is.EqualTo(0f).Within(1e-5f));

            Assert.That(Steering.TryGetHeading(0f, 10f, 2f, out float up), Is.True);
            Assert.That(up, Is.EqualTo(MathF.PI / 2f).Within(1e-5f));

            Assert.That(Steering.TryGetHeading(-10f, 0f, 2f, out float left), Is.True);
            Assert.That(MathF.Abs(left), Is.EqualTo(MathF.PI).Within(1e-5f));
        }

        [Test]
        public void PointerInsideTheDeadZoneKeepsTheHeading()
        {
            Assert.That(Steering.TryGetHeading(1f, 1f, 4f, out _), Is.False);
            Assert.That(Steering.TryGetHeading(0f, 0f, 0f, out _), Is.False);
        }
    }
}
