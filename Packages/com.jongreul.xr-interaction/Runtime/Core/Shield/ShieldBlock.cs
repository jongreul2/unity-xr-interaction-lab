using System;
using System.Numerics;

namespace Jongreul.XrInteraction.Shield
{
    public enum BlockOutcome
    {
        Blocked,
        Missed,
        /// <summary>막기를 무시하는 공격(관통·잡기 등).</summary>
        Bypassed,
        /// <summary>방패가 이미 부서졌다.</summary>
        ShieldBroken,
    }

    /// <summary>
    /// 방패 각도 판정. 방패 앞면 방향과 "공격이 오는 쪽" 사이 각도가 한도 이내면 막는다.
    /// </summary>
    public sealed class BlockJudge
    {
        public BlockJudge(float maxAngleDegrees = 45f)
        {
            if (maxAngleDegrees <= 0f || maxAngleDegrees > 180f)
                throw new ArgumentOutOfRangeException(nameof(maxAngleDegrees));
            MaxAngleDegrees = maxAngleDegrees;
        }

        public float MaxAngleDegrees { get; }

        /// <summary>방패 앞면과 공격이 오는 방향 사이 각도(도).</summary>
        public static float AngleTo(Vector3 shieldForward, Vector3 attackDirection)
        {
            if (shieldForward.LengthSquared() < 1e-10f || attackDirection.LengthSquared() < 1e-10f)
                return 180f;

            // 공격 진행 방향의 반대가 "공격이 오는 쪽"이다.
            float cos = Vector3.Dot(Vector3.Normalize(shieldForward), Vector3.Normalize(-attackDirection));
            return MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
        }

        /// <param name="attackDirection">공격이 날아가는 진행 방향(공격자 → 방어자).</param>
        public BlockOutcome Judge(Vector3 shieldForward, Vector3 attackDirection, bool bypassesBlock = false)
        {
            if (bypassesBlock)
                return BlockOutcome.Bypassed;
            return AngleTo(shieldForward, attackDirection) <= MaxAngleDegrees ? BlockOutcome.Blocked : BlockOutcome.Missed;
        }
    }

    public enum DurabilityStage
    {
        Healthy,
        Worn,
        Critical,
        Broken,
    }

    /// <summary>방패 내구도와 3단계 표시 구간. 0이 되면 파손.</summary>
    public sealed class ShieldDurability
    {
        public ShieldDurability(int max, float wornBelow = 0.66f, float criticalBelow = 0.33f)
        {
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));
            if (!(criticalBelow > 0f && criticalBelow < wornBelow && wornBelow < 1f))
                throw new ArgumentOutOfRangeException(nameof(wornBelow), "0 < critical < worn < 1 이어야 한다.");

            Max = max;
            Current = max;
            WornBelow = wornBelow;
            CriticalBelow = criticalBelow;
        }

        public int Max { get; }
        public int Current { get; private set; }
        public float WornBelow { get; }
        public float CriticalBelow { get; }
        public float Ratio => (float)Current / Max;
        public bool IsBroken => Current <= 0;

        public DurabilityStage Stage =>
            IsBroken ? DurabilityStage.Broken
            : Ratio < CriticalBelow ? DurabilityStage.Critical
            : Ratio < WornBelow ? DurabilityStage.Worn
            : DurabilityStage.Healthy;

        public event Action<DurabilityStage> StageChanged;
        public event Action Broken;

        /// <summary>막아서 받은 피해. 음수는 무시한다.</summary>
        public DurabilityStage ApplyDamage(int damage)
        {
            if (IsBroken || damage <= 0)
                return Stage;

            DurabilityStage before = Stage;
            Current = Math.Max(0, Current - damage);
            DurabilityStage after = Stage;
            if (after != before)
            {
                StageChanged?.Invoke(after);
                if (after == DurabilityStage.Broken)
                    Broken?.Invoke();
            }

            return after;
        }

        public void Repair()
        {
            DurabilityStage before = Stage;
            Current = Max;
            if (before != DurabilityStage.Healthy)
                StageChanged?.Invoke(DurabilityStage.Healthy);
        }
    }

    /// <summary>판정 + 내구도를 묶은 한 번의 막기 처리.</summary>
    public sealed class ShieldBlocker
    {
        public ShieldBlocker(BlockJudge judge, ShieldDurability durability)
        {
            Judge = judge ?? throw new ArgumentNullException(nameof(judge));
            Durability = durability ?? throw new ArgumentNullException(nameof(durability));
        }

        public BlockJudge Judge { get; }
        public ShieldDurability Durability { get; }

        public BlockOutcome ReceiveAttack(Vector3 shieldForward, Vector3 attackDirection, int damage,
            bool bypassesBlock = false)
        {
            if (Durability.IsBroken)
                return BlockOutcome.ShieldBroken;

            BlockOutcome outcome = Judge.Judge(shieldForward, attackDirection, bypassesBlock);
            if (outcome == BlockOutcome.Blocked)
                Durability.ApplyDamage(damage);
            return outcome;
        }
    }
}
