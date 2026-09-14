using System;
using Jongreul.XrInteraction.Throw;
using NUnit.Framework;

namespace Jongreul.XrInteraction.Tests.Throw
{
    public class SubmissionBoxTests
    {
        static SubmissionBox Box() => new SubmissionBox(capacity: 4, quota: 2, acceptedKinds: new[] { "ball" });

        [Test]
        public void AcceptedKind_IsAdded()
        {
            SubmissionBox box = Box();

            Assert.That(box.TryAdd("a", "ball"), Is.EqualTo(SubmitAddResult.Added));
            Assert.That(box.Count, Is.EqualTo(1));
            Assert.That(box.Contains("a"), Is.True);
        }

        [Test]
        public void SameItemTwice_CountsOnce()
        {
            SubmissionBox box = Box();
            box.TryAdd("a", "ball");

            Assert.That(box.TryAdd("a", "ball"), Is.EqualTo(SubmitAddResult.Duplicate));
            Assert.That(box.Count, Is.EqualTo(1));
        }

        [Test]
        public void WrongKind_IsRejected()
        {
            SubmissionBox box = Box();

            Assert.That(box.TryAdd("junk", "rock"), Is.EqualTo(SubmitAddResult.WrongKind));
            Assert.That(box.Count, Is.EqualTo(0));
        }

        [Test]
        public void OverCapacity_IsFull()
        {
            SubmissionBox box = Box();
            for (int i = 0; i < 4; i++)
                box.TryAdd($"b{i}", "ball");

            Assert.That(box.TryAdd("b4", "ball"), Is.EqualTo(SubmitAddResult.Full));
            Assert.That(box.Count, Is.EqualTo(4));
        }

        [Test]
        public void Remove_Uncounts_AndAllowsAddingAgain()
        {
            SubmissionBox box = Box();
            box.TryAdd("a", "ball");

            Assert.That(box.Remove("a"), Is.True);
            Assert.That(box.Remove("a"), Is.False);
            Assert.That(box.TryAdd("a", "ball"), Is.EqualTo(SubmitAddResult.Added));
        }

        [Test]
        public void Submit_Empty()
        {
            Assert.That(Box().Submit(out Delivery delivery), Is.EqualTo(SubmitResult.Empty));
            Assert.That(delivery.Count, Is.EqualTo(0));
        }

        [Test]
        public void Submit_BelowQuota_KeepsItems()
        {
            SubmissionBox box = Box();
            box.TryAdd("a", "ball");

            Assert.That(box.Submit(out _), Is.EqualTo(SubmitResult.NotEnough));
            Assert.That(box.Count, Is.EqualTo(1));
            Assert.That(box.Rounds, Is.EqualTo(0));
        }

        [Test]
        public void Submit_MeetingQuota_Delivers_AndEmptiesTheBox()
        {
            SubmissionBox box = Box();
            box.TryAdd("a", "ball");
            box.TryAdd("b", "ball");
            box.TryAdd("c", "ball");

            Assert.That(box.Submit(out Delivery delivery), Is.EqualTo(SubmitResult.Delivered));
            Assert.That(delivery.Items, Is.EquivalentTo(new[] { "a", "b", "c" }));
            Assert.That(delivery.Round, Is.EqualTo(1));
            Assert.That(box.Count, Is.EqualTo(0));
            Assert.That(box.TotalDelivered, Is.EqualTo(3));
        }

        [Test]
        public void NoKindFilter_AcceptsAnything()
        {
            var box = new SubmissionBox(capacity: 2, quota: 1);

            Assert.That(box.TryAdd("x", "anything"), Is.EqualTo(SubmitAddResult.Added));
            Assert.That(box.TryAdd("y", null), Is.EqualTo(SubmitAddResult.Added));
        }
    }

    public class SettleDetectorTests
    {
        const double Dt = 1.0 / 90;

        static bool Run(SettleDetector detector, ref double t, float speed, double seconds)
        {
            bool settled = false;
            for (double elapsed = 0; elapsed < seconds; elapsed += Dt)
            {
                t += Dt;
                settled |= detector.Update(t, speed);
            }

            return settled;
        }

        static SettleDetector Detector() => new SettleDetector(speedThreshold: 0.08f, restSeconds: 0.4f, maxSeconds: 5f);

        [Test]
        public void SlowForRestSeconds_Settles_AtRest()
        {
            SettleDetector detector = Detector();
            double t = 0;
            detector.Begin(t);

            Assert.That(Run(detector, ref t, 0.01f, 0.35), Is.False);
            Assert.That(Run(detector, ref t, 0.01f, 0.1), Is.True);
            Assert.That(detector.Reason, Is.EqualTo(SettleReason.AtRest));
            Assert.That(detector.IsSettled, Is.True);
        }

        [Test]
        public void KeepsRolling_IsStoppedAtMaxSeconds()
        {
            SettleDetector detector = Detector();
            double t = 0;
            detector.Begin(t);

            Assert.That(Run(detector, ref t, 1f, 4.9), Is.False);
            Assert.That(Run(detector, ref t, 1f, 0.2), Is.True);
            Assert.That(detector.Reason, Is.EqualTo(SettleReason.TimedOut));
        }

        [Test]
        public void SlowdownsShorterThanRestSeconds_DoNotSettle()
        {
            SettleDetector detector = Detector();
            double t = 0;
            detector.Begin(t);

            bool settled = Run(detector, ref t, 2f, 0.3) | Run(detector, ref t, 0.02f, 0.3) |
                           Run(detector, ref t, 1f, 0.1) | Run(detector, ref t, 0.02f, 0.3);

            Assert.That(settled, Is.False, "구르다 잠깐 느려지는 순간(바운드 꼭대기 등)은 멈춘 것이 아니다");
        }

        [Test]
        public void ReportsSettleOnlyOnce()
        {
            SettleDetector detector = Detector();
            double t = 0;
            detector.Begin(t);
            Run(detector, ref t, 0f, 0.5);

            Assert.That(detector.Update(t + 0.1, 0f), Is.False);
            Assert.That(detector.IsTracking, Is.False);
        }

        [Test]
        public void NotTracking_NeverSettles()
        {
            SettleDetector detector = Detector();
            double t = 0;
            Assert.That(Run(detector, ref t, 0f, 1.0), Is.False, "Begin 전");

            detector.Begin(t);
            detector.Cancel();
            Assert.That(Run(detector, ref t, 0f, 1.0), Is.False, "Cancel 후");
        }

        [Test]
        public void BeginAgain_AfterSettling_TracksAnew()
        {
            SettleDetector detector = Detector();
            double t = 0;
            detector.Begin(t);
            Run(detector, ref t, 0f, 0.5);

            detector.Begin(t);

            Assert.That(detector.IsSettled, Is.False);
            Assert.That(detector.Reason, Is.EqualTo(SettleReason.None));
            Assert.That(Run(detector, ref t, 0f, 0.5), Is.True);
        }

        [Test]
        public void InvalidSettings_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetector(restSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetector(restSeconds: 2f, maxSeconds: 1f));
        }
    }
}
