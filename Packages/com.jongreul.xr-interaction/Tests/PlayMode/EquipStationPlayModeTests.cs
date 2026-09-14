using System.Collections;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Tests
{
    /// <summary>시뮬레이터 손으로 미니어처를 잡아 머리 소켓에 가져가 장착·교체·해제하는 흐름.</summary>
    public class EquipStationPlayModeTests
    {
        GameObject _rigRoot;
        GameObject _stationRoot;
        DesktopHandSimulator _simulator;
        PlayerRig _rig;
        EquipStation _station;
        int _previousCaptureFramerate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;

            _rigRoot = new GameObject("Rig");
            _simulator = _rigRoot.AddComponent<DesktopHandSimulator>();
            _simulator.KeyboardAndMouse = false;
            var head = new GameObject("Head");
            head.transform.SetParent(_rigRoot.transform, false);
            head.AddComponent<Camera>();
            Hand left = CreateHand(HandSide.Left);
            Hand right = CreateHand(HandSide.Right);
            _rig = _rigRoot.AddComponent<PlayerRig>();
            _rig.Configure(head.transform, left, right, _simulator, useXRDeviceWhenAvailable: false);

            _stationRoot = new GameObject("EquipStation");
            _station = _stationRoot.AddComponent<EquipStation>();
            yield return null;
            _station.Configure(_rig);
            _simulator.SetHand(HandSide.Left, new Vector3(-0.8f, 0.9f, -0.3f), Quaternion.identity);
            _simulator.SetHand(HandSide.Right, new Vector3(0.8f, 0.9f, -0.3f), Quaternion.identity);
            yield return Frames(3);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_rigRoot);
            Object.Destroy(_stationRoot);
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

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        EquipPreviewItem Preview(string id) =>
            new System.Collections.Generic.List<EquipPreviewItem>(_station.Previews).Find(p => p.Item.Id == id);

        Vector3 RigLocal(Vector3 world) => _rigRoot.transform.InverseTransformPoint(world);

        /// <summary>미니어처를 잡아 목표(리그 로컬)로 옮긴 뒤 놓는다.</summary>
        IEnumerator GrabAndDrop(string itemId, Vector3 dropLocal)
        {
            EquipPreviewItem preview = Preview(itemId);
            Assert.That(preview, Is.Not.Null, itemId);
            _simulator.SetHand(HandSide.Right, RigLocal(preview.transform.position), Quaternion.identity);
            yield return Frames(3);
            _simulator.SetGrip(HandSide.Right, 1f);
            yield return Frames(3);
            Assert.That(preview.IsHeld, Is.True, $"{itemId} 잡기");

            _simulator.SetHand(HandSide.Right, dropLocal, Quaternion.identity);
            yield return Frames(3);
            _simulator.SetGrip(HandSide.Right, 0f);
            yield return Frames(3);
            _simulator.SetHand(HandSide.Right, new Vector3(0.8f, 0.9f, -0.3f), Quaternion.identity);
            yield return Frames(3);
        }

        Vector3 AboveHead => _rig.Head.localPosition + new Vector3(0f, 0.2f, 0f);

        [UnityTest]
        public IEnumerator DropAboveHead_Equips_AndMannequinMirrors()
        {
            yield return GrabAndDrop("cap", AboveHead);

            Assert.That(_station.State.Get("head")?.Id, Is.EqualTo("cap"));
            Assert.That(_station.HasMannequinWearable("head"), Is.True);
            Assert.That(Vector3.Distance(Preview("cap").transform.position, Preview("cap").Home.position), Is.LessThan(1e-3f),
                "놓은 미니어처는 진열 칸으로 돌아간다");
        }

        [UnityTest]
        public IEnumerator DropFromBelowHeadSocket_DoesNotEquip()
        {
            // 머리 소켓 가까이지만 아래쪽(목) 방향 — 각도 판정으로 거부
            yield return GrabAndDrop("cap", _rig.Head.localPosition + new Vector3(0f, 0.04f, 0f));

            Assert.That(_station.State.Get("head"), Is.Null);
        }

        [UnityTest]
        public IEnumerator DifferentHat_NeedsConfirm_SecondDropReplaces()
        {
            yield return GrabAndDrop("cap", AboveHead);
            yield return GrabAndDrop("crown", AboveHead);

            Assert.That(_station.State.Get("head")?.Id, Is.EqualTo("cap"), "첫 번째 놓기는 확인 요청만");
            StringAssert.Contains("Replace", _station.PromptText);

            yield return GrabAndDrop("crown", AboveHead);

            Assert.That(_station.State.Get("head")?.Id, Is.EqualTo("crown"));
        }

        [UnityTest]
        public IEnumerator TakeOffButton_Unequips_AndMannequinFollows()
        {
            yield return GrabAndDrop("cap", AboveHead);
            Transform button = _stationRoot.transform.Find("Shelf/Remove_head");
            Assert.That(button, Is.Not.Null);

            _simulator.SetHand(HandSide.Right, RigLocal(button.position), Quaternion.identity);
            yield return Frames(3);

            Assert.That(_station.State.Get("head"), Is.Null);
            Assert.That(_station.HasMannequinWearable("head"), Is.False);
        }

        [UnityTest]
        public IEnumerator Shelf_TurnsOffWhenFarAway()
        {
            Assert.That(_station.ShelfActive, Is.True);

            _rigRoot.transform.position = new Vector3(0f, 0f, -3f);
            yield return Frames(2);

            Assert.That(_station.ShelfActive, Is.False);
        }
    }
}
