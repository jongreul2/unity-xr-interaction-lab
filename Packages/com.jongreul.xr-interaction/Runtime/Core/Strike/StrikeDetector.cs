using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jongreul.XrInteraction.Strike
{
    public enum StrikerKind
    {
        Hand,
        /// <summary>손에 쥔 도구(막대·무기). 도구 끝이 손보다 크게 돌기 때문에 손 기준 임계를 따로 둔다.</summary>
        Tool,
    }

    public enum StrikeRejectReason
    {
        None,
        LockedOut,
        NotEnoughHistory,
        MovingAway,
        WrongAngle,
        TooSlow,
        StrokeTooShort,
    }

    /// <summary>판정 기준. 수치는 데모 씬의 튜닝 패널에서 실측해 정한다.</summary>
    [Serializable]
    public sealed class StrikeSettings
    {
        /// <summary>맨손: 타격면 안쪽으로 들어가는 속도 하한(m/s).</summary>
        public float MinHandSpeed = 1.8f;

        /// <summary>도구: 타격면 안쪽으로 들어가는 손 속도 하한(m/s).</summary>
        public float MinToolSpeed = 1.2f;

        /// <summary>속도 방향과 "타격면 안쪽" 사이 최대 각. 넘으면 스치는 동작으로 본다.</summary>
        public float MaxAngleDegrees = 50f;

        /// <summary>접촉 직전 타격면 쪽으로 움직인 최소 거리(m). 팔목 스냅처럼 짧고 빠른 동작을 거른다.</summary>
        public float MinStrokeLength = 0.12f;

        /// <summary>속도를 구하는 구간(s). 한두 프레임만 보면 추적 잡음에 흔들린다.</summary>
        public float VelocityWindowSeconds = 0.03f;

        /// <summary>스트로크 길이를 재는 구간(s).</summary>
        public float StrokeWindowSeconds = 0.3f;

        /// <summary>한 번 맞은 뒤 다시 인정하지 않는 시간(s). 표면에서 튕기며 여러 번 닿는 것을 막는다.</summary>
        public float LockoutSeconds = 0.3f;

        /// <summary>샘플 사이가 이보다 비면 추적이 끊긴 것으로 보고 이력을 버린다(s).</summary>
        public float MaxSampleGapSeconds = 0.1f;
    }

    public readonly struct StrikeResult
    {
        public readonly bool Hit;
        public readonly StrikeRejectReason Reason;

        /// <summary>타격면 안쪽으로 들어가는 속도(m/s).</summary>
        public readonly float Speed;

        public readonly float AngleDegrees;
        public readonly float StrokeLength;

        public StrikeResult(bool hit, StrikeRejectReason reason, float speed, float angleDegrees, float strokeLength)
        {
            Hit = hit;
            Reason = reason;
            Speed = speed;
            AngleDegrees = angleDegrees;
            StrokeLength = strokeLength;
        }

        public override string ToString() =>
            $"{(Hit ? "HIT" : Reason.ToString())} speed={Speed:0.00} angle={AngleDegrees:0} stroke={StrokeLength:0.00}";
    }

    /// <summary>
    /// 손 속도로 "내리쳤는가"를 판정한다.
    /// - 위치는 플레이어 리그 기준(로컬)으로 받는다. 걷거나 탈것에 타서 리그가 움직여도 손을 휘두른 게 아니면 속도가 생기지 않는다.
    /// - 속도는 프레임 이력에서 구한다. 네트워크 틱 재시뮬레이션처럼 시간이 되돌아간 샘플은 버리고 이력은 유지한다.
    /// - 타격면 법선 기준으로 안쪽 속도·각도·스트로크 길이를 함께 본다.
    /// </summary>
    public sealed class StrikeDetector
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

        readonly List<Sample> _history = new List<Sample>();
        double _lastHitTime = double.NegativeInfinity;

        public StrikeDetector(StrikeSettings settings = null)
        {
            Settings = settings ?? new StrikeSettings();
        }

        public StrikeSettings Settings { get; }
        public int SampleCount => _history.Count;
        public int RewoundSamplesIgnored { get; private set; }

        /// <summary>매 프레임 손(또는 도구 끝) 위치를 리그 로컬로 넣는다.</summary>
        public void AddSample(double time, Vector3 rigLocalPosition)
        {
            if (_history.Count > 0)
            {
                double last = _history[_history.Count - 1].Time;
                if (time <= last)
                {
                    // 시간이 되돌아간 샘플(재시뮬레이션 틱 등). 기준을 리셋하지 않고 무시한다.
                    RewoundSamplesIgnored++;
                    return;
                }

                if (time - last > Settings.MaxSampleGapSeconds)
                    _history.Clear();
            }

            _history.Add(new Sample(time, rigLocalPosition));

            // 스트로크 구간보다 오래된 샘플은 버린다(구간 경계 계산용으로 하나는 남긴다).
            double cutoff = time - Math.Max(Settings.StrokeWindowSeconds, Settings.VelocityWindowSeconds);
            int remove = 0;
            while (remove + 1 < _history.Count && _history[remove + 1].Time <= cutoff)
                remove++;
            if (remove > 0)
                _history.RemoveRange(0, remove);
        }

        /// <summary>타격면에 닿은 순간 호출. 법선은 타격면 바깥쪽(리그 로컬).</summary>
        public StrikeResult Evaluate(double time, Vector3 surfaceNormal, StrikerKind kind)
        {
            if (time - _lastHitTime < Settings.LockoutSeconds)
                return Reject(StrikeRejectReason.LockedOut);

            if (_history.Count < 2 || surfaceNormal.LengthSquared() < 1e-10f)
                return Reject(StrikeRejectReason.NotEnoughHistory);

            Vector3 normal = Vector3.Normalize(surfaceNormal);
            Sample latest = _history[_history.Count - 1];
            Sample start = SampleAtOrBefore(latest.Time - Settings.VelocityWindowSeconds);
            double dt = latest.Time - start.Time;
            if (dt <= 0)
                return Reject(StrikeRejectReason.NotEnoughHistory);

            Vector3 velocity = (latest.Position - start.Position) / (float)dt;
            float inward = -Vector3.Dot(velocity, normal);
            float stroke = StrokeLength(latest, normal);
            if (inward <= 0f)
                return new StrikeResult(false, StrikeRejectReason.MovingAway, inward, 180f, stroke);

            float speed = velocity.Length();
            float angle = MathF.Acos(Math.Clamp(inward / speed, -1f, 1f)) * (180f / MathF.PI);
            if (angle > Settings.MaxAngleDegrees)
                return new StrikeResult(false, StrikeRejectReason.WrongAngle, inward, angle, stroke);

            float threshold = kind == StrikerKind.Tool ? Settings.MinToolSpeed : Settings.MinHandSpeed;
            if (inward < threshold)
                return new StrikeResult(false, StrikeRejectReason.TooSlow, inward, angle, stroke);

            if (stroke < Settings.MinStrokeLength)
                return new StrikeResult(false, StrikeRejectReason.StrokeTooShort, inward, angle, stroke);

            _lastHitTime = time;
            return new StrikeResult(true, StrikeRejectReason.None, inward, angle, stroke);
        }

        public void Reset()
        {
            _history.Clear();
            _lastHitTime = double.NegativeInfinity;
        }

        /// <summary>구간 안에서 타격면 쪽으로 가장 멀리 온 거리.</summary>
        float StrokeLength(Sample latest, Vector3 normal)
        {
            float best = 0f;
            double from = latest.Time - Settings.StrokeWindowSeconds;
            foreach (Sample sample in _history)
            {
                if (sample.Time < from)
                    continue;
                float travelled = -Vector3.Dot(latest.Position - sample.Position, normal);
                if (travelled > best)
                    best = travelled;
            }

            return best;
        }

        Sample SampleAtOrBefore(double time)
        {
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].Time <= time)
                    return _history[i];
            }

            return _history[0];
        }

        static StrikeResult Reject(StrikeRejectReason reason) => new StrikeResult(false, reason, 0f, 0f, 0f);
    }

    /// <summary>월드 좌표 ↔ 플레이어 리그 로컬 좌표.</summary>
    public static class RigSpace
    {
        public static Vector3 ToLocalPoint(Vector3 worldPoint, Vector3 rigPosition, Quaternion rigRotation) =>
            Vector3.Transform(worldPoint - rigPosition, Quaternion.Inverse(rigRotation));

        public static Vector3 ToLocalDirection(Vector3 worldDirection, Quaternion rigRotation) =>
            Vector3.Transform(worldDirection, Quaternion.Inverse(rigRotation));
    }
}
