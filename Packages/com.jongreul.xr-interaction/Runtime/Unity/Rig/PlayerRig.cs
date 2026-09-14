using UnityEngine;

namespace Jongreul.XrInteraction
{
    /// <summary>
    /// 플레이어 리그: 트래킹 원점. 매 프레임 입력 공급원에서 머리·손 자세를 받아 자식 트랜스폼에 넣는다.
    /// 헤드셋이 연결돼 있으면 실기기 입력, 아니면 데스크톱 시뮬레이터.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerRig : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Hand leftHand;
        [SerializeField] Hand rightHand;
        [SerializeField] DesktopHandSimulator simulator;
        [SerializeField] bool preferXRDevice = true;

        readonly XRDeviceInputSource _xr = new XRDeviceInputSource();

        public Transform Head => head;
        public Hand LeftHand => leftHand;
        public Hand RightHand => rightHand;
        public DesktopHandSimulator Simulator => simulator;
        public IXRInputSource ActiveSource { get; private set; }

        /// <summary>false면 헤드셋이 있어도 시뮬레이터만 쓴다(자동 테스트·촬영).</summary>
        public bool PreferXRDevice
        {
            get => preferXRDevice;
            set => preferXRDevice = value;
        }

        public Hand GetHand(HandSide side) => side == HandSide.Left ? leftHand : rightHand;

        /// <summary>코드로 조립하는 씬·테스트용.</summary>
        public void Configure(Transform headTransform, Hand left, Hand right, DesktopHandSimulator desktopSimulator,
            bool useXRDeviceWhenAvailable = true)
        {
            head = headTransform;
            leftHand = left;
            rightHand = right;
            simulator = desktopSimulator;
            preferXRDevice = useXRDeviceWhenAvailable;
        }

        void Update()
        {
            ActiveSource = preferXRDevice && _xr.IsAvailable ? _xr : (IXRInputSource)simulator;
            if (ActiveSource == null)
                return;

            if (head != null && ActiveSource.TryGetHead(out Vector3 headPosition, out Quaternion headRotation))
                head.SetLocalPositionAndRotation(headPosition, headRotation);

            Apply(leftHand, HandSide.Left);
            Apply(rightHand, HandSide.Right);
        }

        void Apply(Hand hand, HandSide side)
        {
            if (hand == null)
                return;

            if (ActiveSource.TryGetHand(side, out HandInputFrame frame) && frame.Tracked)
            {
                hand.transform.SetLocalPositionAndRotation(frame.Position, frame.Rotation);
                hand.SetInput(frame.Grip, frame.Trigger, frame.Primary);
            }
            else
            {
                // 추적을 잃으면 쥐고 있던 것을 놓는다(허공에 붙은 채 남지 않게).
                hand.SetInput(0f, 0f, false);
            }
        }
    }
}
