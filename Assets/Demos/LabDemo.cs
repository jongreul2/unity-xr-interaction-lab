using Jongreul.XrInteraction.Stations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Demos
{
    /// <summary>
    /// 상호작용 랩 데모 부트스트랩. 씬에는 이 컴포넌트 하나만 두고 환경·리그·스테이션을 코드로 만든다.
    /// 스테이션은 X축으로 나란히 서 있고 숫자 키로 그 앞에 순간이동한다.
    /// 헤드셋이 있으면 실기기 입력, 없으면 데스크톱 시뮬레이터로 조작한다.
    /// </summary>
    public sealed class LabDemo : MonoBehaviour
    {
        public static readonly Color Backdrop = new Color(0.07f, 0.08f, 0.1f);

        public const float StrikeSpotX = 0f;
        public const float EquipSpotX = 3.0f;
        public const float ShieldSpotX = 6.0f;
        public const float CartridgeSpotX = 9.0f;
        public const float ZiplineSpotX = 12.0f;

        /// <summary>순간이동 지점. 짚라인은 출발대 위에 선다.</summary>
        static readonly Vector3[] Spots =
        {
            new Vector3(StrikeSpotX, 0f, 0f),
            new Vector3(EquipSpotX, 0f, 0f),
            new Vector3(ShieldSpotX, 0f, 0f),
            new Vector3(CartridgeSpotX, 0f, 0f),
            new Vector3(ZiplineSpotX, ZiplineStation.PlatformHeight, 0f),
        };

        static readonly Key[] SpotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        public PlayerRig Rig { get; private set; }
        public DesktopHandSimulator Simulator { get; private set; }
        public Camera HeadCamera { get; private set; }
        public StrikeStation Strike { get; private set; }
        public EquipStation Equip { get; private set; }
        public ShieldStation Shield { get; private set; }
        public CartridgeStation Cartridge { get; private set; }
        public ZiplineStation Zipline { get; private set; }

        void Awake()
        {
            BuildEnvironment();
            BuildRig();

            Strike = CreateStation<StrikeStation>("StrikeStation", new Vector3(StrikeSpotX, 0f, 0.6f));
            Strike.Configure(Rig);
            Equip = CreateStation<EquipStation>("EquipStation", new Vector3(EquipSpotX, 0f, 0f));
            Equip.Configure(Rig);
            Shield = CreateStation<ShieldStation>("ShieldStation", new Vector3(ShieldSpotX, 0f, 0f));
            Shield.Configure(Rig);
            Cartridge = CreateStation<CartridgeStation>("CartridgeStation", new Vector3(CartridgeSpotX, 0f, 0f));
            Cartridge.Configure(Rig);
            Zipline = CreateStation<ZiplineStation>("ZiplineStation", new Vector3(ZiplineSpotX, 0f, 0f));
            Zipline.Configure(Rig);

            BuildHelp();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !Simulator.KeyboardAndMouse)
                return;

            for (int i = 0; i < SpotKeys.Length; i++)
            {
                if (keyboard[SpotKeys[i]].wasPressedThisFrame)
                {
                    TeleportTo(i);
                    break;
                }
            }
        }

        /// <summary>스테이션 앞으로 리그를 옮긴다. 짚라인을 타는 중이면 끊는다.</summary>
        public void TeleportTo(int station)
        {
            station = Mathf.Clamp(station, 0, Spots.Length - 1);
            if (Zipline != null)
                Zipline.ResetStation();
            Rig.transform.position = Spots[station];
        }

        T CreateStation<T>(string stationName, Vector3 position) where T : Component
        {
            var go = new GameObject(stationName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            return go.AddComponent<T>();
        }

        void BuildEnvironment()
        {
            StationKit.Primitive(PrimitiveType.Plane, transform, "Floor", new Vector3(9f, 0f, 0.8f), new Vector3(2.2f, 1f, 0.55f),
                new Color(0.15f, 0.16f, 0.19f), collider: true);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        void BuildRig()
        {
            var root = new GameObject("PlayerRig");
            root.transform.SetParent(transform, false);
            Simulator = root.AddComponent<DesktopHandSimulator>();

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.tag = "MainCamera";
            HeadCamera = head.AddComponent<Camera>();
            HeadCamera.nearClipPlane = 0.02f;
            HeadCamera.fieldOfView = 70f;
            HeadCamera.clearFlags = CameraClearFlags.SolidColor;
            HeadCamera.backgroundColor = Backdrop;

            Hand left = CreateHand(root.transform, HandSide.Left, new Color(0.45f, 0.62f, 1f));
            Hand right = CreateHand(root.transform, HandSide.Right, new Color(1f, 0.62f, 0.45f));

            Rig = root.AddComponent<PlayerRig>();
            Rig.Configure(head.transform, left, right, Simulator);

            // 관찰자 카메라용 아바타. 1인칭 카메라에서는 내 착용물 레이어로 숨긴다.
            var skin = new Color(0.62f, 0.66f, 0.74f);
            GameObject avatarHead = StationKit.Primitive(PrimitiveType.Sphere, head.transform, "AvatarHead",
                Vector3.zero, Vector3.one * 0.2f, skin);
            GameObject body = StationKit.Primitive(PrimitiveType.Capsule, root.transform, "AvatarBody",
                new Vector3(0f, 1.0f, 0f), new Vector3(0.34f, 0.45f, 0.22f), skin);
            Wearables.SetLayerRecursively(avatarHead, EquipStation.SelfWearableLayer);
            Wearables.SetLayerRecursively(body, EquipStation.SelfWearableLayer);
            root.AddComponent<AvatarBody>().Bind(head.transform, body.transform);
        }

        static Hand CreateHand(Transform rig, HandSide side, Color color)
        {
            var go = new GameObject($"{side}Hand");
            go.transform.SetParent(rig, false);
            var hand = go.AddComponent<Hand>();
            hand.Side = side;

            GameObject palm = StationKit.Primitive(PrimitiveType.Sphere, go.transform, "Palm", Vector3.zero,
                Vector3.one * 0.07f, color);
            go.AddComponent<HandVisual>().Bind(hand, palm.GetComponent<Renderer>(), color);
            return hand;
        }

        void BuildHelp()
        {
            var canvasGo = new GameObject("Help", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = HeadCamera;
            canvas.planeDistance = 0.3f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.offsetMin = new Vector2(20, 12);
            rect.offsetMax = new Vector2(-20, 44);
            var text = textGo.AddComponent<Text>();
            text.font = StationKit.Font;
            text.fontSize = 16;
            text.color = StationKit.Muted;
            text.alignment = TextAnchor.LowerLeft;
            text.raycastTarget = false;
            text.text = "1-5: station   Mouse: move hand   Q (hold): left hand   Wheel: depth   R+Mouse: rotate   LMB: grip   Arrows: look";
        }
    }

    /// <summary>손 표시: 쥐면 노랑, 무언가를 잡고 있으면 초록, 잡을 수 있는 게 가까우면 밝게.</summary>
    public sealed class HandVisual : MonoBehaviour
    {
        Hand _hand;
        Renderer _renderer;
        Color _baseColor;

        public void Bind(Hand hand, Renderer palm, Color baseColor)
        {
            _hand = hand;
            _renderer = palm;
            _baseColor = baseColor;
        }

        void LateUpdate()
        {
            if (_hand == null || _renderer == null)
                return;

            Color color = _hand.Held != null ? StationKit.Good
                : _hand.Grip > 0.5f ? StationKit.Warn
                : _hand.Hovered != null ? Color.Lerp(_baseColor, Color.white, 0.5f)
                : _baseColor;
            _renderer.material.color = color;
        }
    }

    /// <summary>아바타 몸통을 머리 아래에 두고 머리의 좌우 방향만 따라 돌린다.</summary>
    public sealed class AvatarBody : MonoBehaviour
    {
        Transform _head;
        Transform _body;

        public void Bind(Transform head, Transform body)
        {
            _head = head;
            _body = body;
        }

        void LateUpdate()
        {
            if (_head == null || _body == null)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = transform.forward;
            _body.SetPositionAndRotation(_head.position + Vector3.down * 0.62f, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
