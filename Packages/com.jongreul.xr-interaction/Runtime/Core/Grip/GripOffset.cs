using System;
using System.Numerics;

namespace Jongreul.XrInteraction.Grip
{
    /// <summary>
    /// 손 기준 도구 자세(위치·회전). 도구마다, 손마다 따로 둔다.
    /// 월드 자세 = 손 자세 × 오프셋. 에디터에서 도구를 손에 맞춰 놓은 뒤 <see cref="FromPoses"/>로 역산해 저장한다.
    /// </summary>
    public readonly struct GripOffset : IEquatable<GripOffset>
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public GripOffset(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation.LengthSquared() > 1e-10f ? Quaternion.Normalize(rotation) : Quaternion.Identity;
        }

        public static GripOffset Identity => new GripOffset(Vector3.Zero, Quaternion.Identity);

        /// <summary>손 자세에 오프셋을 적용한 도구의 월드 자세.</summary>
        public void Apply(Vector3 handPosition, Quaternion handRotation, out Vector3 itemPosition, out Quaternion itemRotation)
        {
            itemPosition = handPosition + Vector3.Transform(Position, handRotation);
            itemRotation = Quaternion.Normalize(handRotation * Rotation);
        }

        /// <summary>손과 도구의 현재 자세로부터 오프셋을 역산한다.</summary>
        public static GripOffset FromPoses(Vector3 handPosition, Quaternion handRotation, Vector3 itemPosition,
            Quaternion itemRotation)
        {
            Quaternion inverse = Quaternion.Inverse(handRotation);
            return new GripOffset(Vector3.Transform(itemPosition - handPosition, inverse), inverse * itemRotation);
        }

        /// <summary>
        /// 좌우 거울상(손의 로컬 X축 기준). 오른손 오프셋 하나로 왼손 오프셋을 만든다.
        /// YZ 평면 반사: 위치 x 부호 반전, 회전은 (x, −y, −z, w).
        /// </summary>
        public GripOffset MirrorX() =>
            new GripOffset(new Vector3(-Position.X, Position.Y, Position.Z),
                new Quaternion(Rotation.X, -Rotation.Y, -Rotation.Z, Rotation.W));

        public bool Equals(GripOffset other) => Position == other.Position && Rotation == other.Rotation;
        public override bool Equals(object obj) => obj is GripOffset other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, Rotation);
        public override string ToString() => $"pos={Position} rot={Rotation}";
    }
}
