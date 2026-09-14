using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Tests
{
    /// <summary>데스크톱 시뮬레이터로 손을 움직여 잡기·따라오기·던지기·손 바꿔 잡기를 실제 프레임 루프에서 검증한다.</summary>
    public class HandGrabPlayModeTests
    {
        const int CaptureFps = 60;

        GameObject _rigRoot;
        GameObject _cube;
        DesktopHandSimulator _simulator;
        Hand _left;
        Hand _right;
        Grabbable _grabbable;
        int _previousCaptureFramerate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 전역 시간 설정 — TearDown에서 원래 값으로 되돌린다.
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = CaptureFps;

            _rigRoot = new GameObject("Rig");
            _simulator = _rigRoot.AddComponent<DesktopHandSimulator>();
            _simulator.KeyboardAndMouse = false;
            var head = new GameObject("Head").transform;
            head.SetParent(_rigRoot.transform, false);
            _left = CreateHand(HandSide.Left);
            _right = CreateHand(HandSide.Right);
            _rigRoot.AddComponent<PlayerRig>().Configure(head, _left, _right, _simulator, useXRDeviceWhenAvailable: false);

            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.transform.localScale = Vector3.one * 0.1f;
            _cube.transform.position = new Vector3(0.2f, 1.2f, 0.5f);
            _cube.AddComponent<Rigidbody>().useGravity = false;
            _grabbable = _cube.AddComponent<Grabbable>();

            _simulator.SetHand(HandSide.Left, new Vector3(-0.5f, 1.2f, 0.5f), Quaternion.identity);
            _simulator.SetHand(HandSide.Right, new Vector3(0.5f, 1.2f, 0.5f), Quaternion.identity);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_rigRoot);
            Object.Destroy(_cube);
            Time.captureFramerate = _previousCaptureFramerate;
            yield return null;
        }

        Hand CreateHand(HandSide side)
        {
            var go = new GameObject($"{side}Hand");
            go.transform.SetParent(_rigRoot.transform, false);
            var hand = go.AddComponent<Hand>();
            hand.Side = side;
            return hand;
        }

        IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator GripNearCube_GrabsIt_AndCubeFollowsHand()
        {
            _simulator.SetHand(HandSide.Right, new Vector3(0.2f, 1.2f, 0.5f), Quaternion.identity);
            yield return Frames(2);
            _simulator.SetGrip(HandSide.Right, 1f);
            yield return Frames(2);

            Assert.That(_right.Held, Is.SameAs(_grabbable));
            Assert.That(_grabbable.Body.isKinematic, Is.True);

            _simulator.SetHand(HandSide.Right, new Vector3(0.4f, 1.3f, 0.4f), Quaternion.Euler(0, 30, 0));
            yield return Frames(2);

            Assert.That(Vector3.Distance(_cube.transform.position, new Vector3(0.4f, 1.3f, 0.4f)), Is.LessThan(1e-3f));
        }

        [UnityTest]
        public IEnumerator GripFarFromCube_GrabsNothing()
        {
            _simulator.SetGrip(HandSide.Right, 1f);
            yield return Frames(3);

            Assert.That(_right.Held, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReleaseWhileMoving_ThrowsWithHandVelocity()
        {
            var start = new Vector3(0.2f, 1.2f, 0.5f);
            _simulator.SetHand(HandSide.Right, start, Quaternion.identity);
            yield return Frames(2);
            _simulator.SetGrip(HandSide.Right, 1f);
            yield return Frames(2);

            // 2 m/s로 앞으로 휘두른다
            var velocity = new Vector3(0f, 0f, 2f);
            Vector3 position = start;
            for (int i = 0; i < 12; i++)
            {
                position += velocity / CaptureFps;
                _simulator.SetHand(HandSide.Right, position, Quaternion.identity);
                yield return null;
            }

            _simulator.SetGrip(HandSide.Right, 0f);
            yield return null;

            Assert.That(_right.Held, Is.Null);
            Assert.That(_grabbable.Body.isKinematic, Is.False);
            Assert.That(Vector3.Distance(_grabbable.Body.linearVelocity, velocity), Is.LessThan(0.3f),
                _grabbable.Body.linearVelocity.ToString());
        }

        [UnityTest]
        public IEnumerator OtherHand_TakesOver()
        {
            _simulator.SetHand(HandSide.Right, new Vector3(0.2f, 1.2f, 0.5f), Quaternion.identity);
            yield return Frames(2);
            _simulator.SetGrip(HandSide.Right, 1f);
            yield return Frames(2);

            Assert.That(_left.TryGrab(_grabbable), Is.True);

            Assert.That(_left.Held, Is.SameAs(_grabbable));
            Assert.That(_right.Held, Is.Null);
            Assert.That(_grabbable.Holder, Is.SameAs(_left));
        }

        [UnityTest]
        public IEnumerator GripValueHovering_NearThreshold_DoesNotFlicker()
        {
            _simulator.SetHand(HandSide.Right, new Vector3(0.2f, 1.2f, 0.5f), Quaternion.identity);
            yield return Frames(2);
            _simulator.SetGrip(HandSide.Right, 0.7f);
            yield return Frames(2);
            int releases = 0;
            _right.Released += (_, __) => releases++;

            // 쥐는 기준(0.6)과 펴는 기준(0.35) 사이를 오가도 놓지 않는다
            foreach (float value in new[] { 0.55f, 0.45f, 0.58f, 0.4f })
            {
                _simulator.SetGrip(HandSide.Right, value);
                yield return null;
            }

            Assert.That(_right.Held, Is.SameAs(_grabbable));
            Assert.That(releases, Is.EqualTo(0));
        }
    }
}
