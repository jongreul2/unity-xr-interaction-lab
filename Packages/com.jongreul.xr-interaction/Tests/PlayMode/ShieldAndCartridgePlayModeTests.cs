using System.Collections;
using Jongreul.XrInteraction.Cartridge;
using Jongreul.XrInteraction.Shield;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Tests
{
    public class ShieldStationPlayModeTests
    {
        StationTestRig _rig;
        GameObject _stationRoot;
        ShieldStation _station;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new StationTestRig();
            _stationRoot = new GameObject("ShieldStation");
            _station = _stationRoot.AddComponent<ShieldStation>();
            _station.AutoFire = false;
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

        IEnumerator HoldShieldInFront()
        {
            yield return _rig.GrabAt(_station.Shield.transform.position);
            Assert.That(_station.Shield.IsHeld, Is.True, "방패 잡기");
            yield return _rig.MoveRightTo(_rig.Rig.Head.position + new Vector3(0f, -0.35f, 0.3f));
        }

        IEnumerator FireAndWait(float angle, bool bypass)
        {
            _station.FireFrom(angle, bypass);
            for (int i = 0; i < 600 && _station.InFlight > 0; i++)
                yield return null;
            Assert.That(_station.InFlight, Is.EqualTo(0), "투사체가 도착해야 한다");
        }

        [UnityTest]
        public IEnumerator FrontalShot_WithShieldUp_IsBlocked_AndCostsDurability()
        {
            yield return HoldShieldInFront();
            yield return FireAndWait(0f, bypass: false);

            Assert.That(_station.Blocked, Is.EqualTo(1));
            Assert.That(_station.Blocker.Durability.Current, Is.LessThan(_station.Blocker.Durability.Max));
        }

        [UnityTest]
        public IEnumerator SideShot_GetsPastTheShield()
        {
            yield return HoldShieldInFront();
            yield return FireAndWait(80f, bypass: false);

            Assert.That(_station.Blocked, Is.EqualTo(0));
            Assert.That(_station.Hits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnblockableShot_IgnoresShield()
        {
            yield return HoldShieldInFront();
            yield return FireAndWait(0f, bypass: true);

            Assert.That(_station.Bypassed, Is.EqualTo(1));
            Assert.That(_station.Blocker.Durability.Current, Is.EqualTo(_station.Blocker.Durability.Max));
        }

        [UnityTest]
        public IEnumerator WithoutShield_EverythingHits()
        {
            yield return FireAndWait(0f, bypass: false);

            Assert.That(_station.Hits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FiveBlocks_BreakTheShield_ThenRepair()
        {
            yield return HoldShieldInFront();
            for (int i = 0; i < 5; i++)
                yield return FireAndWait(0f, bypass: false);

            Assert.That(_station.Blocker.Durability.Stage, Is.EqualTo(DurabilityStage.Broken));
            yield return FireAndWait(0f, bypass: false);
            Assert.That(_station.Hits, Is.EqualTo(1), "부서진 방패는 막지 못한다");

            _station.Repair();
            Assert.That(_station.Blocker.Durability.Stage, Is.EqualTo(DurabilityStage.Healthy));
        }
    }

    public class CartridgeStationPlayModeTests
    {
        StationTestRig _rig;
        GameObject _stationRoot;
        CartridgeStation _station;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new StationTestRig();
            _stationRoot = new GameObject("CartridgeStation");
            _station = _stationRoot.AddComponent<CartridgeStation>();
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

        CartridgeItem Item(string id)
        {
            foreach (CartridgeItem item in _station.Items)
                if (item.Id == id)
                    return item;
            return null;
        }

        IEnumerator PlugIn(string id, int slot)
        {
            yield return _rig.GrabAt(Item(id).transform.position);
            Assert.That(Item(id).IsHeld, Is.True, $"{id} 잡기");
            yield return _rig.MoveRightTo(_station.GetDockSlot(slot).position);
            yield return _rig.ReleaseRight();
            yield return _rig.MoveRightTo(_rig.Rig.Head.position + new Vector3(0.5f, -0.6f, 0.2f));
        }

        [UnityTest]
        public IEnumerator DropNearDockSlot_Inserts_AndRidesWithRig()
        {
            yield return PlugIn("hook", 0);

            Assert.That(_station.Slots.Get(0), Is.EqualTo("hook"));
            Assert.That(Item("hook").transform.parent, Is.SameAs(_station.GetDockSlot(0)));

            _rig.Root.transform.position += new Vector3(1f, 0f, 0f);
            yield return null;
            Assert.That(Vector3.Distance(Item("hook").transform.position, _station.GetDockSlot(0).position), Is.LessThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator Use_StartsCooldown_RingRefills()
        {
            yield return PlugIn("cube", 1);

            Assert.That(_station.Use(1), Is.EqualTo(CartridgeUseResult.Used));
            Assert.That(_station.Use(1), Is.EqualTo(CartridgeUseResult.CoolingDown));
            Assert.That(_station.Slots.CooldownProgress(1), Is.LessThan(0.2));
        }

        [UnityTest]
        public IEnumerator GrabbingFromDock_Ejects()
        {
            yield return PlugIn("boost", 2);
            yield return _rig.GrabAt(Item("boost").transform.position);

            Assert.That(_station.Slots.Get(2), Is.Null);
            Assert.That(Item("boost").IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator DropAwayFromDock_ReturnsToBooth()
        {
            CartridgeItem hook = Item("hook");
            yield return _rig.GrabAt(hook.transform.position);
            yield return _rig.MoveRightTo(_rig.Rig.Head.position + new Vector3(0.6f, 0f, 0.4f));
            yield return _rig.ReleaseRight();

            Assert.That(_station.Slots.FindSlot("hook"), Is.EqualTo(-1));
            Assert.That(Vector3.Distance(hook.transform.position, hook.Home.position), Is.LessThan(1e-4f));
        }
    }
}
