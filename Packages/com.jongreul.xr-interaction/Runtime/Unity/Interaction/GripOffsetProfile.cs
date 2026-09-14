using System;
using Jongreul.XrInteraction.Grip;
using UnityEngine;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// 도구별 그립 오프셋(손 기준 위치·회전). 오른손 값만 넣으면 왼손은 거울상으로 만든다.
    /// 아티스트·기획이 코드 없이 에디터 창에서 Play 중에 맞추고 바로 저장한다.
    /// </summary>
    [CreateAssetMenu(menuName = "XR Interaction Lab/Grip Offset Profile", fileName = "GripOffset")]
    public sealed class GripOffsetProfile : ScriptableObject
    {
        [SerializeField] Vector3 rightPosition;
        [SerializeField] Vector3 rightEuler;
        [SerializeField] bool mirrorForLeft = true;
        [SerializeField] Vector3 leftPosition;
        [SerializeField] Vector3 leftEuler;

        public bool MirrorForLeft
        {
            get => mirrorForLeft;
            set => mirrorForLeft = value;
        }

        public event Action Changed;

        public GripOffset Get(HandSide side)
        {
            var right = new GripOffset(rightPosition.ToNumerics(), Quaternion.Euler(rightEuler).ToNumerics());
            if (side == HandSide.Right)
                return right;

            return mirrorForLeft
                ? right.MirrorX()
                : new GripOffset(leftPosition.ToNumerics(), Quaternion.Euler(leftEuler).ToNumerics());
        }

        /// <summary>한쪽 손 값을 저장한다. 왼손을 따로 저장하면 거울상 모드를 끈다.</summary>
        public void Set(HandSide side, GripOffset offset)
        {
            Vector3 position = offset.Position.ToUnity();
            Vector3 euler = offset.Rotation.ToUnity().eulerAngles;
            if (side == HandSide.Right)
            {
                rightPosition = position;
                rightEuler = euler;
            }
            else
            {
                mirrorForLeft = false;
                leftPosition = position;
                leftEuler = euler;
            }

            Changed?.Invoke();
        }

        void OnValidate() => Changed?.Invoke();
    }
}
