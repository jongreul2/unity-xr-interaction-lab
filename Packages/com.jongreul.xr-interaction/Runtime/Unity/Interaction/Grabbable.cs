using System;
using Jongreul.XrInteraction.Grip;
using UnityEngine;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// 손으로 잡을 수 있는 물체. 잡힌 동안은 키네매틱으로 손을 따라가고, 놓으면 물리로 돌아가 손 속도를 받는다.
    /// 그립 프로필이 있으면 도구처럼 정해진 자세로 쥐고, 없으면 잡은 순간의 상대 자세를 유지한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [SerializeField] GripOffsetProfile gripProfile;

        Rigidbody _body;
        GripOffset _offset;
        bool _hasOverride;
        GripOffset _override;
        bool _wasKinematic;

        public Hand Holder { get; private set; }
        public bool IsHeld => Holder != null;
        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public GripOffsetProfile GripProfile
        {
            get => gripProfile;
            set => gripProfile = value;
        }

        /// <summary>현재 쥐고 있는 오프셋(손 기준).</summary>
        public GripOffset CurrentOffset => _offset;

        public event Action<Grabbable, Hand> Grabbed;
        public event Action<Grabbable, Hand, Vector3> Released;

        public virtual bool CanBeGrabbedBy(Hand hand) => isActiveAndEnabled;

        /// <summary>에디터 라이브 튜닝용: 프로필 대신 이 오프셋을 쓴다.</summary>
        public void SetOffsetOverride(GripOffset offset)
        {
            _hasOverride = true;
            _override = offset;
            if (IsHeld)
                _offset = offset;
        }

        public void ClearOffsetOverride() => _hasOverride = false;

        internal void BeginHold(Hand hand)
        {
            Holder = hand;
            _wasKinematic = Body.isKinematic;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }

            Body.isKinematic = true;

            if (_hasOverride)
                _offset = _override;
            else if (gripProfile != null)
                _offset = gripProfile.Get(hand.Side);
            else
                _offset = GripOffset.FromPoses(hand.transform.position.ToNumerics(), hand.transform.rotation.ToNumerics(),
                    transform.position.ToNumerics(), transform.rotation.ToNumerics());

            Follow();
            OnGrabbed(hand);
            Grabbed?.Invoke(this, hand);
        }

        internal void EndHold(Hand hand, Vector3 velocity)
        {
            Holder = null;
            Body.isKinematic = _wasKinematic;
            if (!Body.isKinematic)
                Body.linearVelocity = velocity;

            OnReleased(hand, velocity);
            Released?.Invoke(this, hand, velocity);
        }

        /// <summary>손 자세 × 오프셋으로 즉시 옮긴다.</summary>
        public void Follow()
        {
            if (Holder == null)
                return;

            _offset.Apply(Holder.transform.position.ToNumerics(), Holder.transform.rotation.ToNumerics(),
                out System.Numerics.Vector3 position, out System.Numerics.Quaternion rotation);
            transform.SetPositionAndRotation(position.ToUnity(), rotation.ToUnity());
        }

        protected virtual void OnGrabbed(Hand hand)
        {
        }

        protected virtual void OnReleased(Hand hand, Vector3 velocity)
        {
        }

        protected virtual void LateUpdate()
        {
            // 리그가 손 자세를 넣은(Update) 뒤에 따라간다.
            if (IsHeld)
                Follow();
        }

        protected virtual void OnDisable()
        {
            if (Holder != null)
                Holder.Release(throwing: false);
        }
    }
}
