using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Equip;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>진열대 미니어처. 잡아서 신체 소켓 근처에서 놓으면 장착 요청이 되고, 놓은 뒤에는 진열 칸으로 돌아간다.</summary>
    public sealed class EquipPreviewItem : Grabbable
    {
        public EquipItem Item { get; internal set; }
        public Transform Home { get; internal set; }
        internal EquipStation Station;

        protected override void OnReleased(Hand hand, Vector3 velocity)
        {
            Station?.OnPreviewReleased(this);
            ReturnHome();
        }

        internal void ReturnHome()
        {
            if (Home == null)
                return;
            transform.SetPositionAndRotation(Home.position, Home.rotation);
        }
    }

    /// <summary>
    /// 그랩 장착 옷장 + 마네킹 미러.
    /// - 진열대: 카탈로그를 카테고리별 격자로 자동 진열(<see cref="ShelfLayout"/>), 가까이 가야 켜진다(<see cref="ProximityToggle"/>).
    /// - 장착: 미니어처를 쥐고 신체 소켓에 대면 고리가 뜨고, 놓으면 <see cref="EquipState"/>에 장착.
    ///   다른 아이템이 있으면 교체 확인(3초 안에 한 번 더 놓기).
    /// - 마네킹: 장착·해제 이벤트를 받아 같은 모양을 입는다. 서버 저장도 같은 이벤트에 붙일 자리.
    /// - 내 착용물은 1인칭 카메라에서 숨긴다(얼굴 소켓의 안경이 시야를 가리지 않게).
    /// </summary>
    public sealed class EquipStation : MonoBehaviour
    {
        /// <summary>내 착용물 레이어. 머리 카메라의 컬링 마스크에서 뺀다.</summary>
        public const int SelfWearableLayer = 30;

        const float ReplaceConfirmSeconds = 3f;
        const int Columns = 3;
        const int Rows = 2;

        readonly List<EquipSocket> _playerSockets = new List<EquipSocket>();
        readonly Dictionary<string, Transform> _mannequinSockets = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> _playerWorn = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> _mannequinWorn = new Dictionary<string, GameObject>();
        readonly List<EquipPreviewItem> _previews = new List<EquipPreviewItem>();
        readonly List<PokeButton> _tabs = new List<PokeButton>();
        readonly ProximityToggle _shelfToggle = new ProximityToggle(1.6f, 2.0f);

        PlayerRig _rig;
        EquipState _state;
        Transform _shelf;
        Transform _shelfItems;
        Text _prompt;
        Text _status;
        string _category = "hats";
        string _pendingReplaceItem;
        float _pendingUntil;
        float _promptUntil;

        public EquipState State => _state;
        public IReadOnlyList<EquipSocket> PlayerSockets => _playerSockets;
        public IReadOnlyList<EquipPreviewItem> Previews => _previews;
        public bool ShelfActive => _shelfItems != null && _shelfItems.gameObject.activeSelf;
        public string PromptText => _prompt != null ? _prompt.text : string.Empty;
        public bool HasMannequinWearable(string slot) => _mannequinWorn.ContainsKey(slot);

        public event Action<string> Prompted;

        public void Configure(PlayerRig rig)
        {
            _rig = rig;
            CreatePlayerSockets();
            BuildButtons();
            if (_rig.Head != null && _rig.Head.TryGetComponent(out Camera headCamera))
                headCamera.cullingMask &= ~(1 << SelfWearableLayer);
        }

        void Awake()
        {
            _state = new EquipState(Wearables.Slots);
            _state.Equipped += OnEquipped;
            _state.Unequipped += OnUnequipped;
            BuildShelf();
            BuildMannequin();
            ShowCategory(_category);
        }

        void Update()
        {
            if (_rig == null)
                return;

            UpdateShelfVisibility();
            UpdateHighlights();

            if (_prompt.text.Length > 0 && Time.time > _promptUntil)
                _prompt.text = string.Empty;
        }

        /// <summary>진열 카테고리 전환.</summary>
        public void ShowCategory(string category)
        {
            _category = category;
            foreach (EquipPreviewItem preview in _previews)
            {
                if (preview.Holder != null)
                    preview.Holder.Release(throwing: false);
                Destroy(preview.Home.gameObject);
                Destroy(preview.gameObject);
            }

            _previews.Clear();
            foreach (ShelfCell cell in ShelfLayout.Arrange(Wearables.Catalog, category, Columns, Rows, 0.24f, 0.22f))
                _previews.Add(CreatePreview(cell));

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].SetColor(Wearables.Categories[i] == category ? StationKit.Accent : StationKit.Surface);
            RefreshStatus();
        }

        internal void OnPreviewReleased(EquipPreviewItem preview)
        {
            var candidates = new List<SocketCandidate>(_playerSockets.Count);
            foreach (EquipSocket socket in _playerSockets)
                candidates.Add(socket.ToCandidate());

            if (SocketSensor.FindBest(candidates, preview.Item.Slot, preview.transform.position.ToNumerics()) < 0)
                return;

            bool confirm = _pendingReplaceItem == preview.Item.Id && Time.time <= _pendingUntil;
            EquipResult result = _state.TryEquip(preview.Item, confirm);
            _pendingReplaceItem = null;

            switch (result.Outcome)
            {
                case EquipOutcome.NeedsReplaceConfirm:
                    _pendingReplaceItem = preview.Item.Id;
                    _pendingUntil = Time.time + ReplaceConfirmSeconds;
                    Prompt($"Replace {result.Previous.Id} with {preview.Item.Id}?  Drop again within {ReplaceConfirmSeconds:0}s");
                    break;
                case EquipOutcome.Equipped:
                    Prompt($"Equipped {preview.Item.Id}");
                    break;
                case EquipOutcome.Replaced:
                    Prompt($"Replaced {result.Previous.Id} → {preview.Item.Id}");
                    break;
                case EquipOutcome.AlreadyEquipped:
                    Prompt($"{preview.Item.Id} is already on");
                    break;
            }
        }

        #region 장착 이벤트 → 표시

        void OnEquipped(string slot, EquipItem item)
        {
            EquipSocket socket = _playerSockets.Find(s => s.Slot == slot);
            if (socket != null)
            {
                GameObject worn = Wearables.Create(item.Id, socket.transform);
                Wearables.SetLayerRecursively(worn, SelfWearableLayer);
                _playerWorn[slot] = worn;
            }

            if (_mannequinSockets.TryGetValue(slot, out Transform mannequinSocket))
                _mannequinWorn[slot] = Wearables.Create(item.Id, mannequinSocket);

            RefreshStatus();
        }

        void OnUnequipped(string slot, EquipItem item)
        {
            if (_playerWorn.TryGetValue(slot, out GameObject worn))
            {
                Destroy(worn);
                _playerWorn.Remove(slot);
            }

            if (_mannequinWorn.TryGetValue(slot, out GameObject mirror))
            {
                Destroy(mirror);
                _mannequinWorn.Remove(slot);
            }

            RefreshStatus();
        }

        #endregion

        #region 매 프레임

        void UpdateShelfVisibility()
        {
            if (_rig.Head == null)
                return;

            Vector3 head = _rig.Head.position;
            Vector3 shelf = _shelf.position;
            float distance = Vector2.Distance(new Vector2(head.x, head.z), new Vector2(shelf.x, shelf.z));
            bool active = _shelfToggle.Update(distance);
            if (_shelfItems.gameObject.activeSelf != active)
                _shelfItems.gameObject.SetActive(active);
        }

        void UpdateHighlights()
        {
            EquipPreviewItem held = null;
            foreach (EquipPreviewItem preview in _previews)
            {
                if (preview.IsHeld)
                {
                    held = preview;
                    break;
                }
            }

            for (int i = 0; i < _playerSockets.Count; i++)
            {
                EquipSocket socket = _playerSockets[i];
                bool show = held != null && socket.Slot == held.Item.Slot &&
                            SocketSensor.Score(socket.ToCandidate(), held.transform.position.ToNumerics()) >= 0f;
                bool occupied = _state.Get(socket.Slot) != null && _state.Get(socket.Slot).Id != held?.Item.Id;
                socket.SetHighlight(show, occupied ? StationKit.Warn : StationKit.Good);
            }
        }

        void Prompt(string message)
        {
            _prompt.text = message;
            _promptUntil = Time.time + ReplaceConfirmSeconds + 0.5f;
            Prompted?.Invoke(message);
        }

        void RefreshStatus()
        {
            if (_status == null)
                return;
            var lines = new List<string>();
            foreach (string slot in Wearables.Slots)
                lines.Add($"{slot}: {_state.Get(slot)?.Id ?? "-"}");
            _status.text = string.Join("   ", lines);
        }

        #endregion

        #region 조립

        void CreatePlayerSockets()
        {
            Transform head = _rig.Head != null ? _rig.Head : _rig.transform;
            // 머리 위(바깥쪽 = 위), 얼굴 앞(바깥쪽 = 앞), 등 뒤(바깥쪽 = 뒤)
            _playerSockets.Add(EquipSocket.Create(head, Wearables.Head, new Vector3(0, 0.12f, 0), Quaternion.Euler(-90, 0, 0)));
            _playerSockets.Add(EquipSocket.Create(head, Wearables.Face, new Vector3(0, -0.02f, 0.1f), Quaternion.identity));
            _playerSockets.Add(EquipSocket.Create(head, Wearables.Back, new Vector3(0, -0.35f, -0.16f), Quaternion.Euler(0, 180, 0), 0.25f));
        }

        void BuildShelf()
        {
            _shelf = new GameObject("Shelf").transform;
            _shelf.SetParent(transform, false);
            _shelf.localPosition = new Vector3(0, 0, 0.55f);

            StationKit.Primitive(PrimitiveType.Cube, _shelf, "Back", new Vector3(0, 1.2f, 0.06f), new Vector3(0.9f, 0.62f, 0.03f), StationKit.Surface);
            StationKit.Primitive(PrimitiveType.Cube, _shelf, "Base", new Vector3(0, 0.44f, 0.02f), new Vector3(0.9f, 0.88f, 0.12f), new Color(0.2f, 0.21f, 0.25f));
            StationKit.Label(_shelf, "Title", new Vector3(0, 1.78f, 0.04f), new Vector2(0.9f, 0.08f), 56).text = "<b>WARDROBE</b>";
            _prompt = StationKit.Label(_shelf, "Prompt", new Vector3(0, 1.66f, 0.02f), new Vector2(1.2f, 0.07f), 36, TextAnchor.MiddleCenter, StationKit.Warn);
            _status = StationKit.Label(_shelf, "Status", new Vector3(0, 0.83f, -0.05f), new Vector2(0.9f, 0.05f), 30, TextAnchor.MiddleCenter, StationKit.Muted);

            _shelfItems = new GameObject("Items").transform;
            _shelfItems.SetParent(_shelf, false);
            _shelfItems.localPosition = new Vector3(0, 1.33f, -0.02f);
        }

        void BuildButtons()
        {
            for (int i = 0; i < Wearables.Categories.Length; i++)
            {
                string category = Wearables.Categories[i];
                PokeButton tab = PokeButton.Create(_shelf, $"Tab_{category}", category.ToUpperInvariant(),
                    new Vector3((i - 1) * 0.2f, 1.56f, -0.02f), StationKit.Surface, _rig);
                tab.Pressed += () => ShowCategory(category);
                _tabs.Add(tab);
            }

            for (int i = 0; i < Wearables.Slots.Length; i++)
            {
                string slot = Wearables.Slots[i];
                PokeButton remove = PokeButton.Create(_shelf, $"Remove_{slot}", $"TAKE OFF {slot.ToUpperInvariant()}",
                    new Vector3((i - 1) * 0.28f, 0.72f, -0.06f), new Color(0.35f, 0.25f, 0.28f), _rig);
                remove.Pressed += () => _state.Unequip(slot);
            }

            ShowCategory(_category);
        }

        EquipPreviewItem CreatePreview(ShelfCell cell)
        {
            var home = new GameObject($"Cell_{cell.Item.Id}").transform;
            home.SetParent(_shelfItems, false);
            home.localPosition = cell.LocalPosition.ToUnity();
            home.localRotation = Quaternion.identity;

            var go = new GameObject($"Preview_{cell.Item.Id}");
            go.transform.SetParent(_shelfItems, false);
            go.transform.SetPositionAndRotation(home.position, home.rotation);
            GameObject model = Wearables.Create(cell.Item.Id, go.transform, 0.7f);
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.18f, 0.16f, 0.12f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var preview = go.AddComponent<EquipPreviewItem>();
            preview.Item = cell.Item;
            preview.Station = this;
            preview.Home = home;
            return preview;
        }

        void BuildMannequin()
        {
            var mannequin = new GameObject("Mannequin").transform;
            mannequin.SetParent(transform, false);
            mannequin.localPosition = new Vector3(0.85f, 0, 0.95f);
            mannequin.localRotation = Quaternion.Euler(0, 200, 0);

            var skin = new Color(0.78f, 0.78f, 0.82f);
            StationKit.Primitive(PrimitiveType.Capsule, mannequin, "Body", new Vector3(0, 1.0f, 0), new Vector3(0.36f, 0.5f, 0.24f), skin);
            Transform head = StationKit.Primitive(PrimitiveType.Sphere, mannequin, "Head", new Vector3(0, 1.62f, 0), Vector3.one * 0.22f, skin).transform;
            StationKit.Primitive(PrimitiveType.Cylinder, mannequin, "Stand", new Vector3(0, 0.25f, 0), new Vector3(0.08f, 0.25f, 0.08f), StationKit.Surface);
            StationKit.Label(mannequin, "Title", new Vector3(0, 2.0f, 0), new Vector2(0.6f, 0.06f), 40, TextAnchor.MiddleCenter, StationKit.Muted)
                .text = "MIRROR";
            head.name = "Head";

            _mannequinSockets[Wearables.Head] = Socket(mannequin, "Head", new Vector3(0, 1.74f, 0), Quaternion.Euler(-90, 0, 0));
            _mannequinSockets[Wearables.Face] = Socket(mannequin, "Face", new Vector3(0, 1.6f, 0.11f), Quaternion.identity);
            _mannequinSockets[Wearables.Back] = Socket(mannequin, "Back", new Vector3(0, 1.28f, -0.14f), Quaternion.Euler(0, 180, 0));
        }

        static Transform Socket(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var socket = new GameObject($"Socket_{name}").transform;
            socket.SetParent(parent, false);
            socket.SetLocalPositionAndRotation(position, rotation);
            return socket;
        }

        #endregion
    }
}
