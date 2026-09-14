using System.Collections;
using Jongreul.XrInteraction.Stations;
using Jongreul.XrInteraction.Strike;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Tests
{
    /// <summary>시뮬레이터 손으로 타격 패드를 친다. 실제 프레임 루프·리그 로컬 변환·접촉 영역까지 포함한 흐름.</summary>
    public class StrikeStationPlayModeTests
    {
        const int Fps = 90;

        GameObject _rigRoot;
        GameObject _stationRoot;
        DesktopHandSimulator _simulator;
        PlayerRig _rig;
        StrikeStation _station;
        int _previousCaptureFramerate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = Fps;

            _rigRoot = new GameObject("Rig");
            _simulator = _rigRoot.AddComponent<DesktopHandSimulator>();
            _simulator.KeyboardAndMouse = false;
            var head = new GameObject("Head").transform;
            head.SetParent(_rigRoot.transform, false);
            Hand left = CreateHand(HandSide.Left);
            Hand right = CreateHand(HandSide.Right);
            _rig = _rigRoot.AddComponent<PlayerRig>();
            _rig.Configure(head, left, right, _simulator, useXRDeviceWhenAvailable: false);

            _stationRoot = new GameObject("StrikeStation");
            _stationRoot.transform.position = new Vector3(0f, 0f, 0.5f);
            _station = _stationRoot.AddComponent<StrikeStation>();
            _station.Configure(_rig);

            _simulator.SetHand(HandSide.Left, new Vector3(-0.6f, 1.2f, 0f), Quaternion.identity);
            _simulator.SetHand(HandSide.Right, new Vector3(0f, 1.4f, 0.5f), Quaternion.identity);
            yield return null;
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

        IEnumerator MoveRight(Vector3 from, Vector3 to, float seconds)
        {
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (int i = 1; i <= frames; i++)
            {
                _simulator.SetHand(HandSide.Right, Vector3.Lerp(from, to, (float)i / frames), Quaternion.identity);
                yield return null;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DownwardStrike_FillsGauge()
        {
            Vector3 top = _station.Surface.position;
            yield return MoveRight(top + Vector3.up * 0.4f, top + Vector3.up * 0.4f, 0.1f);
            yield return MoveRight(top + Vector3.up * 0.4f, top - Vector3.up * 0.02f, 0.15f);

            Assert.That(_station.Contacts, Is.EqualTo(1));
            Assert.That(_station.LastResult.Hit, Is.True, _station.LastResult.ToString());
            Assert.That(_station.Hits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SidewaysSwipe_IsNotAHit()
        {
            Vector3 top = _station.Surface.position;
            Vector3 start = top + new Vector3(-0.5f, 0.01f, 0f);
            yield return MoveRight(start, start, 0.1f);
            yield return MoveRight(start, top + new Vector3(0.1f, 0.0f, 0f), 0.15f);

            Assert.That(_station.Contacts, Is.EqualTo(1));
            Assert.That(_station.LastResult.Reason, Is.EqualTo(StrikeRejectReason.WrongAngle));
            Assert.That(_station.Hits, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator RigDescending_WithStillHands_DoesNotHit()
        {
            // 손은 리그에 대해 고정, 리그만 아래로 빠르게 내려가며 손이 패드에 닿는다(탈것·엘리베이터)
            Vector3 top = _station.Surface.position;
            _simulator.SetHand(HandSide.Right, new Vector3(0f, 1.0f, 0.5f), Quaternion.identity);
            _rigRoot.transform.position = new Vector3(0f, top.y - 1.0f + 0.4f, 0f);
            yield return null;
            yield return null;

            for (int i = 0; i < 20; i++)
            {
                _rigRoot.transform.position += Vector3.down * (3f / Fps);
                yield return null;
            }

            Assert.That(_station.Contacts, Is.GreaterThanOrEqualTo(1));
            Assert.That(_station.Hits, Is.EqualTo(0), _station.LastResult.ToString());
        }
    }
}
