using System.Collections;
using Jongreul.XrInteraction.Grip;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Tests
{
    public class GripAlignStationPlayModeTests
    {
        StationTestRig _rig;
        GameObject _stationRoot;
        GripAlignStation _station;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new StationTestRig();
            _stationRoot = new GameObject("GripAlignStation");
            _station = _stationRoot.AddComponent<GripAlignStation>();
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

        static void ExpectPose(GripTool tool, Hand hand, GripOffset offset)
        {
            offset.Apply(hand.transform.position.ToNumerics(), hand.transform.rotation.ToNumerics(),
                out System.Numerics.Vector3 position, out System.Numerics.Quaternion rotation);
            Assert.That(Vector3.Distance(tool.transform.position, position.ToUnity()), Is.LessThan(1e-3f), "위치");
            Assert.That(Quaternion.Angle(tool.transform.rotation, rotation.ToUnity()), Is.LessThan(0.1f), "회전");
        }

        IEnumerator GrabLeft(Vector3 world)
        {
            _rig.Simulator.SetHand(HandSide.Left, _rig.Local(world), Quaternion.identity);
            yield return StationTestRig.Frames(3);
            _rig.Simulator.SetGrip(HandSide.Left, 1f);
            yield return StationTestRig.Frames(3);
        }

        IEnumerator ReleaseLeft()
        {
            _rig.Simulator.SetGrip(HandSide.Left, 0f);
            yield return StationTestRig.Frames(3);
        }

        /// <summary>오른손으로 새 도구(Paddle)를 쥐고 왼손으로 ROLL 슬라이더를 roll까지 끈 뒤 슬라이더를 놓는다.</summary>
        IEnumerator HoldPaddleAndDragRollTo(float roll)
        {
            GripTool paddle = _station.FindTool("Paddle");
            yield return _rig.GrabAt(paddle.transform.position);
            Assert.That(paddle.Holder, Is.SameAs(_rig.Right), "도구 잡기");

            WorldSlider slider = _station.GetSlider(GripAxis.Roll);
            yield return GrabLeft(slider.Knob.transform.position);
            Assert.That(slider.Knob.Holder, Is.SameAs(_rig.Left), "슬라이더 잡기");
            _rig.Simulator.SetHand(HandSide.Left, _rig.Local(slider.WorldPointFor(roll)), Quaternion.identity);
            yield return StationTestRig.Frames(3);
            yield return ReleaseLeft();
        }

        [UnityTest]
        public IEnumerator Tool_IsHeldAtProfileOffset_AndLeftHandUsesTheMirror()
        {
            GripTool hammer = _station.FindTool("Hammer");
            yield return _rig.GrabAt(hammer.transform.position);
            Assert.That(hammer.Holder, Is.SameAs(_rig.Right));
            ExpectPose(hammer, _rig.Right, hammer.GripProfile.Get(HandSide.Right));

            yield return _rig.ReleaseRight();
            Assert.That(Vector3.Distance(hammer.transform.position, hammer.Home.position), Is.LessThan(1e-4f), "놓으면 제자리");

            yield return GrabLeft(hammer.transform.position);
            Assert.That(hammer.Holder, Is.SameAs(_rig.Left));
            ExpectPose(hammer, _rig.Left, hammer.GripProfile.Get(HandSide.Right).MirrorX());
        }

        [UnityTest]
        public IEnumerator DraggingSliderWithOtherHand_ChangesHeldToolLive()
        {
            GripTool paddle = _station.FindTool("Paddle");
            WorldSlider roll = _station.GetSlider(GripAxis.Roll);

            yield return _rig.GrabAt(paddle.transform.position);
            Quaternion before = paddle.transform.rotation;
            Assert.That(roll.Value, Is.EqualTo(70f).Within(1f), "잡으면 현재 오프셋을 불러온다");

            yield return HoldPaddleAndDragRollTo(0f);

            Assert.That(roll.Value, Is.EqualTo(0f).Within(1f));
            Assert.That(_station.HasUnsavedChanges, Is.True);
            ExpectPose(paddle, _rig.Right, _station.CurrentValue());
            Assert.That(Quaternion.Angle(before, paddle.transform.rotation), Is.GreaterThan(60f));
        }

        [UnityTest]
        public IEnumerator Save_WritesTheHoldingHandsProfile()
        {
            yield return HoldPaddleAndDragRollTo(0f);
            GripTool paddle = _station.FindTool("Paddle");

            _station.SaveButton.Press();

            Assert.That(_station.HasUnsavedChanges, Is.False);
            GripOffset saved = paddle.GripProfile.Get(HandSide.Right);
            Assert.That(Quaternion.Angle(saved.Rotation.ToUnity(), _station.CurrentValue().Rotation.ToUnity()),
                Is.LessThan(0.1f));
            Assert.That(paddle.GripProfile.MirrorForLeft, Is.True, "오른손 저장은 왼손 거울상을 유지");
        }

        [UnityTest]
        public IEnumerator Revert_GoesBackToTheProfile()
        {
            yield return HoldPaddleAndDragRollTo(0f);
            GripTool paddle = _station.FindTool("Paddle");

            _station.RevertButton.Press();
            yield return StationTestRig.Frames(2);

            Assert.That(_station.GetSlider(GripAxis.Roll).Value, Is.EqualTo(70f).Within(1f));
            Assert.That(_station.HasUnsavedChanges, Is.False);
            ExpectPose(paddle, _rig.Right, paddle.GripProfile.Get(HandSide.Right));
        }

        [UnityTest]
        public IEnumerator ReleasingWithoutSaving_DiscardsTheTuning()
        {
            yield return HoldPaddleAndDragRollTo(0f);
            GripTool paddle = _station.FindTool("Paddle");

            yield return _rig.ReleaseRight();
            Assert.That(Vector3.Distance(paddle.transform.position, paddle.Home.position), Is.LessThan(1e-4f));

            yield return _rig.GrabAt(paddle.transform.position);
            ExpectPose(paddle, _rig.Right, paddle.GripProfile.Get(HandSide.Right));
            Assert.That(_station.GetSlider(GripAxis.Roll).Value, Is.EqualTo(70f).Within(1f), "프로필은 그대로");
        }
    }
}
