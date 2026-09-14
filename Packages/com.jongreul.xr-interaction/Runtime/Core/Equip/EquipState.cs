using System;
using System.Collections.Generic;

namespace Jongreul.XrInteraction.Equip
{
    /// <summary>장착 가능한 아이템. 어느 신체 슬롯에 들어가는지와 진열 카테고리를 가진다.</summary>
    public sealed class EquipItem
    {
        public EquipItem(string id, string slot, string category)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("아이템 ID가 비어 있다.", nameof(id));
            if (string.IsNullOrEmpty(slot))
                throw new ArgumentException("슬롯이 비어 있다.", nameof(slot));

            Id = id;
            Slot = slot;
            Category = category ?? string.Empty;
        }

        public string Id { get; }
        public string Slot { get; }
        public string Category { get; }

        public override string ToString() => $"{Id}({Slot})";
    }

    public enum EquipOutcome
    {
        Equipped,
        /// <summary>교체 확인을 받아 기존 아이템을 벗기고 새 아이템을 입혔다.</summary>
        Replaced,
        AlreadyEquipped,
        /// <summary>슬롯에 다른 아이템이 있다. 교체 확인이 필요하다(상태는 바뀌지 않음).</summary>
        NeedsReplaceConfirm,
        UnknownSlot,
    }

    public readonly struct EquipResult
    {
        public readonly EquipOutcome Outcome;
        public readonly EquipItem Item;

        /// <summary>교체 대상(또는 교체된) 기존 아이템. 없으면 null.</summary>
        public readonly EquipItem Previous;

        public EquipResult(EquipOutcome outcome, EquipItem item, EquipItem previous)
        {
            Outcome = outcome;
            Item = item;
            Previous = previous;
        }

        public bool Changed => Outcome == EquipOutcome.Equipped || Outcome == EquipOutcome.Replaced;
    }

    /// <summary>
    /// 신체 슬롯별 장착 상태. 한 슬롯에 하나. 다른 아이템이 있으면 교체 확인을 요구한다.
    /// 장착·해제는 이벤트로 알린다 — 마네킹 미러·서버 저장이 여기에 붙는다.
    /// </summary>
    public sealed class EquipState
    {
        readonly Dictionary<string, EquipItem> _bySlot = new Dictionary<string, EquipItem>(StringComparer.Ordinal);
        readonly List<string> _slots;

        public EquipState(IEnumerable<string> slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));

            _slots = new List<string>();
            foreach (string slot in slots)
            {
                if (string.IsNullOrEmpty(slot) || _slots.Contains(slot))
                    throw new ArgumentException($"슬롯 이름이 비었거나 중복: '{slot}'", nameof(slots));
                _slots.Add(slot);
            }
        }

        public IReadOnlyList<string> Slots => _slots;

        public event Action<string, EquipItem> Equipped;
        public event Action<string, EquipItem> Unequipped;

        public bool HasSlot(string slot) => slot != null && _slots.Contains(slot);

        public EquipItem Get(string slot) =>
            slot != null && _bySlot.TryGetValue(slot, out EquipItem item) ? item : null;

        public bool IsEquipped(string itemId)
        {
            foreach (EquipItem item in _bySlot.Values)
            {
                if (item.Id == itemId)
                    return true;
            }

            return false;
        }

        public EquipResult TryEquip(EquipItem item, bool confirmReplace = false)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!HasSlot(item.Slot))
                return new EquipResult(EquipOutcome.UnknownSlot, item, null);

            EquipItem current = Get(item.Slot);
            if (current != null && current.Id == item.Id)
                return new EquipResult(EquipOutcome.AlreadyEquipped, item, current);

            if (current != null && !confirmReplace)
                return new EquipResult(EquipOutcome.NeedsReplaceConfirm, item, current);

            if (current != null)
            {
                _bySlot.Remove(item.Slot);
                Unequipped?.Invoke(item.Slot, current);
            }

            _bySlot[item.Slot] = item;
            Equipped?.Invoke(item.Slot, item);
            return new EquipResult(current == null ? EquipOutcome.Equipped : EquipOutcome.Replaced, item, current);
        }

        public bool Unequip(string slot)
        {
            EquipItem current = Get(slot);
            if (current == null)
                return false;

            _bySlot.Remove(slot);
            Unequipped?.Invoke(slot, current);
            return true;
        }

        /// <summary>현재 장착 상태 복사본. 미러 초기화·저장용.</summary>
        public IReadOnlyDictionary<string, EquipItem> Snapshot() =>
            new Dictionary<string, EquipItem>(_bySlot, StringComparer.Ordinal);
    }
}
