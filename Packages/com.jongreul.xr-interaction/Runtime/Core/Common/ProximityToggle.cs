using System;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// 거리 기반 켜기/끄기. 켜지는 거리보다 꺼지는 거리를 멀게 둬(히스테리시스) 경계에서 깜빡이지 않게 한다.
    /// 예: 진열대에 다가가면 켜지고, 조금 더 멀어져야 꺼진다.
    /// </summary>
    public sealed class ProximityToggle
    {
        public ProximityToggle(float enterDistance, float exitDistance)
        {
            if (!(enterDistance > 0))
                throw new ArgumentOutOfRangeException(nameof(enterDistance));
            if (!(exitDistance >= enterDistance))
                throw new ArgumentOutOfRangeException(nameof(exitDistance), "꺼지는 거리는 켜지는 거리 이상이어야 한다.");

            EnterDistance = enterDistance;
            ExitDistance = exitDistance;
        }

        public float EnterDistance { get; }
        public float ExitDistance { get; }
        public bool IsActive { get; private set; }

        public event Action<bool> Changed;

        /// <summary>현재 거리를 넣고 켜짐 여부를 받는다.</summary>
        public bool Update(float distance)
        {
            bool next = IsActive ? distance <= ExitDistance : distance <= EnterDistance;
            if (next != IsActive)
            {
                IsActive = next;
                Changed?.Invoke(next);
            }

            return IsActive;
        }
    }
}
