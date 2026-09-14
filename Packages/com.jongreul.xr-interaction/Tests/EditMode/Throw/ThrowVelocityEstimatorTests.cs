using Jongreul.XrInteraction.Throw;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction.Tests.Throw
{
    public class ThrowVelocityEstimatorTests
    {
        const double Dt = 1.0 / 90;

        static void Feed(ThrowVelocityEstimator estimator, Vector3 velocity, int frames, ref double t, ref Vector3 p,
            float noise = 0f)
        {
            for (int i = 0; i < frames; i++)
            {
                t += Dt;
                p += velocity * (float)Dt;
                // 결정적 잡음: 프레임마다 ±noise
                Vector3 jitter = new Vector3(i % 2 == 0 ? noise : -noise, 0, 0);
                estimator.AddSample(t, p + jitter);
            }
        }

        [Test]
        public void ConstantVelocity_IsRecoveredExactly()
        {
            var estimator = new ThrowVelocityEstimator();
            double t = 0;
            Vector3 p = Vector3.Zero;
            var velocity = new Vector3(1.5f, 2f, -0.5f);

            Feed(estimator, velocity, 20, ref t, ref p);

            Vector3 estimate = estimator.EstimateVelocity();
            Assert.That(Vector3.Distance(estimate, velocity), Is.LessThan(1e-3f), estimate.ToString());
        }

        [Test]
        public void Stationary_IsZero()
        {
            var estimator = new ThrowVelocityEstimator();
            double t = 0;
            Vector3 p = new Vector3(1, 1, 1);

            Feed(estimator, Vector3.Zero, 20, ref t, ref p);

            Assert.That(estimator.EstimateVelocity().Length(), Is.LessThan(1e-4f));
        }

        [Test]
        public void FewerThanTwoSamples_IsZero()
        {
            var estimator = new ThrowVelocityEstimator();
            estimator.AddSample(0, Vector3.One);

            Assert.That(estimator.EstimateVelocity(), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void OldMotion_OutsideWindow_IsForgotten()
        {
            var estimator = new ThrowVelocityEstimator(windowSeconds: 0.1);
            double t = 0;
            Vector3 p = Vector3.Zero;

            Feed(estimator, new Vector3(5, 0, 0), 20, ref t, ref p);
            Feed(estimator, Vector3.Zero, 20, ref t, ref p); // 0.22초 정지

            Assert.That(estimator.EstimateVelocity().Length(), Is.LessThan(1e-3f));
        }

        [Test]
        public void TrackingJitter_IsSmoothed_ComparedToLastFrameDifference()
        {
            var estimator = new ThrowVelocityEstimator();
            double t = 0;
            Vector3 p = Vector3.Zero;
            var velocity = new Vector3(2, 0, 0);

            Feed(estimator, velocity, 20, ref t, ref p, noise: 0.005f);

            // 마지막 두 프레임 차이라면 ±0.01 m / (1/90 s) = ±0.9 m/s 오차가 실린다.
            float naiveError = 0.01f / (float)Dt;
            float error = Vector3.Distance(estimator.EstimateVelocity(), velocity);
            Assert.That(error, Is.LessThan(naiveError * 0.2f), $"error={error}");
        }

        [Test]
        public void RewoundSample_IsIgnored()
        {
            var estimator = new ThrowVelocityEstimator();
            double t = 0;
            Vector3 p = Vector3.Zero;
            Feed(estimator, new Vector3(1, 0, 0), 10, ref t, ref p);
            int before = estimator.Count;

            estimator.AddSample(t - 0.05, new Vector3(100, 0, 0));

            Assert.That(estimator.Count, Is.EqualTo(before));
            Assert.That(estimator.EstimateVelocity().X, Is.EqualTo(1f).Within(1e-3f));
        }
    }
}
