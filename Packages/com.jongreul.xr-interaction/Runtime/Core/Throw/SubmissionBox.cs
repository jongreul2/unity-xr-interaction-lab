using System;
using System.Collections.Generic;

namespace Jongreul.XrInteraction.Throw
{
    public enum SubmitAddResult
    {
        Added,
        /// <summary>이미 세었다(튕겨 나갔다 다시 들어오기 전의 중복 인식 등).</summary>
        Duplicate,
        WrongKind,
        Full,
    }

    public enum SubmitResult
    {
        Delivered,
        /// <summary>정해진 수에 못 미친다. 들어 있는 것은 그대로 둔다.</summary>
        NotEnough,
        Empty,
    }

    /// <summary>한 번의 제출 결과.</summary>
    public readonly struct Delivery
    {
        public readonly int Round;
        public readonly IReadOnlyList<string> Items;

        public Delivery(int round, IReadOnlyList<string> items)
        {
            Round = round;
            Items = items;
        }

        public int Count => Items?.Count ?? 0;
    }

    /// <summary>
    /// 제출 슬롯. 들어온 물건을 종류로 걸러 세고, 벨을 치면 모인 것을 한 번에 제출한다.
    /// 같은 물건은 한 번만 세고, 받지 않는 종류와 정원 초과는 거부한다. 정해진 수가 안 되면 제출하지 않고 그대로 둔다.
    /// </summary>
    public sealed class SubmissionBox
    {
        readonly List<string> _items = new List<string>();
        readonly HashSet<string> _kinds;

        /// <param name="acceptedKinds">받는 종류. null이나 빈 목록이면 전부 받는다.</param>
        public SubmissionBox(int capacity, int quota, IEnumerable<string> acceptedKinds = null)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (quota <= 0 || quota > capacity)
                throw new ArgumentOutOfRangeException(nameof(quota));

            Capacity = capacity;
            Quota = quota;
            if (acceptedKinds != null)
                _kinds = new HashSet<string>(acceptedKinds, StringComparer.Ordinal);
        }

        public int Capacity { get; }
        public int Quota { get; }
        public int Count => _items.Count;
        public IReadOnlyList<string> Items => _items;
        public int Rounds { get; private set; }
        public int TotalDelivered { get; private set; }

        public bool Accepts(string kind) => _kinds == null || _kinds.Count == 0 || (kind != null && _kinds.Contains(kind));

        public bool Contains(string itemId) => _items.Contains(itemId);

        public SubmitAddResult TryAdd(string itemId, string kind)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("물건 ID가 비어 있다.", nameof(itemId));
            if (_items.Contains(itemId))
                return SubmitAddResult.Duplicate;
            if (!Accepts(kind))
                return SubmitAddResult.WrongKind;
            if (_items.Count >= Capacity)
                return SubmitAddResult.Full;

            _items.Add(itemId);
            return SubmitAddResult.Added;
        }

        /// <summary>손으로 다시 꺼내거나 튕겨 나갔을 때. 들어 있지 않았으면 false.</summary>
        public bool Remove(string itemId) => _items.Remove(itemId);

        public SubmitResult Submit(out Delivery delivery)
        {
            delivery = default;
            if (_items.Count == 0)
                return SubmitResult.Empty;
            if (_items.Count < Quota)
                return SubmitResult.NotEnough;

            Rounds++;
            TotalDelivered += _items.Count;
            delivery = new Delivery(Rounds, _items.ToArray());
            _items.Clear();
            return SubmitResult.Delivered;
        }
    }
}
