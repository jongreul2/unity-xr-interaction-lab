using System;
using Jongreul.XrInteraction.Zipline;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction.Tests.Zipline
{
    public class ZiplinePathTests
    {
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 1e-4f;

        // (0,0,0) → (3,0,0) → (3,4,0): 길이 3 + 4
        static ZiplinePath Elbow() => new ZiplinePath(new[] { Vector3.Zero, new Vector3(3, 0, 0), new Vector3(3, 4, 0) });

        [Test]
        public void Length_IsSumOfSegments()
        {
            Assert.That(Elbow().Length, Is.EqualTo(7f).Within(1e-5f));
        }

        [Test]
        public void Evaluate_WalksByArcLength_AndClampsAtEnds()
        {
            ZiplinePath path = Elbow();

            Assert.That(Near(path.Evaluate(1.5f), new Vector3(1.5f, 0, 0)), Is.True);
            Assert.That(Near(path.Evaluate(5f), new Vector3(3, 2, 0)), Is.True, path.Evaluate(5f).ToString());
            Assert.That(Near(path.Evaluate(-1f), path.Start), Is.True);
            Assert.That(Near(path.Evaluate(100f), path.End), Is.True);
            Assert.That(Near(path.TangentAt(5f), new Vector3(0, 1, 0)), Is.True);
        }

        [Test]
        public void Project_FindsNearestPointOnCable()
        {
            ZiplinePath path = Elbow();
            var point = new Vector3(4, 1, 0);

            Assert.That(path.Project(point), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(path.DistanceTo(point), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Sagging_DropsMiddleBySag_AndKeepsEnds()
        {
            var start = new Vector3(0, 3, 0);
            var end = new Vector3(0, 2, 10);

            ZiplinePath path = ZiplinePath.Sagging(start, end, sag: 0.4f, segments: 20);

            Assert.That(Near(path.Start, start) && Near(path.End, end), Is.True);
            Assert.That(path.GetPoint(10).Y, Is.EqualTo(2.5f - 0.4f).Within(1e-4f), "가운데 = 직선 높이 − sag");
        }

        [Test]
        public void Constructor_RejectsTooFewOrOverlappingPoints()
        {
            Assert.Throws<ArgumentException>(() => new ZiplinePath(new[] { Vector3.Zero }));
            Assert.Throws<ArgumentException>(() => new ZiplinePath(new[] { Vector3.Zero, Vector3.Zero, Vector3.One }));
        }
    }

    public class ZiplineRideTests
    {
        const float Dt = 1f / 90f;

        // 10 m 가는 동안 10 m 내려가는(45°) 가파른 케이블
        static ZiplinePath Steep() => new ZiplinePath(new[] { new Vector3(0, 10, 0), new Vector3(0, 0, 10) });

        // 12 m 가는 동안 1 m 내려가는 완만한 케이블
        static ZiplinePath Gentle() => new ZiplinePath(new[] { new Vector3(0, 3, 0), new Vector3(0, 2, 12) });

        static ZiplineEndReason RunUntilEnd(ZiplineRide ride, float maxSeconds = 60f)
        {
            for (float t = 0; t < maxSeconds; t += Dt)
            {
                ZiplineEndReason reason = ride.Step(Dt);
                if (reason != ZiplineEndReason.None)
                    return reason;
            }

            return ZiplineEndReason.None;
        }

        [Test]
        public void Downhill_Accelerates_FromMinSpeed()
        {
            var ride = new ZiplineRide(Gentle());
            ride.Grab(HandSide.Right, 0f);
            Assert.That(ride.Speed, Is.EqualTo(ride.Settings.MinSpeed));

            for (int i = 0; i < 90; i++)
                ride.Step(Dt);

            Assert.That(ride.Speed, Is.GreaterThan(ride.Settings.MinSpeed + 0.3f));
        }

        [Test]
        public void SteepCable_NeverExceedsMaxSpeed()
        {
            var ride = new ZiplineRide(Steep());
            ride.Grab(HandSide.Right, 0f);
            float top = 0f;

            while (ride.IsRiding)
            {
                ride.Step(Dt);
                top = Math.Max(top, ride.Speed);
            }

            Assert.That(top, Is.LessThanOrEqualTo(ride.Settings.MaxSpeed + 1e-4f));
            Assert.That(top, Is.GreaterThan(ride.Settings.MaxSpeed - 0.5f), "가파르면 상한까지 붙는다");
        }

        [Test]
        public void ArrivalSpeed_IsBraked_EvenOnSteepCable()
        {
            var ride = new ZiplineRide(Steep());
            float exitSpeed = -1f;
            ride.Ended += (_, velocity) => exitSpeed = velocity.Length();
            ride.Grab(HandSide.Right, 0f);

            Assert.That(RunUntilEnd(ride), Is.EqualTo(ZiplineEndReason.ReachedEnd));
            Assert.That(exitSpeed, Is.LessThanOrEqualTo(ride.Settings.ArrivalSpeed + 0.1f), $"exit={exitSpeed}");
        }

        [Test]
        public void SpeedStaysUnderBrakeCurve_ThroughoutTheRide()
        {
            var ride = new ZiplineRide(Steep());
            ride.Grab(HandSide.Right, 0f);

            while (ride.IsRiding)
            {
                float remaining = ride.Path.Length - ride.Distance;
                float limit = ride.BrakeLimit(remaining);
                ride.Step(Dt);
                if (ride.IsRiding)
                    Assert.That(ride.Speed, Is.LessThanOrEqualTo(limit + 1e-4f));
            }
        }

        [Test]
        public void SaggingCable_WithUphillEnd_NeverStalls()
        {
            // 처짐이 커서 끝부분이 오르막인 케이블: 최저 속도 덕분에 멈추거나 되돌아가지 않는다
            ZiplinePath path = ZiplinePath.Sagging(new Vector3(0, 3, 0), new Vector3(0, 2.8f, 10), sag: 1.5f);
            Assert.That(path.TangentAt(path.Length - 0.1f).Y, Is.GreaterThan(0f), "전제: 끝이 오르막");
            var ride = new ZiplineRide(path);
            ride.Grab(HandSide.Left, 0f);
            float previous = ride.Distance;

            while (ride.IsRiding)
            {
                ride.Step(Dt);
                Assert.That(ride.Distance, Is.GreaterThanOrEqualTo(previous));
                previous = ride.Distance;
            }

            Assert.That(ride.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void ReachingEnd_EndsRide_AndLetsGoOfHands()
        {
            var ride = new ZiplineRide(Gentle());
            ride.Grab(HandSide.Right, 0f);

            Assert.That(RunUntilEnd(ride), Is.EqualTo(ZiplineEndReason.ReachedEnd));
            Assert.That(ride.IsRiding, Is.False);
            Assert.That(ride.HandCount, Is.EqualTo(0));
            Assert.That(ride.Distance, Is.EqualTo(ride.Path.Length));
        }

        [Test]
        public void ReleasingBothHands_AfterGrace_Drops_WithVelocityAlongCable()
        {
            var ride = new ZiplineRide(Gentle());
            ZiplineEndReason reason = ZiplineEndReason.None;
            Vector3 exit = Vector3.Zero;
            ride.Ended += (r, v) => (reason, exit) = (r, v);
            ride.Grab(HandSide.Right, 0f);
            for (int i = 0; i < 90; i++)
                ride.Step(Dt);
            Vector3 expected = ride.Velocity;

            ride.Release(HandSide.Right);
            for (int i = 0; i < 30 && ride.IsRiding; i++)
                ride.Step(Dt);

            Assert.That(reason, Is.EqualTo(ZiplineEndReason.Released));
            Assert.That(Vector3.Distance(exit, expected), Is.LessThan(0.1f), $"{exit} vs {expected}");
            Assert.That(ride.Progress, Is.LessThan(1f));
        }

        [Test]
        public void SwappingHands_WithinGrace_KeepsRiding_AndSpeed()
        {
            var ride = new ZiplineRide(Gentle());
            ride.Grab(HandSide.Right, 0f);
            for (int i = 0; i < 90; i++)
                ride.Step(Dt);

            ride.Release(HandSide.Right);
            for (int i = 0; i < 9; i++) // 0.1초 동안 빈손
                ride.Step(Dt);
            float speed = ride.Speed;
            ride.Grab(HandSide.Left, 0f); // 타는 중이면 거리 인자는 무시된다

            Assert.That(ride.IsRiding, Is.True);
            Assert.That(ride.IsHeldBy(HandSide.Left), Is.True);
            Assert.That(ride.Speed, Is.EqualTo(speed), "다시 출발하지 않는다");
            Assert.That(ride.Distance, Is.GreaterThan(1f));
        }

        [Test]
        public void SecondHand_WhileRiding_DoesNotRestart()
        {
            var ride = new ZiplineRide(Gentle());
            int starts = 0;
            ride.Started += () => starts++;
            ride.Grab(HandSide.Right, 2f);
            ride.Step(Dt);
            float distance = ride.Distance;

            ride.Grab(HandSide.Left, 0f);

            Assert.That(starts, Is.EqualTo(1));
            Assert.That(ride.HandCount, Is.EqualTo(2));
            Assert.That(ride.Distance, Is.EqualTo(distance));
        }

        [Test]
        public void OneHandLetGo_WhileOtherHolds_KeepsRiding()
        {
            var ride = new ZiplineRide(Gentle());
            ride.Grab(HandSide.Right, 0f);
            ride.Grab(HandSide.Left, 0f);

            ride.Release(HandSide.Right);
            for (int i = 0; i < 60; i++)
                ride.Step(Dt);

            Assert.That(ride.IsRiding, Is.True);
        }

        [Test]
        public void Progress_GoesFromZeroToOne_Monotonically()
        {
            var ride = new ZiplineRide(Gentle());
            ride.Grab(HandSide.Right, 0f);
            float previous = ride.Progress;
            Assert.That(previous, Is.EqualTo(0f));

            while (ride.IsRiding)
            {
                ride.Step(Dt);
                Assert.That(ride.Progress, Is.GreaterThanOrEqualTo(previous));
                previous = ride.Progress;
            }

            Assert.That(previous, Is.EqualTo(1f));
        }

        [Test]
        public void Cancel_EndsWithCancelled()
        {
            var ride = new ZiplineRide(Gentle());
            ZiplineEndReason reason = ZiplineEndReason.None;
            ride.Ended += (r, _) => reason = r;
            ride.Grab(HandSide.Right, 1f);

            ride.Cancel();

            Assert.That(reason, Is.EqualTo(ZiplineEndReason.Cancelled));
            Assert.That(ride.IsRiding, Is.False);
        }
    }
}
