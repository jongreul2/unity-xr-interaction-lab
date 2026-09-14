using UnityEngine;

namespace Jongreul.XrInteraction
{
    /// <summary>Time.timeScale의 영향을 받지 않는 실시간 시계. Core 로직에 주입한다.</summary>
    public sealed class UnityClock : IClock
    {
        public double Now => Time.unscaledTimeAsDouble;
    }
}
