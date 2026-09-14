using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Shield;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction.Tests.Shield
{
    public class ShieldTests
    {
        static readonly Vector3 Forward = new Vector3(0, 0, 1);

        /// <summary>방패 정면 기준 degrees만큼 옆에서 날아오는 공격의 진행 방향.</summary>
        static Vector3 AttackFrom(float degrees)
        {
            float rad = degrees * MathF.PI / 180f;
            var from = new Vector3(MathF.Sin(rad), 0, MathF.Cos(rad));
            return -from;
        }

        [Test]
        public void FrontalAttack_IsBlocked()
        {
            Assert.That(new BlockJudge().Judge(Forward, AttackFrom(0)), Is.EqualTo(BlockOutcome.Blocked));
        }

        [Test]
        public void SideAttack_IsMissed()
        {
            Assert.That(new BlockJudge().Judge(Forward, AttackFrom(90)), Is.EqualTo(BlockOutcome.Missed));
        }

        [TestCase(44.5f, BlockOutcome.Blocked)]
        [TestCase(45.5f, BlockOutcome.Missed)]
        public void AngleBoundary(float degrees, BlockOutcome expected)
        {
            Assert.That(new BlockJudge(45f).Judge(Forward, AttackFrom(degrees)), Is.EqualTo(expected));
        }

        [Test]
        public void BypassingAttack_IsNotBlockedEvenFromFront()
        {
            Assert.That(new BlockJudge().Judge(Forward, AttackFrom(0), bypassesBlock: true),
                Is.EqualTo(BlockOutcome.Bypassed));
        }

        [Test]
        public void Durability_PassesThroughThreeStagesThenBreaks()
        {
            var durability = new ShieldDurability(100);
            var stages = new List<DurabilityStage>();
            int broken = 0;
            durability.StageChanged += stages.Add;
            durability.Broken += () => broken++;

            durability.ApplyDamage(35); // 65
            durability.ApplyDamage(35); // 30
            durability.ApplyDamage(40); // 0

            Assert.That(stages, Is.EqualTo(new[] { DurabilityStage.Worn, DurabilityStage.Critical, DurabilityStage.Broken }));
            Assert.That(broken, Is.EqualTo(1));
            Assert.That(durability.Current, Is.EqualTo(0));
        }

        [Test]
        public void OnlyBlockedAttacks_CostDurability()
        {
            var blocker = new ShieldBlocker(new BlockJudge(), new ShieldDurability(100));

            blocker.ReceiveAttack(Forward, AttackFrom(90), 30);
            blocker.ReceiveAttack(Forward, AttackFrom(0), 30, bypassesBlock: true);
            blocker.ReceiveAttack(Forward, AttackFrom(10), 30);

            Assert.That(blocker.Durability.Current, Is.EqualTo(70));
        }

        [Test]
        public void BrokenShield_BlocksNothing()
        {
            var blocker = new ShieldBlocker(new BlockJudge(), new ShieldDurability(10));
            blocker.ReceiveAttack(Forward, AttackFrom(0), 10);

            Assert.That(blocker.ReceiveAttack(Forward, AttackFrom(0), 10), Is.EqualTo(BlockOutcome.ShieldBroken));
        }

        [Test]
        public void NegativeDamage_IsIgnored_RepairRestores()
        {
            var durability = new ShieldDurability(100);
            durability.ApplyDamage(-50);
            Assert.That(durability.Current, Is.EqualTo(100));

            durability.ApplyDamage(80);
            durability.Repair();
            Assert.That(durability.Stage, Is.EqualTo(DurabilityStage.Healthy));
            Assert.That(durability.Current, Is.EqualTo(100));
        }

        [Test]
        public void InvalidThresholds_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShieldDurability(100, wornBelow: 0.2f, criticalBelow: 0.5f));
        }
    }
}
