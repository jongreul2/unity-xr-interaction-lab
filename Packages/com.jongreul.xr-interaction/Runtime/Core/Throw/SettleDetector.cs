using System;

namespace Jongreul.XrInteraction.Throw
{
    public enum SettleReason
    {
        None,
        AtRest,
        /// <summary>계속 구르거나 떨려서 제한 시간에 강제로 멈췄다.</summary>
        TimedOut,
    }

    /// <summary>
    /// 던진 물체가 멈췄는지 판정한다. 속도가 기준 아래로 restSeconds 동안 이어지면 멈춘 것으로 보고,
    /// 계속 구르거나 떨리는 물체도 놓은 뒤 maxSeconds가 지나면 멈춘 것으로 한다.
    /// 멈춘 순간 물리를 끄고 자리를 확정하면 멀티플레이에서 피어마다 정지 위치가 달라지지 않는다.
    /// </summary>
    public sealed class SettleDetector
    {
        double _startedAt;
        double _restSince = -1;

        public SettleDetector(float speedThreshold = 0.08f, float restSeconds = 0.4f, float maxSeconds = 5f)
        {
            if (!(speedThreshold >= 0f))
                throw new ArgumentOutOfRangeException(nameof(speedThreshold));
            if (!(restSeconds > 0f))
                throw new ArgumentOutOfRangeException(nameof(restSeconds));
            if (!(maxSeconds >= restSeconds))
                throw new ArgumentOutOfRangeException(nameof(maxSeconds));

            SpeedThreshold = speedThreshold;
            RestSeconds = restSeconds;
            MaxSeconds = maxSeconds;
        }

        public float SpeedThreshold { get; }
        public float RestSeconds { get; }
        public float MaxSeconds { get; }
        public bool IsTracking { get; private set; }
        public bool IsSettled { get; private set; }
        public SettleReason Reason { get; private set; }

        /// <summary>놓은(던진) 순간부터 지켜본다.</summary>
        public void Begin(double time)
        {
            IsTracking = true;
            IsSettled = false;
            Reason = SettleReason.None;
            _startedAt = time;
            _restSince = -1;
        }

        /// <summary>다시 잡히거나 제자리로 돌아가면 그만 지켜본다.</summary>
        public void Cancel()
        {
            IsTracking = false;
            IsSettled = false;
            Reason = SettleReason.None;
            _restSince = -1;
        }

        /// <returns>이번 호출에서 멈췄으면 true(한 번만).</returns>
        public bool Update(double time, float speed)
        {
            if (!IsTracking)
                return false;

            if (speed <= SpeedThreshold)
            {
                if (_restSince < 0)
                    _restSince = time;
                if (time - _restSince >= RestSeconds)
                    return Settle(SettleReason.AtRest);
            }
            else
            {
                _restSince = -1;
            }

            return time - _startedAt >= MaxSeconds && Settle(SettleReason.TimedOut);
        }

        bool Settle(SettleReason reason)
        {
            IsTracking = false;
            IsSettled = true;
            Reason = reason;
            return true;
        }
    }
}
