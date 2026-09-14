using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jongreul.XrInteraction.Throw
{
    /// <summary>
    /// 놓는 순간의 던지기 속도 추정. 마지막 두 프레임 차이만 쓰면 추적 잡음과 손을 펴는 순간의 흔들림이 그대로 실린다.
    /// 최근 구간의 샘플에 직선을 맞춰(최소제곱) 기울기를 속도로 쓴다.
    /// </summary>
    public sealed class ThrowVelocityEstimator
    {
        readonly struct Sample
        {
            public readonly double Time;
            public readonly Vector3 Position;

            public Sample(double time, Vector3 position)
            {
                Time = time;
                Position = position;
            }
        }

        readonly List<Sample> _samples = new List<Sample>();

        public ThrowVelocityEstimator(double windowSeconds = 0.1, int maxSamples = 64)
        {
            if (!(windowSeconds > 0))
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (maxSamples < 2)
                throw new ArgumentOutOfRangeException(nameof(maxSamples));

            WindowSeconds = windowSeconds;
            MaxSamples = maxSamples;
        }

        public double WindowSeconds { get; }
        public int MaxSamples { get; }
        public int Count => _samples.Count;

        public void AddSample(double time, Vector3 position)
        {
            if (_samples.Count > 0 && time <= _samples[_samples.Count - 1].Time)
                return; // 시간이 되돌아간 샘플은 버린다

            _samples.Add(new Sample(time, position));

            double cutoff = time - WindowSeconds;
            int remove = 0;
            while (remove < _samples.Count && _samples[remove].Time < cutoff)
                remove++;
            remove = Math.Max(remove, _samples.Count - MaxSamples);
            if (remove > 0)
                _samples.RemoveRange(0, remove);
        }

        /// <summary>구간 샘플의 최소제곱 기울기. 샘플이 둘보다 적으면 0.</summary>
        public Vector3 EstimateVelocity()
        {
            int n = _samples.Count;
            if (n < 2)
                return Vector3.Zero;

            double meanT = 0;
            Vector3 meanP = Vector3.Zero;
            foreach (Sample s in _samples)
            {
                meanT += s.Time;
                meanP += s.Position;
            }

            meanT /= n;
            meanP /= n;

            double denominator = 0;
            Vector3 numerator = Vector3.Zero;
            foreach (Sample s in _samples)
            {
                double dt = s.Time - meanT;
                denominator += dt * dt;
                numerator += (s.Position - meanP) * (float)dt;
            }

            return denominator > 1e-12 ? numerator / (float)denominator : Vector3.Zero;
        }

        public void Clear() => _samples.Clear();
    }
}
