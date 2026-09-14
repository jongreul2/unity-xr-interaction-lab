using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// Core(엔진 비의존, System.Numerics) ↔ Unity 타입 변환. 성분을 그대로 옮긴다 —
    /// 쿼터니언 곱과 벡터 회전 공식은 좌표계 손잡이와 무관하게 성분 단위로 같다.
    /// </summary>
    public static class NumericsExtensions
    {
        public static NVector3 ToNumerics(this Vector3 v) => new NVector3(v.x, v.y, v.z);

        public static Vector3 ToUnity(this NVector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static NQuaternion ToNumerics(this Quaternion q) => new NQuaternion(q.x, q.y, q.z, q.w);

        public static Quaternion ToUnity(this NQuaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
