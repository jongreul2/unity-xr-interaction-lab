using System;
using Jongreul.XrInteraction.Zipline;
using UnityEngine;
using UnityEngine.UI;
using NVector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>짚라인 손잡이. 잡혀도 손을 따라가지 않는다 — 반대로 리그가 손잡이를 따라간다.</summary>
    public sealed class ZiplineHandle : Grabbable
    {
        internal ZiplineStation Station;

        /// <summary>탑승이 끝나 출발점으로 돌아가는 중. 이때는 잡을 수 없다.</summary>
        public bool Returning { get; internal set; }

        public override bool CanBeGrabbedBy(Hand hand) => base.CanBeGrabbedBy(hand) && !Returning;

        protected override void OnGrabbed(Hand hand) => Station?.OnHandleGrabbed(hand);

        protected override void OnReleased(Hand hand, Vector3 velocity) => Station?.OnHandleReleased(hand);

        protected override void LateUpdate()
        {
            // Grabbable 기본 동작(손 따라가기)을 끈다. 위치는 스테이션이 케이블 위에 둔다.
        }
    }

    /// <summary>
    /// 짚라인. 머리 위 손잡이를 잡으면 리그가 케이블을 따라 내려가고, 양손을 놓으면 케이블 방향 속도를 안고 떨어진다.
    /// 종점에 닿으면 자동으로 놓는다. 탑승 규칙(속도·감속·손 바꿔 잡기 여유)은 Core <see cref="ZiplineRide"/>.
    /// 리그는 손이 아니라 손잡이에 고정한다 — 손을 케이블에 붙이면 팔의 흔들림이 곧 시야의 흔들림이 된다.
    /// </summary>
    public sealed class ZiplineStation : MonoBehaviour
    {
        /// <summary>출발대 높이(m). 리그를 이 높이에 세우면 손잡이가 머리 위 손 뻗는 높이에 온다.</summary>
        public const float PlatformHeight = 1.0f;

        public const float RestDistance = 0.3f;

        static readonly Vector3 StartAnchor = new Vector3(0f, 3.0f, 0.1f);
        static readonly Vector3 EndAnchor = new Vector3(0f, 2.3f, 9.35f);
        const float Sag = 0.15f;
        const float HangBelowCable = 0.12f;
        const float ReturnDelay = 0.8f;
        const float ReturnSpeed = 4f;

        [SerializeField] ZiplineSettings settings = new ZiplineSettings();

        readonly RaycastHit[] _groundHits = new RaycastHit[16];

        PlayerRig _rig;
        ZiplinePath _path;
        ZiplineRide _ride;
        ZiplineHandle _handle;
        float _handleDistance;
        Vector3 _rigOffset;
        Vector3 _fallVelocity;
        bool _ending;
        float _returnWait;
        Text _status;
        Text _speedLabel;

        public ZiplineRide Ride => _ride;
        public ZiplineHandle Handle => _handle;
        public ZiplineSettings Settings => settings;
        public float HandleDistance => _handleDistance;
        public bool IsFalling { get; private set; }
        public ZiplineEndReason LastEnd { get; private set; }
        public int Rides { get; private set; }

        public event Action<ZiplineEndReason> RideEnded;
        public event Action Landed;

        void Awake()
        {
            _path = ZiplinePath.Sagging(StartAnchor.ToNumerics(), EndAnchor.ToNumerics(), Sag);
            _ride = new ZiplineRide(_path, settings);
            _ride.Ended += OnRideEnded;
            Build();
            _handleDistance = RestDistance;
            PlaceHandle();
        }

        public void Configure(PlayerRig rig) => _rig = rig;

        /// <summary>순간이동 등으로 자리를 뜰 때: 탑승·낙하를 끊고 손잡이를 출발점에 둔다.</summary>
        public void ResetStation()
        {
            _ride.Cancel();
            IsFalling = false;
            _fallVelocity = Vector3.zero;
            _handle.Returning = false;
            _handleDistance = RestDistance;
            PlaceHandle();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_ride.IsRiding)
            {
                _ride.Step(dt); // 끝나면 OnRideEnded가 이 안에서 불린다
                if (_ride.IsRiding)
                {
                    _handleDistance = _ride.Distance;
                    PlaceHandle();
                    if (_rig != null)
                        _rig.transform.position = _handle.transform.position + _rigOffset;
                }
            }
            else if (_handle.Returning)
            {
                ReturnHandle(dt);
            }

            if (IsFalling)
                Fall(dt);

            UpdateLabels();
        }

        internal void OnHandleGrabbed(Hand hand)
        {
            bool starting = !_ride.IsRiding;
            _ride.Grab(hand.Side, _handleDistance);
            if (!starting)
                return; // 다른 손으로 바꿔 잡음: 속도·위치 유지

            Rides++;
            IsFalling = false;
            if (_rig != null)
                _rigOffset = _rig.transform.position - _handle.transform.position;
        }

        internal void OnHandleReleased(Hand hand)
        {
            if (!_ending)
                _ride.Release(hand.Side);
        }

        void OnRideEnded(ZiplineEndReason reason, NVector3 exitVelocity)
        {
            LastEnd = reason;
            _handleDistance = _ride.Distance;
            PlaceHandle();

            if (_handle.Holder != null)
            {
                _ending = true;
                _handle.Holder.Release(throwing: false);
                _ending = false;
            }

            if (reason != ZiplineEndReason.Cancelled && _rig != null)
            {
                _rig.transform.position = _handle.transform.position + _rigOffset;
                _fallVelocity = transform.TransformDirection(exitVelocity.ToUnity());
                IsFalling = true;
            }

            _handle.Returning = true;
            _returnWait = ReturnDelay;
            RideEnded?.Invoke(reason);
        }

        void ReturnHandle(float dt)
        {
            if (_returnWait > 0f)
            {
                _returnWait -= dt;
                return;
            }

            _handleDistance = Mathf.MoveTowards(_handleDistance, RestDistance, ReturnSpeed * dt);
            PlaceHandle();
            if (Mathf.Abs(_handleDistance - RestDistance) < 1e-4f)
                _handle.Returning = false;
        }

        void PlaceHandle()
        {
            Vector3 cable = transform.TransformPoint(_path.Evaluate(_handleDistance).ToUnity());
            Vector3 tangent = transform.TransformDirection(_path.TangentAt(_handleDistance).ToUnity());
            Vector3 flat = Vector3.ProjectOnPlane(tangent, Vector3.up);
            Quaternion rotation = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat, Vector3.up) : transform.rotation;
            _handle.transform.SetPositionAndRotation(cable + Vector3.down * HangBelowCable, rotation);
        }

        #region 낙하

        void Fall(float dt)
        {
            if (_rig == null)
            {
                IsFalling = false;
                return;
            }

            Vector3 position = _rig.transform.position;
            float ground = GroundHeight(position);
            _fallVelocity += Physics.gravity * dt;
            position += _fallVelocity * dt;
            if (position.y <= ground)
            {
                // 착지하면 수평 속도도 멈춘다(미끄러지지 않게).
                position.y = ground;
                IsFalling = false;
                _fallVelocity = Vector3.zero;
                _rig.transform.position = position;
                Landed?.Invoke();
                return;
            }

            _rig.transform.position = position;
        }

        /// <summary>발밑에서 가장 높은 바닥. 리그 자신과 잡을 수 있는 물체는 바닥으로 치지 않는다.</summary>
        float GroundHeight(Vector3 feet)
        {
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * 0.05f, Vector3.down, _groundHits, 50f, ~0,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            float best = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _groundHits[i].collider;
                if (hit.GetComponentInParent<Grabbable>() != null || hit.transform.IsChildOf(_rig.transform))
                    continue;
                if (_groundHits[i].point.y > best)
                {
                    best = _groundHits[i].point.y;
                    found = true;
                }
            }

            return found ? best : transform.position.y;
        }

        #endregion

        #region 표시

        void UpdateLabels()
        {
            _speedLabel.text = _ride.IsRiding ? $"{_ride.Speed:0.0} m/s   {_ride.Progress * 100f:0}%" : string.Empty;
            _status.text = _ride.IsRiding ? $"riding · {_ride.HandCount} hand(s) on the handle"
                : IsFalling ? "dropped"
                : _handle.Returning ? $"{Describe(LastEnd)} — handle returning"
                : Rides > 0 ? $"{Describe(LastEnd)}. grab the handle again"
                : "reach up and grab the handle";
        }

        static string Describe(ZiplineEndReason reason)
        {
            switch (reason)
            {
                case ZiplineEndReason.ReachedEnd: return "arrived";
                case ZiplineEndReason.Released: return "let go midway";
                case ZiplineEndReason.Cancelled: return "cancelled";
                default: return "ready";
            }
        }

        void Build()
        {
            var wood = new Color(0.36f, 0.3f, 0.26f);
            var steel = new Color(0.55f, 0.58f, 0.64f);

            StationKit.Primitive(PrimitiveType.Cube, transform, "Platform", new Vector3(0f, PlatformHeight * 0.5f, 0f),
                new Vector3(1.6f, PlatformHeight, 1.6f), wood, collider: true);
            StationKit.Primitive(PrimitiveType.Plane, transform, "LandingStrip", new Vector3(0f, 0.002f, 6.5f),
                new Vector3(0.3f, 1f, 1.4f), new Color(0.18f, 0.2f, 0.24f), collider: true);
            for (int z = 1; z <= 13; z++)
            {
                StationKit.Primitive(PrimitiveType.Cube, transform, $"Tick{z}", new Vector3(0f, 0.006f, z),
                    new Vector3(2.4f, 0.004f, 0.03f), new Color(0.3f, 0.33f, 0.38f));
            }

            BuildGantry("StartGantry", StartAnchor, steel);
            BuildGantry("EndGantry", EndAnchor, steel);

            for (int i = 0; i < _path.PointCount - 1; i++)
            {
                Vector3 a = _path.GetPoint(i).ToUnity();
                Vector3 b = _path.GetPoint(i + 1).ToUnity();
                GameObject segment = StationKit.Primitive(PrimitiveType.Cylinder, transform, $"Cable{i}", (a + b) * 0.5f,
                    new Vector3(0.02f, Vector3.Distance(a, b) * 0.5f, 0.02f), new Color(0.75f, 0.77f, 0.8f));
                segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            }

            StationKit.Label(transform, "Title", new Vector3(-0.8f, 2.58f, 1.3f), new Vector2(0.8f, 0.1f), 60).text =
                "<b>ZIPLINE</b>";
            _status = StationKit.Label(transform, "Status", new Vector3(-0.8f, 2.46f, 1.3f), new Vector2(0.9f, 0.14f), 30,
                TextAnchor.UpperCenter, StationKit.Muted);

            BuildHandle(steel);
        }

        void BuildGantry(string gantryName, Vector3 anchor, Color color)
        {
            var root = new GameObject(gantryName).transform;
            root.SetParent(transform, false);
            float top = anchor.y + 0.06f;
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                StationKit.Primitive(PrimitiveType.Cylinder, root, "Post", new Vector3(x, top * 0.5f, anchor.z),
                    new Vector3(0.08f, top * 0.5f, 0.08f), color);
            }

            StationKit.Primitive(PrimitiveType.Cube, root, "Beam", new Vector3(0f, top, anchor.z),
                new Vector3(1.6f, 0.08f, 0.08f), color);
        }

        void BuildHandle(Color steel)
        {
            var go = new GameObject("ZiplineHandle");
            go.transform.SetParent(transform, false);

            GameObject bar = StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Bar", Vector3.zero,
                new Vector3(0.035f, 0.17f, 0.035f), new Color(0.95f, 0.55f, 0.2f));
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Strap", new Vector3(0f, HangBelowCable * 0.5f, 0f),
                new Vector3(0.012f, HangBelowCable * 0.5f, 0.012f), steel);
            StationKit.Primitive(PrimitiveType.Cube, go.transform, "Trolley", new Vector3(0f, HangBelowCable, 0f),
                new Vector3(0.07f, 0.06f, 0.14f), steel);
            // 매달린 사람 눈앞(손잡이 앞쪽 아래)에 띄운다.
            _speedLabel = StationKit.Label(go.transform, "Speed", new Vector3(0f, -0.1f, 0.9f), new Vector2(0.6f, 0.08f), 50,
                TextAnchor.MiddleCenter, StationKit.Warn);

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.36f, 0.07f, 0.07f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            _handle = go.AddComponent<ZiplineHandle>();
            _handle.Station = this;
        }

        #endregion
    }
}
