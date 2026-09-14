using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Cartridge;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>카트리지 하나. 슬롯독 근처에서 놓으면 꽂히고, 다시 잡으면 빠진다.</summary>
    public sealed class CartridgeItem : Grabbable
    {
        public string Id { get; internal set; }
        public Color Color { get; internal set; }
        public Transform Home { get; internal set; }
        internal CartridgeStation Station;

        protected override void OnGrabbed(Hand hand) => Station?.OnCartridgeGrabbed(this);

        protected override void OnReleased(Hand hand, Vector3 velocity) => Station?.OnCartridgeReleased(this);
    }

    /// <summary>
    /// 카트리지 슬롯독. 부스에서 카트리지를 뽑아 등 뒤 슬롯독(3칸)에 꽂으면 그 슬롯의 스킬이 활성화된다.
    /// 사용하면 슬롯 둘레 점 고리가 쿨타임 진행률만큼 다시 찬다. 쿨타임은 카트리지에 붙어 있어
    /// 뽑아서 다른 슬롯에 꽂아도 초기화되지 않는다(Core <see cref="CartridgeSlots"/>).
    /// </summary>
    public sealed class CartridgeStation : MonoBehaviour
    {
        const int SlotCount = 3;
        const int RingDots = 10;
        const float SnapDistance = 0.14f;

        static readonly (string Id, Color Color, float Cooldown)[] Catalog =
        {
            ("hook", new Color(0.35f, 0.6f, 1f), 3f),
            ("boost", new Color(1f, 0.55f, 0.25f), 5f),
            ("cube", new Color(0.35f, 0.85f, 0.55f), 2f),
        };

        readonly Transform[] _dock = new Transform[SlotCount];
        readonly Renderer[][] _rings = new Renderer[SlotCount][];
        readonly List<CartridgeItem> _items = new List<CartridgeItem>();

        PlayerRig _rig;
        CartridgeSlots _slots;
        Transform _booth;
        Text _status;

        public CartridgeSlots Slots => _slots;
        public IReadOnlyList<CartridgeItem> Items => _items;
        public Transform GetDockSlot(int index) => _dock[index];

        public event Action<int, string> Used;

        void Awake()
        {
            _slots = new CartridgeSlots(SlotCount, new UnityClock());
            BuildBooth();
        }

        public void Configure(PlayerRig rig)
        {
            _rig = rig;
            BuildDock();
            for (int i = 0; i < SlotCount; i++)
            {
                int slot = i;
                PokeButton.Create(_booth, $"Use{slot}", $"USE {slot + 1}", new Vector3((slot - 1) * 0.2f, 1.02f, -0.12f),
                    StationKit.Surface, rig).Pressed += () => Use(slot);
            }
        }

        public CartridgeUseResult Use(int slot)
        {
            string id = _slots.Get(slot);
            double cooldown = id != null ? CooldownOf(id) : 0;
            CartridgeUseResult result = _slots.Use(slot, cooldown);
            if (result == CartridgeUseResult.Used)
                Used?.Invoke(slot, id);
            UpdateStatus($"slot {slot + 1}: {result}");
            return result;
        }

        void Update()
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (_rings[slot] == null)
                    continue;

                string id = _slots.Get(slot);
                int lit = id == null ? 0 : Mathf.RoundToInt((float)_slots.CooldownProgress(slot) * RingDots);
                Color color = id == null ? StationKit.Surface : ColorOf(id);
                for (int d = 0; d < RingDots; d++)
                    _rings[slot][d].material.color = d < lit ? color : new Color(0.18f, 0.19f, 0.22f);
            }
        }

        internal void OnCartridgeGrabbed(CartridgeItem item)
        {
            int slot = _slots.FindSlot(item.Id);
            if (slot >= 0)
            {
                _slots.Eject(slot);
                UpdateStatus($"ejected {item.Id} from slot {slot + 1}");
            }

            item.transform.SetParent(transform, true);
        }

        internal void OnCartridgeReleased(CartridgeItem item)
        {
            int best = -1;
            float bestDistance = SnapDistance;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (_dock[slot] == null || _slots.Get(slot) != null)
                    continue;
                float distance = Vector3.Distance(item.transform.position, _dock[slot].position);
                if (distance <= bestDistance)
                {
                    best = slot;
                    bestDistance = distance;
                }
            }

            if (best >= 0 && _slots.Insert(best, item.Id) == CartridgeInsertResult.Inserted)
            {
                item.transform.SetParent(_dock[best], false);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                UpdateStatus($"inserted {item.Id} into slot {best + 1}");
                return;
            }

            item.transform.SetParent(_booth, false);
            item.transform.SetPositionAndRotation(item.Home.position, item.Home.rotation);
        }

        static float CooldownOf(string id)
        {
            foreach ((string Id, Color Color, float Cooldown) entry in Catalog)
                if (entry.Id == id)
                    return entry.Cooldown;
            return 1f;
        }

        static Color ColorOf(string id)
        {
            foreach ((string Id, Color Color, float Cooldown) entry in Catalog)
                if (entry.Id == id)
                    return entry.Color;
            return Color.white;
        }

        void UpdateStatus(string message)
        {
            if (_status == null)
                return;
            var slots = new List<string>();
            for (int i = 0; i < SlotCount; i++)
                slots.Add($"{i + 1}:{_slots.Get(i) ?? "-"}");
            _status.text = $"{message}\n{string.Join("   ", slots)}";
        }

        void BuildBooth()
        {
            _booth = new GameObject("Booth").transform;
            _booth.SetParent(transform, false);
            _booth.localPosition = new Vector3(0f, 0f, 0.55f);
            StationKit.Primitive(PrimitiveType.Cube, _booth, "Table", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 0.4f), new Color(0.2f, 0.21f, 0.25f));
            StationKit.Label(_booth, "Title", new Vector3(0f, 1.55f, 0.1f), new Vector2(1f, 0.1f), 60).text = "<b>CARTRIDGE BOOTH</b>";
            _status = StationKit.Label(_booth, "Status", new Vector3(0f, 1.38f, 0.1f), new Vector2(1.2f, 0.14f), 34,
                TextAnchor.MiddleCenter, StationKit.Muted);

            for (int i = 0; i < Catalog.Length; i++)
            {
                (string id, Color color, float _) = Catalog[i];
                var home = new GameObject($"Home_{id}").transform;
                home.SetParent(_booth, false);
                home.localPosition = new Vector3((i - 1) * 0.22f, 0.98f, 0.02f);

                var go = new GameObject($"Cartridge_{id}");
                go.transform.SetParent(_booth, false);
                go.transform.SetPositionAndRotation(home.position, home.rotation);
                StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Body", Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), color);
                StationKit.Primitive(PrimitiveType.Cube, go.transform, "Label", new Vector3(0f, 0f, 0.031f), new Vector3(0.04f, 0.05f, 0.004f), Color.white);
                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.08f, 0.13f, 0.08f);
                var body = go.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                var item = go.AddComponent<CartridgeItem>();
                item.Id = id;
                item.Color = color;
                item.Home = home;
                item.Station = this;
                _items.Add(item);
            }

            UpdateStatus("take a cartridge and plug it into your back dock");
        }

        void BuildDock()
        {
            var dock = new GameObject("BackDock").transform;
            dock.SetParent(_rig.transform, false);
            dock.localPosition = new Vector3(0f, 1.2f, -0.2f);
            dock.localRotation = Quaternion.Euler(0f, 180f, 0f); // 바깥쪽이 뒤

            StationKit.Primitive(PrimitiveType.Cube, dock, "Plate", Vector3.zero, new Vector3(0.44f, 0.16f, 0.03f), new Color(0.25f, 0.26f, 0.3f));
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var socket = new GameObject($"Slot{slot}").transform;
                socket.SetParent(dock, false);
                socket.localPosition = new Vector3((slot - 1) * 0.14f, 0f, 0.05f);
                _dock[slot] = socket;

                _rings[slot] = new Renderer[RingDots];
                for (int d = 0; d < RingDots; d++)
                {
                    float angle = d * Mathf.PI * 2f / RingDots;
                    _rings[slot][d] = StationKit.Primitive(PrimitiveType.Sphere, socket, $"Ring{d}",
                        new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), -0.03f) * 0.055f, Vector3.one * 0.014f, StationKit.Surface)
                        .GetComponent<Renderer>();
                }
            }
        }
    }
}
