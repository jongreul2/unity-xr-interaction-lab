using System.Collections;
using UnityEngine;

namespace Jongreul.XrInteraction.Tests
{
    /// <summary>PlayMode 테스트용 리그(시뮬레이터 + 머리 + 양손). 전역 captureFramerate는 Dispose에서 되돌린다.</summary>
    sealed class StationTestRig
    {
        readonly int _previousCaptureFramerate;

        public StationTestRig(int fps = 60)
        {
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = fps;

            Root = new GameObject("Rig");
            Simulator = Root.AddComponent<DesktopHandSimulator>();
            Simulator.KeyboardAndMouse = false;
            var head = new GameObject("Head");
            head.transform.SetParent(Root.transform, false);
            head.AddComponent<Camera>();
            Left = CreateHand(HandSide.Left);
            Right = CreateHand(HandSide.Right);
            Rig = Root.AddComponent<PlayerRig>();
            Rig.Configure(head.transform, Left, Right, Simulator, useXRDeviceWhenAvailable: false);
            Simulator.SetHand(HandSide.Left, new Vector3(-0.8f, 0.9f, -0.3f), Quaternion.identity);
            Simulator.SetHand(HandSide.Right, new Vector3(0.8f, 0.9f, -0.3f), Quaternion.identity);
        }

        public GameObject Root { get; }
        public DesktopHandSimulator Simulator { get; }
        public PlayerRig Rig { get; }
        public Hand Left { get; }
        public Hand Right { get; }

        Hand CreateHand(HandSide side)
        {
            var go = new GameObject($"{side}Hand");
            go.transform.SetParent(Root.transform, false);
            var hand = go.AddComponent<Hand>();
            hand.Side = side;
            return hand;
        }

        public Vector3 Local(Vector3 world) => Root.transform.InverseTransformPoint(world);

        public static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        /// <summary>오른손을 target(월드)으로 옮겨 잡는다.</summary>
        public IEnumerator GrabAt(Vector3 world)
        {
            Simulator.SetHand(HandSide.Right, Local(world), Quaternion.identity);
            yield return Frames(3);
            Simulator.SetGrip(HandSide.Right, 1f);
            yield return Frames(3);
        }

        public IEnumerator MoveRightTo(Vector3 world)
        {
            Simulator.SetHand(HandSide.Right, Local(world), Quaternion.identity);
            yield return Frames(3);
        }

        public IEnumerator ReleaseRight()
        {
            Simulator.SetGrip(HandSide.Right, 0f);
            yield return Frames(3);
        }

        public void Dispose()
        {
            Object.Destroy(Root);
            Time.captureFramerate = _previousCaptureFramerate;
        }
    }
}
