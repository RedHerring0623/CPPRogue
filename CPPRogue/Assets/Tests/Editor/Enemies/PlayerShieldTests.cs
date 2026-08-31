using System.Linq;
using CPPRogue.Core.Enemies;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Enemies
{
    /// <summary>玩家护盾与超时惩罚伤害的结算测试（LootDesign.md §1 / CODE_EXECUTION.md shield 语义）。</summary>
    [TestFixture]
    public class PlayerShieldTests
    {
        private static EnemySim NewSim()
        {
            var sim = new EnemySim(new EnemySimConfig(), seed: 3);
            sim.SetPlayerPosition(new Vec2(0f, 0f));
            return sim;
        }

        /// <summary>用看门狗式自爆对玩家造成一次伤害（走无敌帧规则；新 sim 无敌帧为 0）。</summary>
        private static void BlastPlayer(EnemySim sim, float damage)
        {
            Enemy dog = sim.Spawn(EnemyKind.Watchdog, new Vec2(0f, 0f));
            sim.Explode(dog, 5f, damage);
        }

        [Test]
        public void 护盾_完全吸收_不掉血不给无敌帧()
        {
            EnemySim sim = NewSim();
            float hp0 = sim.PlayerHp;
            sim.ShieldPlayer(40f);
            BlastPlayer(sim, 30f);
            sim.DrainEvents();

            Assert.AreEqual(hp0, sim.PlayerHp, 1e-4, "护盾吃满，不掉血");
            Assert.AreEqual(10f, sim.PlayerShield, 1e-4, "护盾剩 40-30");
            Assert.AreEqual(0f, sim.PlayerInvuln, 1e-6, "完全吸收不给无敌帧");
        }

        [Test]
        public void 护盾_溢出伤害进血并给无敌帧()
        {
            EnemySim sim = NewSim();
            float hp0 = sim.PlayerHp;   // 10（直值量级）
            sim.ShieldPlayer(10f);
            BlastPlayer(sim, 12f);
            Assert.AreEqual(hp0 - 2f, sim.PlayerHp, 1e-4, "破盾后 2 点进血");
            Assert.AreEqual(0f, sim.PlayerShield, 1e-4);
            Assert.Greater(sim.PlayerInvuln, 0f, "破盾受击给无敌帧");
        }

        [Test]
        public void 护盾_叠加与tick清空()
        {
            EnemySim sim = NewSim();
            sim.ShieldPlayer(10f);
            sim.ShieldPlayer(15f);
            Assert.AreEqual(25f, sim.PlayerShield, 1e-4);
            sim.ClearPlayerShield();
            Assert.AreEqual(0f, sim.PlayerShield, 1e-4);
        }

        [Test]
        public void 护盾_死亡后不生效()
        {
            EnemySim sim = NewSim();
            sim.TimeoutPunishDamage(1f);   // 直接致死（穿盾路径）
            Assert.IsTrue(sim.PlayerDead);
            sim.ShieldPlayer(99f);
            Assert.AreEqual(0f, sim.PlayerShield, 1e-4, "死亡后叠不了盾");
        }

        [Test]
        public void 超时惩罚_穿盾_按最大生命百分比()
        {
            EnemySim sim = NewSim();
            float hp0 = sim.PlayerHp;
            sim.ShieldPlayer(999f);
            sim.TimeoutPunishDamage(0.1f);
            Assert.AreEqual(hp0 - sim.PlayerMaxHp * 0.1f, sim.PlayerHp, 1e-4, "穿透护盾直接扣血");
            Assert.AreEqual(999f, sim.PlayerShield, 1e-4, "盾不掉");
            Assert.IsTrue(sim.DrainEvents().Any(e => e.Type == SimEventType.PlayerHit));
        }
    }
}
