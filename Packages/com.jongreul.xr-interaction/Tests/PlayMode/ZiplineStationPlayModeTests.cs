using System;
using System.Collections;
using Jongreul.XrInteraction.Stations;
using Jongreul.XrInteraction.Zipline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jongreul.XrInteraction.Tests
{
    public class ZiplineStationPlayModeTests
    {
        StationTestRig _rig;
        GameObject _stationRoot;
        ZiplineStation _station;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new StationTestRig();
            _stationRoot = new GameObject("ZiplineStation");
            _station = _stationRoot.AddComponent<ZiplineStation>();
            _station.Configure(_rig.Rig);
            _rig.Root.transform.position = new Vector3(0f, ZiplineStation.PlatformHeight, 0f);
            yield return StationTestRig.Frames(2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_stationRoot);
            _rig.Dispose();
            yield return null;
        }

        IEnumerator GrabHandle()
        {
            yield return _rig.GrabAt(_station.Handle.transform.position);
            Assert.That(_station.Handle.IsHeld, Is.True, "손잡이 잡기");
            Assert.That(_station.Ride.IsRiding, Is.True, "잡으면 출발");
        }

        static IEnumerator WaitUntil(Func<bool> done, int maxFrames = 900)
        {
            for (int i = 0; i < maxFrames && !done(); i++)
                yield return null;
            Assert.That(done(), Is.True, "시간 초과");
        }

        Vector3 RigPosition => _rig.Root.transform.position;

        [UnityTest]
        public IEnumerator RideToEnd_AutoReleases_ThenDropsToGround()
        {
            yield return GrabHandle();

            yield return WaitUntil(() => !_station.Ride.IsRiding);
            Assert.That(_station.LastEnd, Is.EqualTo(ZiplineEndReason.ReachedEnd));
            Assert.That(_rig.Right.Held, Is.Null, "종점에서 자동으로 놓는다");

            yield return WaitUntil(() => !_station.IsFalling);
            Assert.That(RigPosition.y, Is.EqualTo(0f).Within(0.01f), "착지 구역 바닥");
            Assert.That(RigPosition.z, Is.GreaterThan(8f));
        }

        [UnityTest]
        public IEnumerator RigRidesWithHandle_KeepingItsOffset()
        {
            yield return GrabHandle();
            Vector3 offset = RigPosition - _station.Handle.transform.position;
            float startZ = _station.Handle.transform.position.z;

            yield return StationTestRig.Frames(60);

            Assert.That(_station.Handle.transform.position.z, Is.GreaterThan(startZ + 0.5f), "케이블을 따라 전진");
            Assert.That(Vector3.Distance(RigPosition - _station.Handle.transform.position, offset), Is.LessThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator LettingGoMidway_FallsForward_AndHandleReturns()
        {
            yield return GrabHandle();
            yield return StationTestRig.Frames(90);
            float releaseZ = RigPosition.z;

            yield return _rig.ReleaseRight();
            yield return WaitUntil(() => !_station.Ride.IsRiding);
            Assert.That(_station.LastEnd, Is.EqualTo(ZiplineEndReason.Released));

            yield return WaitUntil(() => !_station.IsFalling);
            Assert.That(RigPosition.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(RigPosition.z, Is.GreaterThan(releaseZ + 0.3f), "케이블 방향 속도를 안고 떨어진다");

            yield return WaitUntil(() => !_station.Handle.Returning);
            Assert.That(_station.HandleDistance, Is.EqualTo(ZiplineStation.RestDistance).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator SwappingHandsMidRide_KeepsGoing_ToTheEnd()
        {
            yield return GrabHandle();
            yield return StationTestRig.Frames(40);

            // 왼손을 손잡이에 대고 쥐면 오른손에서 넘겨받는다
            _rig.Simulator.SetHand(HandSide.Left, _rig.Local(_station.Handle.transform.position), Quaternion.identity);
            yield return StationTestRig.Frames(2);
            _rig.Simulator.SetGrip(HandSide.Left, 1f);
            yield return StationTestRig.Frames(2);

            Assert.That(_station.Handle.Holder, Is.SameAs(_rig.Left));
            Assert.That(_station.Ride.IsRiding, Is.True);
            Assert.That(_station.Rides, Is.EqualTo(1), "다시 출발하지 않는다");

            yield return WaitUntil(() => !_station.Ride.IsRiding);
            Assert.That(_station.LastEnd, Is.EqualTo(ZiplineEndReason.ReachedEnd));
        }
    }
}
