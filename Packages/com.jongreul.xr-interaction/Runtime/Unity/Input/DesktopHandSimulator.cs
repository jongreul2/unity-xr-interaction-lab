using UnityEngine;
using UnityEngine.InputSystem;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// 헤드셋 없이 손을 움직이는 데스크톱 시뮬레이터. 실기기와 같은 <see cref="IXRInputSource"/>로 들어간다.
    /// 조작: 마우스 = 손 이동(Q를 누르고 있으면 왼손) · 휠 = 앞뒤 · R + 마우스 = 손 회전 ·
    /// 왼쪽 버튼 = 그립 · 오른쪽 버튼 = 트리거 · 스페이스 = 주 버튼 · 방향키 = 시선.
    /// 테스트·캡처는 <see cref="KeyboardAndMouse"/>를 끄고 Set* 메서드로 조작한다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DesktopHandSimulator : MonoBehaviour, IXRInputSource
    {
        struct HandState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Grip;
            public float Trigger;
            public bool Primary;
        }

        [SerializeField] bool keyboardAndMouse = true;
        [SerializeField] float headHeight = 1.6f;
        [SerializeField] float moveMetersPerPixel = 0.0012f;
        [SerializeField] float depthMetersPerScroll = 0.0006f;
        [SerializeField] float rotateDegreesPerPixel = 0.3f;
        [SerializeField] float lookDegreesPerSecond = 90f;

        readonly HandState[] _hands = new HandState[2];
        Vector3 _headPosition;
        float _yaw;
        float _pitch;

        public bool KeyboardAndMouse
        {
            get => keyboardAndMouse;
            set => keyboardAndMouse = value;
        }

        public HandSide ActiveHand { get; private set; } = HandSide.Right;

        public Quaternion HeadRotation => Quaternion.Euler(_pitch, _yaw, 0f);

        void Awake()
        {
            _headPosition = new Vector3(0f, headHeight, 0f);
            _hands[(int)HandSide.Left] = new HandState
            {
                Position = new Vector3(-0.2f, headHeight - 0.3f, 0.35f),
                Rotation = Quaternion.identity,
            };
            _hands[(int)HandSide.Right] = new HandState
            {
                Position = new Vector3(0.2f, headHeight - 0.3f, 0.35f),
                Rotation = Quaternion.identity,
            };
        }

        #region 스크립트 조작(테스트·캡처)

        public void SetHead(Vector3 localPosition, float yawDegrees, float pitchDegrees)
        {
            _headPosition = localPosition;
            _yaw = yawDegrees;
            _pitch = pitchDegrees;
        }

        public void SetHand(HandSide side, Vector3 localPosition, Quaternion localRotation)
        {
            _hands[(int)side].Position = localPosition;
            _hands[(int)side].Rotation = localRotation;
        }

        public Vector3 GetHandPosition(HandSide side) => _hands[(int)side].Position;

        public void SetGrip(HandSide side, float value) => _hands[(int)side].Grip = Mathf.Clamp01(value);

        public void SetTrigger(HandSide side, float value) => _hands[(int)side].Trigger = Mathf.Clamp01(value);

        public void SetPrimary(HandSide side, bool pressed) => _hands[(int)side].Primary = pressed;

        #endregion

        void Update()
        {
            if (!keyboardAndMouse)
                return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            float look = lookDegreesPerSecond * Time.deltaTime;
            if (keyboard.leftArrowKey.isPressed) _yaw -= look;
            if (keyboard.rightArrowKey.isPressed) _yaw += look;
            if (keyboard.upArrowKey.isPressed) _pitch = Mathf.Max(-80f, _pitch - look);
            if (keyboard.downArrowKey.isPressed) _pitch = Mathf.Min(80f, _pitch + look);

            ActiveHand = keyboard.qKey.isPressed ? HandSide.Left : HandSide.Right;
            ref HandState hand = ref _hands[(int)ActiveHand];

            Vector2 delta = mouse.delta.ReadValue();
            Quaternion head = HeadRotation;
            if (keyboard.rKey.isPressed)
            {
                hand.Rotation = Quaternion.AngleAxis(delta.x * rotateDegreesPerPixel, head * Vector3.up) *
                                Quaternion.AngleAxis(-delta.y * rotateDegreesPerPixel, head * Vector3.right) *
                                hand.Rotation;
            }
            else
            {
                hand.Position += (head * Vector3.right * delta.x + head * Vector3.up * delta.y) * moveMetersPerPixel;
            }

            hand.Position += head * Vector3.forward * (mouse.scroll.ReadValue().y * depthMetersPerScroll);
            hand.Grip = mouse.leftButton.isPressed ? 1f : 0f;
            hand.Trigger = mouse.rightButton.isPressed ? 1f : 0f;
            hand.Primary = keyboard.spaceKey.isPressed;
        }

        public bool TryGetHead(out Vector3 position, out Quaternion rotation)
        {
            position = _headPosition;
            rotation = HeadRotation;
            return true;
        }

        public bool TryGetHand(HandSide side, out HandInputFrame frame)
        {
            HandState hand = _hands[(int)side];
            frame = new HandInputFrame(true, hand.Position, hand.Rotation, hand.Grip, hand.Trigger, hand.Primary);
            return true;
        }
    }
}
