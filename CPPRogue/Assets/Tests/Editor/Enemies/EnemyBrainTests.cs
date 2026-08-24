using CPPRogue.Core.Enemies;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Enemies
{
    /// <summary>
    /// 各 Brain 行为规格的单元测试，逐条对 EnemyDesign.md：
    /// 断点锁矢量可骗冲、异常定距投掷/贴脸后退、风暴环上游走、过热快慢循环、编译中双倍易伤。
    /// </summary>
    [TestFixture]
    public class EnemyBrainTests
    {
        private static EnemySim NewSim()
        {
            var sim = new EnemySim(new EnemySimConfig(), seed: 3);
            sim.SetPlayerPosition(new Vec2(0f, 0f));
            return sim;
        }

        private static void Run(EnemySim sim, float seconds, float dt = 0.05f)
        {
            int steps = (int)System.Math.Ceiling(seconds / dt);
            for (int i = 0; i < steps; i++)
                sim.Step(dt);
        }

        [Test]
        public void Chaser_MovesTowardPlayer()
        {
            EnemySim sim = NewSim();
            Enemy bug = sim.Spawn(EnemyKind.Bug, new Vec2(5f, 0f));

            sim.Step(0.1f);

            Assert.Less(bug.Position.X, 5f);
            Assert.AreEqual(0f, bug.Position.Y, 0.01f);
        }

        [Test]
        public void Breakpoint_VectorLockedAtStop_PlayerSidestepDodges()
        {
            EnemySim sim = NewSim();
            Enemy hunter = sim.Spawn(EnemyKind.Breakpoint, new Vec2(5f, 0f));
            var cfg = sim.Config;

            // 走到 x 内触发锁定：记录锁定点和矢量
            Vec2 lockPos = Vec2.Zero;
            int guard = 400;
            while (guard-- > 0)
            {
                sim.Step(0.05f);
                if (hunter.TelegraphActive)
                {
                    lockPos = hunter.Position;
                    break;
                }
            }
            Assume.That(hunter.TelegraphActive, Is.True, "应进入锁定阶段");
            Vec2 lockedDir = (sim.PlayerPosition - lockPos).Normalized;
            Assert.AreEqual("paused at breakpoint", hunter.StateLabel);

            // 锁定后玩家侧移：矢量不再更新，预兆线也不变
            sim.SetPlayerPosition(new Vec2(0f, 5f));
            sim.Step(0.05f);
            Assert.AreEqual(lockPos + lockedDir * cfg.BreakpointDashDistance, hunter.TelegraphTo);

            // 走完 等1s + 冲刺2x，落点在锁定矢量延长线上（玩家早已让开）
            Run(sim, 1f + cfg.BreakpointDashDistance / StatTable.Speed(cfg.BreakpointDashSpeedLv) + 0.05f);
            Vec2 expected = lockPos + lockedDir * cfg.BreakpointDashDistance;
            Assert.AreEqual(expected.X, hunter.Position.X, 0.05f, "沿锁定矢量冲刺，吃不到侧移后的玩家");
            Assert.AreEqual(expected.Y, hunter.Position.Y, 0.05f);
            Assert.IsFalse(hunter.TelegraphActive, "冲刺开始预兆线消失");
        }

        [Test]
        public void Breakpoint_DashCrossesStationaryPlayer_DealsContactDamage()
        {
            EnemySim sim = NewSim();
            Enemy hunter = sim.Spawn(EnemyKind.Breakpoint, new Vec2(5f, 0f));

            Run(sim, 5f); // 足够走完 追击→锁定→冲刺→恢复 全流程（玩家站桩被穿过）

            // 站桩玩家在冲刺穿过期间每 0.1s 无敌帧间隔结算一次（实际对局里首跳击退会把玩家弹出冲刺带）
            Assert.Less(sim.PlayerHp, 100f, "冲刺穿过玩家造成接触伤害");
        }

        [Test]
        public void Ranged_HoldsAtStopRange_AndFiresOnArrival()
        {
            EnemySim sim = NewSim();
            Enemy shooter = sim.Spawn(EnemyKind.Exception, new Vec2(6f, 0f));
            var cfg = sim.Config;

            Run(sim, 1.6f); // 6→3 走 1.25s，到位后站定

            float dist = Vec2.Distance(shooter.Position, sim.PlayerPosition);
            Assert.GreaterOrEqual(dist, cfg.ExceptionRetreatRange, "不会走进贴脸区间");
            Assert.LessOrEqual(dist, cfg.ExceptionStopRange + 0.2f, "停在 2x 外缘");
            Assert.AreEqual(1, sim.Bullets.Count, "到位即投掷第一发异常（还在飞）");

            // 射速断言走 PlayerHit 事件：子弹命中玩家即消失，数在场子弹会漏
            sim.DrainEvents();
            Run(sim, cfg.ExceptionFireInterval * 2f);
            int hits = 0;
            foreach (SimEvent ev in sim.DrainEvents())
                if (ev.Type == SimEventType.PlayerHit)
                    hits++;
            Assert.AreEqual(2, hits, "按 2s 间隔持续投掷，两发已命中");
        }

        [Test]
        public void Ranged_RetreatsWhenPlayerClosesIn()
        {
            EnemySim sim = NewSim();
            Enemy shooter = sim.Spawn(EnemyKind.Exception, new Vec2(3f, 0f)); // 恰在 2x 外缘站定

            sim.SetPlayerPosition(new Vec2(2.6f, 0f)); // 玩家贴进 x 内
            Run(sim, 0.3f);

            Assert.Greater(shooter.Position.X, 3f, "背向玩家后退拉开距离");
        }

        [Test]
        public void Storm_OrbitsRingRange_AndFireRateIsHigh()
        {
            EnemySim sim = NewSim();
            Enemy storm = sim.Spawn(EnemyKind.Storm, new Vec2(6f, 0f));
            var cfg = sim.Config;

            Vec2 early = storm.Position;
            Run(sim, 3f);
            Vec2 late = storm.Position;

            float dist = Vec2.Distance(storm.Position, sim.PlayerPosition);
            Assert.AreEqual(cfg.StormRingRange, dist, 0.35f, "稳定在 3x 环上");
            Assert.Greater(System.Math.Abs(late.X - early.X) + System.Math.Abs(late.Y - early.Y), 0.1f,
                "环上持续游走");
            Assert.GreaterOrEqual(sim.Bullets.Count, 2, "高频低压：0.5s 一发");
        }

        [Test]
        public void Overheat_CyclesFastThenThrottle()
        {
            EnemySim sim = NewSim();
            Enemy heater = sim.Spawn(EnemyKind.Overheat, new Vec2(30f, 0f));
            var cfg = sim.Config;

            Run(sim, 2.9f);
            Assert.AreEqual(StatTable.Speed(cfg.OverheatFastSpdLv), heater.Speed, 0.001f, "前 3s 全速");
            Assert.AreEqual("", heater.StateLabel);

            Run(sim, 0.2f);
            Assert.AreEqual(StatTable.Speed(cfg.OverheatSlowSpdLv), heater.Speed, 0.001f, "3s 后过热降频");
            Assert.AreEqual("throttling", heater.StateLabel);

            Run(sim, 2.2f);
            Assert.AreEqual(StatTable.Speed(cfg.OverheatFastSpdLv), heater.Speed, 0.001f, "2s 后恢复全速，循环");
        }

        [Test]
        public void Compiling_VulnerableWhileCompiling_ThenTransforms()
        {
            EnemySim sim = NewSim();
            Enemy unit = sim.Spawn(EnemyKind.Compiling, new Vec2(2f, 0f));
            var cfg = sim.Config;

            Assert.AreEqual(0f, unit.Atk);
            Assert.AreEqual(0f, unit.Speed);
            Assert.AreEqual(cfg.CompileDamageTakenMultiplier, unit.DamageTakenMultiplier);

            sim.DamageEnemy(unit, 3f);
            Assert.AreEqual(StatTable.Hp(1) - 3f * cfg.CompileDamageTakenMultiplier, unit.Hp, 0.001f,
                "编译期受伤 ×2");

            Run(sim, cfg.CompileTime + 0.6f);

            Assert.AreEqual(StatTable.Hp(cfg.CompileRuntimeHpLv), unit.MaxHp, 0.001f, "读条完变运行态");
            Assert.AreEqual(unit.MaxHp, unit.Hp, 0.001f);
            Assert.AreEqual(StatTable.Atk(cfg.CompileRuntimeAtkLv), unit.Atk);
            Assert.AreEqual(1f, unit.DamageTakenMultiplier);
            Assert.Less(Vec2.Distance(unit.Position, sim.PlayerPosition), 2f, "运行态开始追击");
        }

        [Test]
        public void ExceptionBullets_CarryExceptionLabel()
        {
            EnemySim sim = NewSim();
            Enemy shooter = sim.Spawn(EnemyKind.Exception, new Vec2(6f, 0f));

            Run(sim, 1.5f); // 到位即首发（约 1.3s），此时子弹还在飞

            Assert.IsNotEmpty(sim.Bullets[0].Label, "异常的子弹带异常类型名（表现层着色用）");
        }
    }
}
