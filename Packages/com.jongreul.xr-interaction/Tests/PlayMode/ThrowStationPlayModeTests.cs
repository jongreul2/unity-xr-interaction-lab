using System;
using System.Collections;
using Jongreul.XrInteraction.Stations;
using Jongreul.XrInteraction.Throw;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jongreul.XrInteraction.Tests
{
    public class ThrowStationPlayModeTests
    {
        const float Fps = 60f;

        StationTestRig _rig;
        GameObject _stationRoot;
        ThrowStation _station;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new StationTestRig((int)Fps);
            _stationRoot = new GameObject("ThrowStation");
            _station = _stationRoot.AddComponent<ThrowStation>();
            _station.Configure(_rig.Rig);
            yield return StationTestRig.Frames(2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_stationRoot);
            _rig.Dispose();
            yield return null;
        }

        static IEnumerator WaitUntil(Func<bool> done, int maxFrames)
        {
            for (int i = 0; i < maxFrames && !done(); i++)
                yield return null;
            Assert.That(done(), Is.True, "시간 초과");
        }

        /// <summary>오른손으로 쥐고 target에 flightTime 뒤 닿는 속도로 휘두르다 놓는다(포물선 역산).</summary>
        IEnumerator Throw(ThrowItem item, Vector3 target, float flightTime)
        {
            yield return _rig.GrabAt(item.transform.position);
            Assert.That(item.IsHeld, Is.True, "잡기");

            Vector3 release = _rig.Root.transform.TransformPoint(new Vector3(0.25f, 1.3f, 0.3f));
            Vector3 velocity = (target - release) / flightTime - 0.5f * Physics.gravity * flightTime;
            const int swingFrames = 10;
            float dt = 1f / Fps;
            Vector3 start = release - velocity * (dt * swingFrames);
            _rig.Simulator.SetHand(HandSide.Right, _rig.Local(start), Quaternion.identity);
            yield return StationTestRig.Frames(10); // 순간이동한 궤적이 추정 구간(0.1초)에서 빠지게

            for (int i = 1; i <= swingFrames; i++)
            {
                _rig.Simulator.SetHand(HandSide.Right, _rig.Local(start + velocity * (dt * i)), Quaternion.identity);
                if (i == swingFrames)
                    _rig.Simulator.SetGrip(HandSide.Right, 0f); // 움직이는 프레임에 놓는다
                yield return null;
            }

            Assert.That(item.IsHeld, Is.False, "놓기");
            Assert.That(_station.LastThrowSpeed, Is.EqualTo(velocity.magnitude).Within(0.2f), "손 궤적 속도가 실린다");
        }

        /// <summary>쥔 채 상자 안으로 옮겨 가만히 놓는다.</summary>
        IEnumerator CarryIntoSlot(ThrowItem item)
        {
            yield return _rig.GrabAt(item.transform.position);
            Assert.That(item.IsHeld, Is.True, $"{item.Id} 잡기");
            yield return _rig.MoveRightTo(_station.SlotWorldCenter);
            Assert.That(_station.Box.Contains(item.Id), Is.False, "쥔 채로는 세지 않는다");
            yield return StationTestRig.Frames(10); // 손 속도가 0이 된 뒤 놓는다
            yield return _rig.ReleaseRight();
        }

        IEnumerator PokeBell()
        {
            _rig.Simulator.SetHand(HandSide.Left, _rig.Local(_station.Bell.position), Quaternion.identity);
            yield return StationTestRig.Frames(3);
            _rig.Simulator.SetHand(HandSide.Left, _rig.Local(_station.Bell.position + new Vector3(-0.3f, 0f, -0.2f)),
                Quaternion.identity);
            yield return StationTestRig.Frames(2);
        }

        [UnityTest]
        public IEnumerator ThrownIntoSlot_IsCounted_ThenFreezesInside()
        {
            ThrowItem ball = _station.Find("ball-1");

            yield return Throw(ball, _station.SlotWorldCenter, 0.7f);
            yield return WaitUntil(() => _station.IsFrozen(ball), 400);

            Assert.That(_station.IsInSlot(ball), Is.True);
            Assert.That(_station.Box.Contains("ball-1"), Is.True);
            Assert.That(ball.Body.isKinematic, Is.True, "멈추면 물리를 끄고 그 자리에 고정");
        }

        [UnityTest]
        public IEnumerator ThrownOntoFloor_Freezes_ThenReturnsToRack()
        {
            ThrowItem ball = _station.Find("ball-2");
            Vector3 target = _station.transform.TransformPoint(new Vector3(0.7f, 0.05f, 1.2f));

            yield return Throw(ball, target, 0.6f);
            yield return WaitUntil(() => _station.IsFrozen(ball), 400);
            Assert.That(_station.Box.Contains("ball-2"), Is.False);

            yield return WaitUntil(() => _station.IsHome(ball), 120);
        }

        [UnityTest]
        public IEnumerator WrongKind_IsRejected_AndPushedOut()
        {
            ThrowItem junk = _station.Find("junk-1");

            yield return CarryIntoSlot(junk);

            Assert.That(_station.Rejected, Is.EqualTo(1));
            Assert.That(_station.Box.Count, Is.EqualTo(0));
            yield return WaitUntil(() => !_station.IsInSlot(junk), 120);
        }

        [UnityTest]
        public IEnumerator Bell_WithQuotaMet_Delivers_AndSendsBallsHome()
        {
            foreach (string id in new[] { "ball-1", "ball-2", "ball-3" })
                yield return CarryIntoSlot(_station.Find(id));
            Assert.That(_station.Box.Count, Is.EqualTo(3));

            yield return PokeBell();

            Assert.That(_station.LastSubmit, Is.EqualTo(SubmitResult.Delivered));
            Assert.That(_station.Box.Count, Is.EqualTo(0));
            Assert.That(_station.Box.TotalDelivered, Is.EqualTo(3));
            foreach (string id in new[] { "ball-1", "ball-2", "ball-3" })
                Assert.That(_station.IsHome(_station.Find(id)), Is.True, id);
        }

        [UnityTest]
        public IEnumerator Bell_BelowQuota_KeepsWhatIsInside()
        {
            yield return CarryIntoSlot(_station.Find("ball-4"));

            yield return PokeBell();

            Assert.That(_station.LastSubmit, Is.EqualTo(SubmitResult.NotEnough));
            Assert.That(_station.Box.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TakingABallBackOut_Uncounts()
        {
            ThrowItem ball = _station.Find("ball-1");
            yield return CarryIntoSlot(ball);
            Assert.That(_station.Box.Contains("ball-1"), Is.True);

            yield return _rig.GrabAt(ball.transform.position);

            Assert.That(ball.IsHeld, Is.True);
            Assert.That(_station.Box.Contains("ball-1"), Is.False);
        }
    }
}
