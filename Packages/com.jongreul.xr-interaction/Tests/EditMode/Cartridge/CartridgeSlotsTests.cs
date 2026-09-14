using System.Collections.Generic;
using Jongreul.XrInteraction.Cartridge;
using NUnit.Framework;

namespace Jongreul.XrInteraction.Tests.Cartridge
{
    public class CartridgeSlotsTests
    {
        ManualClock _clock;
        CartridgeSlots _slots;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _slots = new CartridgeSlots(3, _clock);
        }

        [Test]
        public void InsertAndEject_RaiseEvents()
        {
            var log = new List<string>();
            _slots.Inserted += (slot, id) => log.Add($"+{slot}:{id}");
            _slots.Ejected += (slot, id) => log.Add($"-{slot}:{id}");

            Assert.That(_slots.Insert(1, "hook"), Is.EqualTo(CartridgeInsertResult.Inserted));
            Assert.That(_slots.Get(1), Is.EqualTo("hook"));
            Assert.That(_slots.Eject(1), Is.EqualTo("hook"));
            Assert.That(_slots.Eject(1), Is.Null);
            Assert.That(log, Is.EqualTo(new[] { "+1:hook", "-1:hook" }));
        }

        [Test]
        public void OccupiedSlot_IsRejected()
        {
            _slots.Insert(0, "hook");

            Assert.That(_slots.Insert(0, "boost"), Is.EqualTo(CartridgeInsertResult.SlotOccupied));
        }

        [Test]
        public void SameCartridgeInTwoSlots_IsRejected()
        {
            _slots.Insert(0, "hook");

            Assert.That(_slots.Insert(2, "hook"), Is.EqualTo(CartridgeInsertResult.AlreadyInserted));
        }

        [Test]
        public void InvalidSlot_IsRejected()
        {
            Assert.That(_slots.Insert(3, "hook"), Is.EqualTo(CartridgeInsertResult.InvalidSlot));
            Assert.That(_slots.Use(-1, 1), Is.EqualTo(CartridgeUseResult.InvalidSlot));
        }

        [Test]
        public void Use_StartsCooldown_ThenReady()
        {
            _slots.Insert(0, "hook");

            Assert.That(_slots.Use(0, 2.0), Is.EqualTo(CartridgeUseResult.Used));
            _clock.Advance(1.0);
            Assert.That(_slots.Use(0, 2.0), Is.EqualTo(CartridgeUseResult.CoolingDown));
            Assert.That(_slots.CooldownProgress(0), Is.EqualTo(0.5).Within(1e-9));
            _clock.Advance(1.0);
            Assert.That(_slots.Use(0, 2.0), Is.EqualTo(CartridgeUseResult.Used));
        }

        [Test]
        public void Cooldown_FollowsCartridge_WhenMovedToAnotherSlot()
        {
            _slots.Insert(0, "hook");
            _slots.Use(0, 3.0);
            _slots.Eject(0);
            _slots.Insert(2, "hook");

            Assert.That(_slots.Use(2, 3.0), Is.EqualTo(CartridgeUseResult.CoolingDown));
            Assert.That(_slots.CooldownRemaining(2), Is.EqualTo(3.0).Within(1e-9));
            Assert.That(_slots.CooldownRemaining(0), Is.EqualTo(0));
        }

        [Test]
        public void EmptySlot_CannotBeUsed()
        {
            Assert.That(_slots.Use(1, 1.0), Is.EqualTo(CartridgeUseResult.EmptySlot));
            Assert.That(_slots.CooldownProgress(1), Is.EqualTo(1));
        }
    }
}
