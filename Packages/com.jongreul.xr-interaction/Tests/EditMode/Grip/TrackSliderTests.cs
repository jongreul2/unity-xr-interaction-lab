using System;
using Jongreul.XrInteraction.Grip;
using NUnit.Framework;

namespace Jongreul.XrInteraction.Tests.Grip
{
    public class TrackSliderTests
    {
        const float Length = 0.3f;

        [Test]
        public void Center_IsHalf_AndBeyondEndsClamps()
        {
            Assert.That(TrackSlider.ToNormalized(0f, Length), Is.EqualTo(0.5f));
            Assert.That(TrackSlider.ToNormalized(-1f, Length), Is.EqualTo(0f));
            Assert.That(TrackSlider.ToNormalized(1f, Length), Is.EqualTo(1f));
        }

        [Test]
        public void LocalX_RoundTrips()
        {
            foreach (float t in new[] { 0f, 0.25f, 0.5f, 0.8f, 1f })
                Assert.That(TrackSlider.ToNormalized(TrackSlider.ToLocalX(t, Length), Length), Is.EqualTo(t).Within(1e-6f));
        }

        [Test]
        public void Value_RoundTrips_AndOutOfRangeClamps()
        {
            float t = TrackSlider.FromValue(45f, -180f, 180f);

            Assert.That(TrackSlider.ToValue(t, -180f, 180f), Is.EqualTo(45f).Within(1e-4f));
            Assert.That(TrackSlider.FromValue(500f, -180f, 180f), Is.EqualTo(1f));
            Assert.That(TrackSlider.FromValue(-0.5f, -0.1f, 0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void Snap_RoundsToStep_ZeroStepKeepsValue()
        {
            Assert.That(TrackSlider.Snap(0.0312f, 0.002f), Is.EqualTo(0.032f).Within(1e-6f));
            Assert.That(TrackSlider.Snap(44.6f, 1f), Is.EqualTo(45f));
            Assert.That(TrackSlider.Snap(0.0312f, 0f), Is.EqualTo(0.0312f));
        }

        [Test]
        public void NonPositiveLength_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TrackSlider.ToNormalized(0f, 0f));
        }
    }
}
