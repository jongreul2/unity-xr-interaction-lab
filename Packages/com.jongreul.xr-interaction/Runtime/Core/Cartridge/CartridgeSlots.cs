using System;
using System.Collections.Generic;

namespace Jongreul.XrInteraction.Cartridge
{
    public enum CartridgeInsertResult
    {
        Inserted,
        SlotOccupied,
        /// <summary>같은 카트리지가 이미 다른 슬롯에 꽂혀 있다.</summary>
        AlreadyInserted,
        InvalidSlot,
    }

    public enum CartridgeUseResult
    {
        Used,
        EmptySlot,
        CoolingDown,
        InvalidSlot,
    }

    /// <summary>
    /// 등 뒤 슬롯독. 카트리지를 꽂으면 그 슬롯의 스킬이 활성화되고, 뽑으면 해제된다.
    /// 쿨타임은 슬롯이 아니라 카트리지에 붙는다 — 뽑았다 다른 슬롯에 꽂아 쿨타임을 지우는 꼼수를 막는다.
    /// </summary>
    public sealed class CartridgeSlots
    {
        readonly string[] _slots;
        readonly Dictionary<string, double> _cooldownEndsAt = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly Dictionary<string, double> _cooldownLength = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly IClock _clock;

        public CartridgeSlots(int slotCount, IClock clock)
        {
            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slots = new string[slotCount];
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public int SlotCount => _slots.Length;

        public event Action<int, string> Inserted;
        public event Action<int, string> Ejected;
        public event Action<int, string> Used;

        public string Get(int slot) => IsValid(slot) ? _slots[slot] : null;

        public int FindSlot(string cartridgeId) => Array.IndexOf(_slots, cartridgeId);

        public CartridgeInsertResult Insert(int slot, string cartridgeId)
        {
            if (string.IsNullOrEmpty(cartridgeId))
                throw new ArgumentException("카트리지 ID가 비어 있다.", nameof(cartridgeId));
            if (!IsValid(slot))
                return CartridgeInsertResult.InvalidSlot;
            if (_slots[slot] != null)
                return CartridgeInsertResult.SlotOccupied;
            if (FindSlot(cartridgeId) >= 0)
                return CartridgeInsertResult.AlreadyInserted;

            _slots[slot] = cartridgeId;
            Inserted?.Invoke(slot, cartridgeId);
            return CartridgeInsertResult.Inserted;
        }

        /// <summary>뽑은 카트리지 ID. 비어 있으면 null.</summary>
        public string Eject(int slot)
        {
            if (!IsValid(slot) || _slots[slot] == null)
                return null;

            string id = _slots[slot];
            _slots[slot] = null;
            Ejected?.Invoke(slot, id);
            return id;
        }

        public CartridgeUseResult Use(int slot, double cooldownSeconds)
        {
            if (!IsValid(slot))
                return CartridgeUseResult.InvalidSlot;

            string id = _slots[slot];
            if (id == null)
                return CartridgeUseResult.EmptySlot;
            if (CooldownRemaining(slot) > 0)
                return CartridgeUseResult.CoolingDown;

            double length = Math.Max(0, cooldownSeconds);
            _cooldownLength[id] = length;
            _cooldownEndsAt[id] = _clock.Now + length;
            Used?.Invoke(slot, id);
            return CartridgeUseResult.Used;
        }

        public double CooldownRemaining(int slot)
        {
            string id = Get(slot);
            if (id == null || !_cooldownEndsAt.TryGetValue(id, out double endsAt))
                return 0;
            return Math.Max(0, endsAt - _clock.Now);
        }

        /// <summary>쿨타임 링 표시용 0(방금 사용)~1(사용 가능).</summary>
        public double CooldownProgress(int slot)
        {
            string id = Get(slot);
            if (id == null || !_cooldownLength.TryGetValue(id, out double length) || length <= 0)
                return 1;
            return 1 - Math.Min(1, CooldownRemaining(slot) / length);
        }

        bool IsValid(int slot) => slot >= 0 && slot < _slots.Length;
    }
}
