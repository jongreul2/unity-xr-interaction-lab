using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Throw;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>던질 수 있는 물건(공·잡동사니). 종류(Kind)로 제출 슬롯이 받을지 가른다.</summary>
    public sealed class ThrowItem : Grabbable
    {
        internal ThrowStation Station;

        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public Transform Home { get; internal set; }

        protected override void OnGrabbed(Hand hand) => Station?.OnItemGrabbed(this);

        protected override void OnReleased(Hand hand, Vector3 velocity) => Station?.OnItemReleased(this, velocity);
    }

    /// <summary>
    /// 던지기 + 제출 슬롯. 공을 쥐고 휘두르다 놓으면 손 궤적의 최소제곱 속도로 날아가고(<see cref="ThrowVelocityEstimator"/>),
    /// 앞쪽 상자(제출 슬롯)에 들어가면 종류를 확인해 센다. 벨을 치면 모인 것을 한 번에 제출한다(정해진 수 이상일 때만).
    /// 던진 물건은 멈추면(또는 제한 시간이 지나면) 물리를 끄고 그 자리에 고정한다 — 멀티플레이에서 피어마다 정지 위치가
    /// 달라지지 않게 한 번 확정하는 자리. 상자 밖에서 멈춘 물건은 잠시 뒤 받침대로 돌아온다.
    /// </summary>
    public sealed class ThrowStation : MonoBehaviour
    {
        public const string BallKind = "ball";
        public const string JunkKind = "junk";
        public const int Quota = 3;

        const int Capacity = 5;
        const int BallCount = 4;
        const float ReturnDelay = 1.2f;
        const float BellRadius = 0.09f;
        const float RingCooldown = 0.8f;

        static readonly Vector3 SlotFloor = new Vector3(0f, 0f, 2.2f);
        static readonly Vector3 SlotInner = new Vector3(0.54f, 0.47f, 0.54f);
        static readonly Vector3 BellPosition = new Vector3(-0.42f, 1.18f, 0.45f);
        static readonly Color CrateColor = new Color(0.45f, 0.36f, 0.26f);

        sealed class ItemState
        {
            public ThrowItem Item;
            public Renderer[] Renderers;
            public Color Color;
            public readonly SettleDetector Settle = new SettleDetector();
            public bool Inside;
            public bool Frozen;
            public double ReturnAt = -1;
        }

        readonly List<ItemState> _states = new List<ItemState>();
        readonly List<ThrowItem> _items = new List<ThrowItem>();
        readonly bool[] _handInBell = new bool[2];

        SubmissionBox _box;
        PlayerRig _rig;
        Transform _bell;
        Transform _bellCup;
        Renderer[] _rim;
        Text _board;
        Text _status;
        Text _throwText;
        double _lastRing = double.NegativeInfinity;
        float _bellSwing;
        float _flash;
        Color _flashColor;

        public SubmissionBox Box => _box;
        public IReadOnlyList<ThrowItem> Items => _items;
        public Transform Bell => _bell;
        public int Rejected { get; private set; }
        public float LastThrowSpeed { get; private set; }
        public SubmitResult? LastSubmit { get; private set; }

        /// <summary>상자 안 가운데(바닥에서 0.25 m 위)의 월드 위치.</summary>
        public Vector3 SlotWorldCenter => transform.TransformPoint(SlotFloor + Vector3.up * 0.25f);

        public event Action<SubmitResult> BellRung;

        void Awake()
        {
            _box = new SubmissionBox(Capacity, Quota, new[] { BallKind });
            Build();
        }

        public void Configure(PlayerRig rig) => _rig = rig;

        public ThrowItem Find(string id)
        {
            foreach (ThrowItem item in _items)
            {
                if (item.Id == id)
                    return item;
            }

            return null;
        }

        public bool IsFrozen(ThrowItem item) => State(item).Frozen;
        public bool IsInSlot(ThrowItem item) => State(item).Inside;

        public bool IsHome(ThrowItem item) =>
            !item.IsHeld && item.Body.isKinematic && Vector3.Distance(item.transform.position, item.Home.position) < 1e-3f;

        void Update()
        {
            double now = Time.timeAsDouble;
            foreach (ItemState state in _states)
            {
                bool inside = InSlot(state.Item.transform.position);
                if (inside != state.Inside)
                {
                    state.Inside = inside;
                    if (inside)
                        OnEnteredSlot(state);
                    else
                        OnLeftSlot(state);
                }

                if (!state.Item.IsHeld && !state.Frozen &&
                    state.Settle.Update(now, state.Item.Body.linearVelocity.magnitude))
                    Freeze(state, now);

                if (state.ReturnAt >= 0 && now >= state.ReturnAt)
                    ReturnHome(state);
            }

            UpdateBell();
            UpdateVisuals();
        }

        #region 물건

        internal void OnItemGrabbed(ThrowItem item)
        {
            ItemState state = State(item);
            state.Settle.Cancel();
            state.Frozen = false;
            state.ReturnAt = -1;
            Tint(state, 1f);
            if (_box.Remove(item.Id))
                SetStatus($"took {item.Id} back out", StationKit.Muted);
        }

        internal void OnItemReleased(ThrowItem item, Vector3 velocity)
        {
            // 받침대에서는 키네매틱으로 쉬고 있으므로, 놓는 순간 물리를 켜고 손 속도를 싣는다.
            ItemState state = State(item);
            Rigidbody body = item.Body;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            state.Settle.Begin(Time.timeAsDouble);
            LastThrowSpeed = velocity.magnitude;
            if (state.Inside)
                Count(state);
        }

        void OnEnteredSlot(ItemState state)
        {
            // 손에 쥔 채 상자에 넣고 있는 동안은 세지 않는다 — 놓는 순간 센다.
            if (!state.Item.IsHeld)
                Count(state);
        }

        void OnLeftSlot(ItemState state)
        {
            if (_box.Remove(state.Item.Id))
                SetStatus($"{state.Item.Id} bounced out", StationKit.Muted);
        }

        void Count(ItemState state)
        {
            switch (_box.TryAdd(state.Item.Id, state.Item.Kind))
            {
                case SubmitAddResult.Added:
                    Flash(StationKit.Good);
                    SetStatus($"{state.Item.Id} in", StationKit.Good);
                    break;
                case SubmitAddResult.WrongKind:
                    Rejected++;
                    Eject(state);
                    Flash(StationKit.Bad);
                    SetStatus($"rejected: {state.Item.Kind}", StationKit.Bad);
                    break;
                case SubmitAddResult.Full:
                    Eject(state);
                    Flash(StationKit.Warn);
                    SetStatus("slot is full", StationKit.Warn);
                    break;
            }
        }

        /// <summary>받지 않는 물건은 플레이어 쪽으로 튕겨 낸다.</summary>
        void Eject(ItemState state)
        {
            Rigidbody body = state.Item.Body;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = transform.TransformDirection(new Vector3(0.3f, 3.8f, -1.8f));
            state.Frozen = false;
            state.ReturnAt = -1;
            Tint(state, 1f);
            state.Settle.Begin(Time.timeAsDouble);
        }

        void Freeze(ItemState state, double now)
        {
            Rigidbody body = state.Item.Body;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            state.Frozen = true;
            Tint(state, 0.65f);
            if (!state.Inside)
                state.ReturnAt = now + ReturnDelay;
        }

        void ReturnHome(ItemState state)
        {
            Rigidbody body = state.Item.Body;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            state.Item.transform.SetPositionAndRotation(state.Item.Home.position, state.Item.Home.rotation);
            state.Frozen = false;
            state.ReturnAt = -1;
            state.Settle.Cancel();
            Tint(state, 1f);
        }

        bool InSlot(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world) - SlotFloor;
            return Mathf.Abs(local.x) <= SlotInner.x * 0.5f && Mathf.Abs(local.z) <= SlotInner.z * 0.5f &&
                   local.y >= 0f && local.y <= SlotInner.y;
        }

        ItemState State(ThrowItem item)
        {
            foreach (ItemState state in _states)
            {
                if (state.Item == item)
                    return state;
            }

            throw new ArgumentException($"이 스테이션의 물건이 아니다: {item}");
        }

        #endregion

        #region 벨

        void UpdateBell()
        {
            if (_rig != null)
            {
                for (int i = 0; i < _handInBell.Length; i++)
                {
                    Hand hand = _rig.GetHand((HandSide)i);
                    if (hand == null)
                        continue;
                    bool inside = Vector3.Distance(hand.transform.position, _bell.position) <= BellRadius;
                    if (inside && !_handInBell[i])
                        RingBell();
                    _handInBell[i] = inside;
                }
            }

            _bellSwing = Mathf.Max(0f, _bellSwing - Time.deltaTime * 1.5f);
            _bellCup.localRotation = Quaternion.Euler(Mathf.Sin(_bellSwing * 25f) * 22f * _bellSwing, 0f, 0f);
        }

        /// <summary>벨을 친다 = 제출. 연달아 치면 짧은 간격 안의 두 번째는 무시한다.</summary>
        public SubmitResult RingBell()
        {
            double now = Time.timeAsDouble;
            if (LastSubmit.HasValue && now - _lastRing < RingCooldown)
                return LastSubmit.Value;

            _lastRing = now;
            _bellSwing = 1f;
            SubmitResult result = _box.Submit(out Delivery delivery);
            switch (result)
            {
                case SubmitResult.Delivered:
                    foreach (string id in delivery.Items)
                        ReturnHome(State(Find(id)));
                    Flash(StationKit.Good);
                    SetStatus($"delivered {delivery.Count} · round {delivery.Round}", StationKit.Good);
                    break;
                case SubmitResult.NotEnough:
                    Flash(StationKit.Warn);
                    SetStatus($"need {Quota} to submit (have {_box.Count})", StationKit.Warn);
                    break;
                default:
                    SetStatus("slot is empty", StationKit.Muted);
                    break;
            }

            LastSubmit = result;
            BellRung?.Invoke(result);
            return result;
        }

        #endregion

        #region 표시

        void Tint(ItemState state, float brightness)
        {
            Color color = state.Color * brightness;
            color.a = 1f;
            foreach (Renderer renderer in state.Renderers)
                renderer.material.color = color;
        }

        void Flash(Color color)
        {
            _flash = 1f;
            _flashColor = color;
        }

        void SetStatus(string message, Color color)
        {
            _status.text = message;
            _status.color = color;
        }

        void UpdateVisuals()
        {
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 2f);
            Color rim = Color.Lerp(CrateColor, _flashColor, _flash);
            foreach (Renderer renderer in _rim)
                renderer.material.color = rim;

            _board.text = $"<b>SUBMIT SLOT</b>   {_box.Count} / {Quota}" +
                          (_box.TotalDelivered > 0 ? $"   <size=40>delivered {_box.TotalDelivered}</size>" : string.Empty);
            _throwText.text = LastThrowSpeed > 0f
                ? $"last release {LastThrowSpeed:0.0} m/s"
                : "grab a ball, swing, let go";
        }

        #endregion

        #region 조립

        void Build()
        {
            StationKit.Primitive(PrimitiveType.Plane, transform, "Lane", new Vector3(0f, 0.001f, 1.5f),
                new Vector3(0.24f, 1f, 0.4f), new Color(0.17f, 0.19f, 0.23f), collider: true);

            StationKit.Primitive(PrimitiveType.Cube, transform, "Rack", new Vector3(0.45f, 0.45f, 0.35f),
                new Vector3(0.6f, 0.9f, 0.22f), new Color(0.2f, 0.21f, 0.25f), collider: true);
            StationKit.Label(transform, "Title", new Vector3(0.45f, 1.45f, 0.5f), new Vector2(0.8f, 0.08f), 50).text =
                "<b>THROW & SUBMIT</b>";
            _throwText = StationKit.Label(transform, "ThrowInfo", new Vector3(0.45f, 1.34f, 0.5f), new Vector2(0.8f, 0.06f),
                30, TextAnchor.MiddleCenter, StationKit.Muted);

            Color[] palette =
            {
                new Color(1f, 0.55f, 0.25f), new Color(0.35f, 0.75f, 1f), new Color(0.45f, 0.85f, 0.45f),
                new Color(1f, 0.82f, 0.3f),
            };
            for (int i = 0; i < BallCount; i++)
                AddItem($"ball-{i + 1}", BallKind, new Vector3(0.25f + i * 0.1f, 0.95f, 0.35f), palette[i], cube: false);
            AddItem("junk-1", JunkKind, new Vector3(0.67f, 0.95f, 0.35f), new Color(0.5f, 0.5f, 0.52f), cube: true);

            BuildCrate();
            BuildBell();
        }

        void AddItem(string id, string kind, Vector3 homeLocal, Color color, bool cube)
        {
            var home = new GameObject($"Home_{id}").transform;
            home.SetParent(transform, false);
            home.localPosition = homeLocal;

            GameObject go = StationKit.Primitive(cube ? PrimitiveType.Cube : PrimitiveType.Sphere, transform, id, homeLocal,
                Vector3.one * (cube ? 0.09f : 0.1f), color, collider: true);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;
            body.mass = 0.2f;
            body.angularDamping = 2f; // 평평한 바닥에서 끝없이 구르지 않게
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 얇은 상자 벽 관통 방지

            var item = go.AddComponent<ThrowItem>();
            item.Id = id;
            item.Kind = kind;
            item.Home = home;
            item.Station = this;
            _items.Add(item);
            _states.Add(new ItemState { Item = item, Renderers = go.GetComponentsInChildren<Renderer>(), Color = color });
        }

        void BuildCrate()
        {
            var crate = new GameObject("SubmitSlot").transform;
            crate.SetParent(transform, false);
            crate.localPosition = SlotFloor;

            const float height = 0.47f;
            const float thickness = 0.04f;
            const float half = 0.29f;
            StationKit.Primitive(PrimitiveType.Cube, crate, "Floor", new Vector3(0f, 0.02f, 0f),
                new Vector3(0.62f, 0.04f, 0.62f), new Color(0.3f, 0.25f, 0.2f), collider: true);
            _rim = new[]
            {
                Wall(crate, "Front", new Vector3(0f, height * 0.5f, -half), new Vector3(0.62f, height, thickness)),
                Wall(crate, "Back", new Vector3(0f, height * 0.5f, half), new Vector3(0.62f, height, thickness)),
                Wall(crate, "Left", new Vector3(-half, height * 0.5f, 0f), new Vector3(thickness, height, 0.62f)),
                Wall(crate, "Right", new Vector3(half, height * 0.5f, 0f), new Vector3(thickness, height, 0.62f)),
            };

            _board = StationKit.Label(transform, "Board", SlotFloor + new Vector3(0f, 1.25f, 0.4f), new Vector2(1.4f, 0.12f), 70);
            _status = StationKit.Label(transform, "Status", SlotFloor + new Vector3(0f, 1.1f, 0.4f), new Vector2(1.4f, 0.1f), 46,
                TextAnchor.MiddleCenter, StationKit.Muted);
            SetStatus("throw balls in, then ring the bell", StationKit.Muted);
        }

        static Renderer Wall(Transform parent, string wallName, Vector3 position, Vector3 size) =>
            StationKit.Primitive(PrimitiveType.Cube, parent, wallName, position, size, CrateColor, collider: true)
                .GetComponent<Renderer>();

        void BuildBell()
        {
            StationKit.Primitive(PrimitiveType.Cylinder, transform, "BellPost",
                new Vector3(BellPosition.x, (BellPosition.y - 0.06f) * 0.5f, BellPosition.z),
                new Vector3(0.04f, (BellPosition.y - 0.06f) * 0.5f, 0.04f), StationKit.Surface);

            _bell = new GameObject("Bell").transform;
            _bell.SetParent(transform, false);
            _bell.localPosition = BellPosition;
            _bellCup = new GameObject("Cup").transform;
            _bellCup.SetParent(_bell, false);
            StationKit.Primitive(PrimitiveType.Sphere, _bellCup, "Dome", Vector3.zero, new Vector3(0.14f, 0.11f, 0.14f),
                new Color(0.95f, 0.78f, 0.3f));
            StationKit.Primitive(PrimitiveType.Cylinder, _bellCup, "Lip", new Vector3(0f, -0.045f, 0f),
                new Vector3(0.16f, 0.006f, 0.16f), new Color(0.85f, 0.66f, 0.22f));
            StationKit.Label(transform, "BellLabel", BellPosition + new Vector3(0f, 0.13f, 0f), new Vector2(0.4f, 0.05f), 28)
                .text = "BELL = SUBMIT";
        }

        #endregion
    }
}
