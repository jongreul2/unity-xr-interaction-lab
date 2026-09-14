using System;
using Jongreul.XrInteraction.Strike;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

namespace Jongreul.XrInteraction.Tests.Strike
{
    /// <summary>합성 궤적(90 fps)으로 타격 판정을 검증한다. 타격면은 기본적으로 위를 향한 수평면.</summary>
    public class StrikeDetectorTests
    {
        const double Fps = 90;
        static readonly Vector3 Up = new Vector3(0, 1, 0);

        StrikeDetector _detector;
        double _t;

        [SetUp]
        public void SetUp()
        {
            _detector = new StrikeDetector();
            _t = 0;
        }

        void Hold(Vector3 at, double seconds)
        {
            if (_detector.SampleCount == 0)
                _detector.AddSample(_t, at);
            int steps = (int)Math.Round(seconds * Fps);
            for (int i = 0; i < steps; i++)
            {
                _t += 1.0 / Fps;
                _detector.AddSample(_t, at);
            }
        }

        void Move(Vector3 from, Vector3 to, double seconds)
        {
            if (_detector.SampleCount == 0)
                _detector.AddSample(_t, from);
            int steps = Math.Max(1, (int)Math.Round(seconds * Fps));
            for (int i = 1; i <= steps; i++)
            {
                _t += 1.0 / Fps;
                _detector.AddSample(_t, Vector3.Lerp(from, to, (float)i / steps));
            }
        }

        StrikeResult Hit(StrikerKind kind = StrikerKind.Hand) => _detector.Evaluate(_t, Up, kind);

        [Test]
        public void DownwardStroke_Hits()
        {
            Hold(new Vector3(0, 0.4f, 0), 0.1);
            Move(new Vector3(0, 0.4f, 0), Vector3.Zero, 0.15);

            StrikeResult result = Hit();

            Assert.That(result.Hit, Is.True, result.ToString());
            Assert.That(result.Speed, Is.EqualTo(0.4f / 0.1556f).Within(0.1f));
            Assert.That(result.StrokeLength, Is.EqualTo(0.4f).Within(0.01f));
        }

        [Test]
        public void SidewaysSwipe_IsWrongAngle()
        {
            Move(new Vector3(-0.4f, 0.03f, 0), Vector3.Zero, 0.15);

            StrikeResult result = Hit();

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Reason, Is.EqualTo(StrikeRejectReason.WrongAngle));
        }

        [Test]
        public void SlowPress_IsTooSlow()
        {
            Move(new Vector3(0, 0.2f, 0), Vector3.Zero, 0.3);

            Assert.That(Hit().Reason, Is.EqualTo(StrikeRejectReason.TooSlow));
        }

        [Test]
        public void WristSnap_FastButShort_IsStrokeTooShort()
        {
            Hold(new Vector3(0, 0.07f, 0), 0.2);
            Move(new Vector3(0, 0.07f, 0), Vector3.Zero, 0.0333);

            StrikeResult result = Hit();

            Assert.That(result.Speed, Is.GreaterThan(_detector.Settings.MinHandSpeed), "빠르긴 하다");
            Assert.That(result.Reason, Is.EqualTo(StrikeRejectReason.StrokeTooShort));
        }

        [Test]
        public void ToolThreshold_IsSeparateFromHand()
        {
            Move(new Vector3(0, 0.4f, 0), Vector3.Zero, 0.3); // 약 1.33 m/s

            Assert.That(Hit(StrikerKind.Hand).Reason, Is.EqualTo(StrikeRejectReason.TooSlow));
            Assert.That(Hit(StrikerKind.Tool).Hit, Is.True);
        }

        [Test]
        public void SecondHitWithinLockout_IsRejected_ThenAllowedAfter()
        {
            Move(new Vector3(0, 0.4f, 0), Vector3.Zero, 0.15);
            Assert.That(Hit().Hit, Is.True);

            Move(Vector3.Zero, new Vector3(0, 0.3f, 0), 0.05);
            Move(new Vector3(0, 0.3f, 0), Vector3.Zero, 0.05);
            Assert.That(Hit().Reason, Is.EqualTo(StrikeRejectReason.LockedOut));

            Move(Vector3.Zero, new Vector3(0, 0.4f, 0), 0.1);
            Move(new Vector3(0, 0.4f, 0), Vector3.Zero, 0.15);
            Assert.That(Hit().Hit, Is.True);
        }

