using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Equip;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction.Tests.Equip
{
    public class EquipStateTests
    {
        static readonly EquipItem Cap = new EquipItem("cap", "head", "hats");
        static readonly EquipItem Crown = new EquipItem("crown", "head", "hats");
        static readonly EquipItem Glasses = new EquipItem("glasses", "face", "eyewear");

        EquipState _state;
        List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _state = new EquipState(new[] { "head", "face", "back" });
            _log = new List<string>();
            _state.Equipped += (slot, item) => _log.Add($"+{slot}:{item.Id}");
            _state.Unequipped += (slot, item) => _log.Add($"-{slot}:{item.Id}");
        }

        [Test]
        public void EmptySlot_Equips_AndRaisesEvent()
        {
            EquipResult result = _state.TryEquip(Cap);

            Assert.That(result.Outcome, Is.EqualTo(EquipOutcome.Equipped));
            Assert.That(_state.Get("head"), Is.SameAs(Cap));
            Assert.That(_log, Is.EqualTo(new[] { "+head:cap" }));
        }

        [Test]
        public void SameItemAgain_IsAlreadyEquipped()
        {
            _state.TryEquip(Cap);

            Assert.That(_state.TryEquip(Cap).Outcome, Is.EqualTo(EquipOutcome.AlreadyEquipped));
            Assert.That(_log.Count, Is.EqualTo(1));
        }

        [Test]
        public void OccupiedSlot_NeedsConfirm_AndChangesNothing()
        {
            _state.TryEquip(Cap);

            EquipResult result = _state.TryEquip(Crown);

            Assert.That(result.Outcome, Is.EqualTo(EquipOutcome.NeedsReplaceConfirm));
            Assert.That(result.Previous, Is.SameAs(Cap));
            Assert.That(_state.Get("head"), Is.SameAs(Cap));
        }

        [Test]
        public void ConfirmedReplace_UnequipsThenEquips()
        {
            _state.TryEquip(Cap);

            EquipResult result = _state.TryEquip(Crown, confirmReplace: true);

            Assert.That(result.Outcome, Is.EqualTo(EquipOutcome.Replaced));
            Assert.That(_log, Is.EqualTo(new[] { "+head:cap", "-head:cap", "+head:crown" }));
        }

        [Test]
        public void UnknownSlot_IsRejected()
        {
            var tail = new EquipItem("tail", "tail", "misc");

            Assert.That(_state.TryEquip(tail).Outcome, Is.EqualTo(EquipOutcome.UnknownSlot));
        }

        [Test]
        public void Unequip_RemovesItem_EmptySlotReturnsFalse()
        {
            _state.TryEquip(Glasses);

            Assert.That(_state.Unequip("face"), Is.True);
            Assert.That(_state.Unequip("face"), Is.False);
            Assert.That(_state.IsEquipped("glasses"), Is.False);
        }

        [Test]
        public void Snapshot_IsACopy()
        {
            _state.TryEquip(Cap);
            IReadOnlyDictionary<string, EquipItem> snapshot = _state.Snapshot();
            _state.Unequip("head");

            Assert.That(snapshot["head"], Is.SameAs(Cap));
        }

        [Test]
        public void DuplicateSlotNames_Throw()
        {
            Assert.Throws<ArgumentException>(() => new EquipState(new[] { "head", "head" }));
        }
    }

    public class SocketSensorTests
    {
        static SocketCandidate Head(float radius = 0.2f, float maxAngle = 60f) =>
            new SocketCandidate("head", new Vector3(0, 1.7f, 0), new Vector3(0, 1, 0), radius, maxAngle);

        [Test]
        public void InFront_WithinRadius_ScoresPositive()
        {
            float score = SocketSensor.Score(Head(), new Vector3(0, 1.8f, 0));

            Assert.That(score, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void OutsideRadius_IsRejected()
        {
            Assert.That(SocketSensor.Score(Head(), new Vector3(0, 1.95f, 0)), Is.EqualTo(-1f));
        }

        [Test]
        public void FromBehind_IsRejectedByAngle()
        {
            // 머리 소켓 아래쪽(목 뒤)에서 다가오면 거리가 가까워도 붙지 않는다
            Assert.That(SocketSensor.Score(Head(), new Vector3(0, 1.6f, 0)), Is.EqualTo(-1f));
        }

        [TestCase(59f, true)]
        [TestCase(61f, false)]
        public void AngleBoundary(float degrees, bool accepted)
        {
            float rad = degrees * MathF.PI / 180f;
            var item = new Vector3(0, 1.7f, 0) + new Vector3(MathF.Sin(rad), MathF.Cos(rad), 0) * 0.1f;

            Assert.That(SocketSensor.Score(Head(), item) >= 0f, Is.EqualTo(accepted));
        }

        [Test]
        public void Overlapping_IsAccepted()
        {
            Assert.That(SocketSensor.Score(Head(), new Vector3(0, 1.7f, 0)), Is.EqualTo(1f));
        }

        [Test]
        public void FindBest_PicksClosestSocketOfSameSlot()
        {
            var sockets = new List<SocketCandidate>
            {
                new SocketCandidate("face", new Vector3(0, 1.75f, 0.1f), new Vector3(0, 0, 1), 0.3f, 90f),
                new SocketCandidate("head", new Vector3(0, 1.7f, 0), new Vector3(0, 1, 0), 0.3f, 90f),
                new SocketCandidate("head", new Vector3(0, 1.9f, 0), new Vector3(0, 1, 0), 0.3f, 90f),
            };

            Assert.That(SocketSensor.FindBest(sockets, "head", new Vector3(0, 1.95f, 0)), Is.EqualTo(2));
            Assert.That(SocketSensor.FindBest(sockets, "back", new Vector3(0, 1.95f, 0)), Is.EqualTo(-1));
        }
    }

    public class ShelfLayoutTests
    {
        static List<EquipItem> Items(int hats, int eyewear)
        {
            var items = new List<EquipItem>();
            for (int i = 0; i < hats; i++)
                items.Add(new EquipItem($"hat{i}", "head", "hats"));
            for (int i = 0; i < eyewear; i++)
                items.Add(new EquipItem($"glasses{i}", "face", "eyewear"));
            return items;
        }

        [Test]
        public void FiltersByCategory()
        {
            IReadOnlyList<ShelfCell> cells = ShelfLayout.Arrange(Items(3, 2), "eyewear", 3, 2, 0.3f, 0.3f);

            Assert.That(cells.Count, Is.EqualTo(2));
            Assert.That(cells[0].Item.Id, Is.EqualTo("glasses0"));
        }

        [Test]
        public void Columns_AreCentered_RowsGoDown()
        {
            IReadOnlyList<ShelfCell> cells = ShelfLayout.Arrange(Items(4, 0), null, 3, 2, 0.3f, 0.25f);

            Assert.That(cells[0].LocalPosition.X, Is.EqualTo(-0.3f).Within(1e-5f));
            Assert.That(cells[1].LocalPosition.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(cells[2].LocalPosition.X, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(cells[3].Row, Is.EqualTo(1));
            Assert.That(cells[3].LocalPosition.Y, Is.EqualTo(-0.25f).Within(1e-5f));
        }

        [Test]
        public void Paging_SplitsAndClamps()
        {
            List<EquipItem> items = Items(7, 0);

            Assert.That(ShelfLayout.PageCount(items.Count, 3, 2), Is.EqualTo(2));
            Assert.That(ShelfLayout.Arrange(items, null, 3, 2, 0.3f, 0.3f, page: 1).Count, Is.EqualTo(1));
            Assert.That(ShelfLayout.Arrange(items, null, 3, 2, 0.3f, 0.3f, page: 9)[0].Item.Id, Is.EqualTo("hat6"));
        }

        [Test]
        public void Empty_HasOnePage()
        {
            Assert.That(ShelfLayout.PageCount(0, 3, 2), Is.EqualTo(1));
            Assert.That(ShelfLayout.Arrange(new List<EquipItem>(), null, 3, 2, 0.3f, 0.3f).Count, Is.EqualTo(0));
        }
    }

    public class ProximityToggleTests
    {
        [Test]
        public void TurnsOn_AtEnterDistance_StaysOnUntilExitDistance()
        {
            var toggle = new ProximityToggle(1.5f, 2.0f);
            int changes = 0;
            toggle.Changed += _ => changes++;

            Assert.That(toggle.Update(1.6f), Is.False);
            Assert.That(toggle.Update(1.5f), Is.True);
            Assert.That(toggle.Update(1.9f), Is.True, "히스테리시스 구간");
            Assert.That(toggle.Update(2.01f), Is.False);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void ExitCloserThanEnter_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProximityToggle(2f, 1f));
        }
    }
}
