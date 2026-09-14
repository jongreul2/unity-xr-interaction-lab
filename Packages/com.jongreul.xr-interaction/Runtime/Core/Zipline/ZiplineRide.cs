using System;
using System.Numerics;

namespace Jongreul.XrInteraction.Zipline
{
    [Serializable]
    public sealed class ZiplineSettings
    {
        public float Gravity = 9.81f;

        /// <summary>선형 저항(1/s). 경사가 일정하면 종속도 = g·sinθ / Drag.</summary>
        public float Drag = 0.2f;

        /// <summary>처진 구간에서 멈춰 버리지 않게 하는 최저 속도(m/s). 케이블은 한 방향으로만 간다.</summary>
        public float MinSpeed = 1.0f;

        /// <summary>멀미를 막는 속도 상한(m/s).</summary>
        public float MaxSpeed = 6f;

        /// <summary>종점 앞 감속도(m/s²). 남은 거리로 허용 속도 곡선을 만든다.</summary>
        public float BrakeDeceleration = 2.5f;

        /// <summary>종점 도착 속도(m/s).</summary>
        public float ArrivalSpeed = 0.8f;

        /// <summary>양손을 다 놓은 뒤에도 탑승을 유지하는 시간(초). 손을 바꿔 잡는 사이 떨어지지 않게 한다.</summary>
        public float HandoverGrace = 0.2f;
    }

    public enum ZiplineEndReason
    {
        None,
        /// <summary>양손을 놓고 여유 시간이 지났다.</summary>
        Released,
        ReachedEnd,
        Cancelled,
    }

    /// <summary>
    /// 짚라인 탑승 규칙. 손잡이를 잡으면 출발하고, 경사(중력)·저항으로 속도가 정해지며,
    /// 종점 앞에서는 남은 거리로 만든 감속 곡선을 넘지 않는다. 양손을 놓으면 여유 시간 뒤에 떨어지고,
    /// 그 사이 다른 손으로 잡으면 속도를 잃지 않고 계속 간다. 종점에 닿으면 자동으로 놓는다.
    /// </summary>
    public sealed class ZiplineRide
    {
        readonly ZiplinePath _path;
        readonly ZiplineSettings _settings;
        readonly bool[] _hands = new bool[2];
        float _handsFreeFor;

        public ZiplineRide(ZiplinePath path, ZiplineSettings settings = null)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _settings = settings ?? new ZiplineSettings();
        }

        public ZiplinePath Path => _path;
        public ZiplineSettings Settings => _settings;
        public bool IsRiding { get; private set; }

        /// <summary>시작점에서 잰 현재 위치(m). 탑승이 끝나도 마지막 값을 유지한다.</summary>
        public float Distance { get; private set; }

        public float Speed { get; private set; }
        public float Progress => _path.Progress(Distance);
        public Vector3 Position => _path.Evaluate(Distance);
        public Vector3 Velocity => _path.TangentAt(Distance) * Speed;
        public int HandCount => (_hands[0] ? 1 : 0) + (_hands[1] ? 1 : 0);
        public bool IsHeldBy(HandSide side) => _hands[(int)side];

        public event Action Started;

        /// <summary>끝난 이유와 떠나는 순간의 속도(케이블 방향).</summary>
        public event Action<ZiplineEndReason, Vector3> Ended;

        /// <summary>
        /// 손잡이를 잡는다. 타고 있지 않으면 distance 지점에서 initialSpeed로 출발한다.
        /// 타는 중이면 손만 추가된다(위치·속도 유지).
        /// </summary>
        public void Grab(HandSide side, float distance, float initialSpeed = 0f)
        {
            _hands[(int)side] = true;
            _handsFreeFor = 0f;
            if (IsRiding)
                return;

            IsRiding = true;
            Distance = Math.Clamp(distance, 0f, _path.Length);
            Speed = Math.Clamp(initialSpeed, _settings.MinSpeed, _settings.MaxSpeed);
            Started?.Invoke();
        }

        public void Release(HandSide side) => _hands[(int)side] = false;

        /// <summary>한 틱 진행한다. 이번 틱에 끝났으면 그 이유, 아니면 None.</summary>
        public ZiplineEndReason Step(float deltaTime)
        {
            if (!IsRiding || !(deltaTime > 0f))
                return ZiplineEndReason.None;

            if (HandCount == 0)
            {
                _handsFreeFor += deltaTime;
                if (_handsFreeFor > _settings.HandoverGrace)
                    return End(ZiplineEndReason.Released);
            }

            Vector3 tangent = _path.TangentAt(Distance);
            float acceleration = -_settings.Gravity * tangent.Y - _settings.Drag * Speed;
            float speed = Math.Max(Speed + acceleration * deltaTime, _settings.MinSpeed);
            Speed = Math.Min(speed, Math.Min(_settings.MaxSpeed, BrakeLimit(_path.Length - Distance)));

            Distance += Speed * deltaTime;
            if (Distance >= _path.Length)
            {
                Distance = _path.Length;
                return End(ZiplineEndReason.ReachedEnd);
            }

            return ZiplineEndReason.None;
        }

        /// <summary>남은 거리에서 허용하는 최고 속도: v² = 도착속도² + 2·감속도·남은거리.</summary>
        public float BrakeLimit(float remaining)
        {
            float arrival = _settings.ArrivalSpeed;
            return MathF.Sqrt(arrival * arrival + 2f * _settings.BrakeDeceleration * Math.Max(0f, remaining));
        }

        /// <summary>외부 사정(순간이동 등)으로 탑승을 끊는다.</summary>
        public void Cancel()
        {
            if (IsRiding)
                End(ZiplineEndReason.Cancelled);
        }

        ZiplineEndReason End(ZiplineEndReason reason)
        {
            Vector3 exitVelocity = Velocity;
            IsRiding = false;
            Speed = 0f;
            _hands[0] = false;
            _hands[1] = false;
            _handsFreeFor = 0f;
            Ended?.Invoke(reason, exitVelocity);
            return reason;
        }
    }
}