        [Test]
        public void MovingAwayFromSurface_IsRejected()
        {
            Move(Vector3.Zero, new Vector3(0, 0.4f, 0), 0.15);

            Assert.That(Hit().Reason, Is.EqualTo(StrikeRejectReason.MovingAway));
        }

        [Test]
        public void RigMovingDown_WithStillHand_IsNotAHit_InRigSpace()
        {
            var rigLocal = new StrikeDetector();
            var worldSpace = new StrikeDetector();
            var handLocal = new Vector3(0, 1f, 0.3f);
            Quaternion rigRotation = Quaternion.CreateFromAxisAngle(Up, 0.7f);

            // 리그가 3 m/s로 내려간다(엘리베이터·탈것). 손은 리그에 대해 가만히 있다.
            for (int i = 0; i <= 20; i++)
            {
                double t = i / Fps;
                var rigPosition = new Vector3(0, 2f - 3f * (float)t, 0);
                Vector3 world = rigPosition + Vector3.Transform(handLocal, rigRotation);
                rigLocal.AddSample(t, RigSpace.ToLocalPoint(world, rigPosition, rigRotation));
                worldSpace.AddSample(t, world);
            }

            double now = 20 / Fps;
            Vector3 localNormal = RigSpace.ToLocalDirection(Up, rigRotation);
            Assert.That(rigLocal.Evaluate(now, localNormal, StrikerKind.Hand).Hit, Is.False);
            Assert.That(worldSpace.Evaluate(now, Up, StrikerKind.Hand).Hit, Is.True, "월드 좌표였다면 오탐");
        }

        [Test]
        public void RewoundSample_IsIgnored_AndHistoryKept()
        {
            Move(new Vector3(0, 0.4f, 0), new Vector3(0, 0.2f, 0), 0.075);
            _detector.AddSample(_t - 0.05, new Vector3(0, 5f, 0)); // 재시뮬레이션처럼 시간이 되돌아간 샘플
            Move(new Vector3(0, 0.2f, 0), Vector3.Zero, 0.075);

            Assert.That(_detector.RewoundSamplesIgnored, Is.EqualTo(1));
            Assert.That(Hit().Hit, Is.True);
        }

        [Test]
        public void TrackingGap_DiscardsHistory()
        {
            Move(new Vector3(0, 0.4f, 0), new Vector3(0, 0.1f, 0), 0.1);
            _t += 0.2;
            _detector.AddSample(_t, Vector3.Zero);

            Assert.That(Hit().Reason, Is.EqualTo(StrikeRejectReason.NotEnoughHistory));
        }

        [Test]
        public void TiltedSurface_StrokeAlongNormal_Hits()
        {
            Vector3 normal = Vector3.Normalize(new Vector3(1, 1, 0));
            Move(normal * 0.35f, Vector3.Zero, 0.15);

            Assert.That(_detector.Evaluate(_t, normal, StrikerKind.Hand).Hit, Is.True);
        }

        [TestCase(50f, true)]
        [TestCase(30f, false)]
        public void StraightDownOnTiltedSurface_DependsOnMaxAngle(float maxAngle, bool expectedHit)
        {
            _detector = new StrikeDetector(new StrikeSettings { MaxAngleDegrees = maxAngle });
            Vector3 normal = Vector3.Normalize(new Vector3(1, 1, 0)); // 수직 하강과 45°
            Move(new Vector3(0, 0.6f, 0), Vector3.Zero, 0.15);

            StrikeResult result = _detector.Evaluate(_t, normal, StrikerKind.Hand);

            Assert.That(result.AngleDegrees, Is.EqualTo(45f).Within(0.5f));
            Assert.That(result.Hit, Is.EqualTo(expectedHit), result.ToString());
        }

        [Test]
        public void SingleSample_IsNotEnoughHistory()
        {
            _detector.AddSample(0, Vector3.Zero);

            Assert.That(Hit().Reason, Is.EqualTo(StrikeRejectReason.NotEnoughHistory));
        }
    }
}
