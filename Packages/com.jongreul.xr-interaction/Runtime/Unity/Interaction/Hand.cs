using System;
using Jongreul.XrInteraction.Throw;
using UnityEngine;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// 손 하나. 그립을 쥐는 순간 반경 안에서 가장 가까운 <see cref="Grabbable"/>을 잡고, 펴는 순간 놓는다.
    /// 놓을 때 속도는 최근 손 궤적의 최소제곱 기울기(<see cref="ThrowVelocityEstimator"/>).
    /// 쥐고/펴는 기준을 따로 둬(히스테리시스) 트리거 값이 경계에서 흔들려도 잡았다 놓았다 반복하지 않는다.
    /// </summary>
    public sealed class Hand : MonoBehaviour
    {
        const int OverlapCapacity = 16;

        [SerializeField] HandSide side = HandSide.Right;
        [SerializeField] float grabRadius = 0.08f;
        [SerializeField, Range(0f, 1f)] float gripPressThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] float gripReleaseThreshold = 0.35f;
        [SerializeField] LayerMask grabMask = ~0;

        readonly Collider[] _overlap = new Collider[OverlapCapacity];
        readonly ThrowVelocityEstimator _velocity = new ThrowVelocityEstimator();
        bool _gripDown;

        public HandSide Side
        {
            get => side;
            set => side = value;
        }

        public float GrabRadius
        {
            get => grabRadius;
            set => grabRadius = value;
        }

        public float Grip { get; private set; }
        public float Trigger { get; private set; }
        public bool Primary { get; private set; }
        public Grabbable Held { get; private set; }
        public Grabbable Hovered { get; private set; }

        /// <summary>최근 손 속도(월드). 던지기·타격 표시용.</summary>
        public Vector3 Velocity => _velocity.EstimateVelocity().ToUnity();

        public event Action<Hand, Grabbable> Grabbed;
        public event Action<Hand, Grabbable> Released;
        public event Action<Hand, Grabbable> HoverChanged;

        /// <summary>입력 공급원이 매 프레임 넣는다.</summary>
        public void SetInput(float grip, float trigger, bool primary)
        {
            Grip = grip;
            Trigger = trigger;
            Primary = primary;
        }

        void Update()
        {
            _velocity.AddSample(Time.timeAsDouble, transform.position.ToNumerics());

            Grabbable hovered = Held == null ? FindNearest() : null;
            if (hovered != Hovered)
            {
                Hovered = hovered;
                HoverChanged?.Invoke(this, hovered);
            }

            if (!_gripDown && Grip >= gripPressThreshold)
            {
                _gripDown = true;
                if (Held == null && Hovered != null)
                    TryGrab(Hovered);
            }
            else if (_gripDown && Grip <= gripReleaseThreshold)
            {
                _gripDown = false;
                if (Held != null)
                    Release();
            }
        }

        /// <summary>코드에서 직접 잡기. 다른 손이 쥐고 있으면 넘겨받는다.</summary>
        public bool TryGrab(Grabbable target)
        {
            if (target == null || Held != null || !target.CanBeGrabbedBy(this))
                return false;

            if (target.Holder != null)
                target.Holder.Release(throwing: false);

            Held = target;
            target.BeginHold(this);
            Grabbed?.Invoke(this, target);
            return true;
        }

        /// <param name="throwing">true면 손 속도를 물체에 싣는다.</param>
        public void Release(bool throwing = true)
        {
            if (Held == null)
                return;

            Grabbable released = Held;
            Held = null;
            released.EndHold(this, throwing ? Velocity : Vector3.zero);
            Released?.Invoke(this, released);
        }

        Grabbable FindNearest()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, _overlap, grabMask,
                QueryTriggerInteraction.Collide);

            Grabbable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Grabbable candidate = _overlap[i].GetComponentInParent<Grabbable>();
                if (candidate == null || !candidate.CanBeGrabbedBy(this))
                    continue;

                float distance = (_overlap[i].ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        void OnDisable()
        {
            Release(throwing: false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Held != null ? Color.green : new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, grabRadius);
        }
    }
}
