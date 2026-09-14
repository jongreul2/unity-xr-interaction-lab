using System;

namespace Jongreul.XrInteraction.Grip
{
    /// <summary>
    /// 손으로 끄는 트랙 슬라이더의 수학. 트랙 로컬 x(가운데 0, 전체 길이 length)를 0~1 진행값으로,
    /// 진행값을 값 범위로 바꾼다. 범위 밖은 양 끝에 붙는다.
    /// </summary>
    public static class TrackSlider
    {
        public static float ToNormalized(float localX, float length)
        {
            if (!(length > 0f))
                throw new ArgumentOutOfRangeException(nameof(length));
            return Clamp01(localX / length + 0.5f);
        }

        public static float ToLocalX(float normalized, float length) => (Clamp01(normalized) - 0.5f) * length;

        public static float ToValue(float normalized, float min, float max) => min + (max - min) * Clamp01(normalized);

        public static float FromValue(float value, float min, float max) =>
            max == min ? 0f : Clamp01((value - min) / (max - min));

        /// <summary>step 간격으로 반올림(0 이하면 그대로). 저장값이 0.0312 같은 끝수로 남지 않게 한다.</summary>
        public static float Snap(float value, float step) => step > 0f ? MathF.Round(value / step) * step : value;

        static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
