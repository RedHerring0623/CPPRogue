using CPPRogue.Core.Enemies;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Enemies
{
    /// <summary>
    /// EnemySim 结算规则的单元测试：§0 碰撞模型、深拷贝分裂、看门狗自爆、
    /// 父进程孵化与 reap、子弹命中。全部纯逻辑，dotnet test 可跑。
    /// </summary>
    [TestFixture]
    public class EnemySimTests
    {
        private static EnemySim NewSim(EnemySimConfig cfg = null)
        {
            var sim = new EnemySim(cfg ?? new EnemySimConfig(), seed: 7);
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
        public void Spawn_UsesArchetypeStats_FromEnemyDesign()
        {
            EnemySim sim = NewSim();
            Enemy bug = sim.Spawn(EnemyKind.Bug, new Vec2(5f, 0f));
            Enemy fast = sim.Spawn(EnemyKind.NullPointer, new Vec2(5f, 0f));

            // 2026-08-31 数值改版：hp/atk 是图鉴直值（bug hp2 就是 2 点生命），spd 仍是等级换算
            Assert.AreEqual(2f, bug.MaxHp);
            Assert.AreEqual(1f, bug.Atk);
            Assert.AreEqual(StatTable.Speed(2), bug.Speed);

            // 空指针：快而脆（hp1 spd3）
            Assert.AreEqual(1f, fast.MaxHp);
            Assert.AreEqual(StatTable.Speed(3), fast.Speed);
        }

        [Test]
        public void Contact_GlobalInvulnIsTheOnlyHitInterval()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(0.5f, 0f)); // 圆心距 0.5 < 半径和 0.85，持续重叠

            sim.Step(0.01f);
            Assert.AreEqual(10f - 1f, sim.PlayerHp, 0.001f, "接触瞬间结算一次（玩家 10 血，bug atk1 直值）");

            Run(sim, 0.05f, 0.05f); // 0.1s 无敌帧内
            Assert.AreEqual(10f - 1f, sim.PlayerHp, 0.001f, "无敌帧内不重复结算");

            Run(sim, 0.1f, 0.05f); // 越过 0.1s：唯一受击间隔到期，持续重叠就继续掉血
            Assert.AreEqual(10f - 1f * 2f, sim.PlayerHp, 0.001f, "间隔到期再次结算");
        }

        [Test]
        public void Contact_GlobalInvuln_BlocksSimultaneousSecondEnemy()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(0.5f, 0f));
            sim.Spawn(EnemyKind.Bug, new Vec2(-0.5f, 0f));

            sim.Step(0.01f);
            Assert.AreEqual(10f - 1f, sim.PlayerHp, 0.001f,
                "两只同时贴脸：0.3s 全局无敌帧保证只掉一次血");
        }

        [Test]
        public void PlayerBullet_KillsDeepCopy_SplitsDownGenerations()
        {
            EnemySim sim = NewSim();
            Enemy original = sim.Spawn(EnemyKind.DeepCopy, new Vec2(0f, 6f));

            // 一枪秒掉 hp4 本体
            sim.SpawnPlayerBullet(new Vec2(0f, 8f), new Vec2(0f, -1f), damage: 999f, hitRadius: 0.2f);
            Run(sim, 0.5f);

            Assert.IsTrue(original.Dead, "本体死亡");
            Assert.AreEqual(2, AliveCount(sim), "分裂为两份 hp2 副本");
            Assert.IsTrue(System.Linq.Enumerable.All(sim.Enemies, e => e.Dead || (e.Generation == 1 && e.MaxHp == 2f)),
                "分裂为两份 hp2 副本（直值）");

            // 打死一份 hp2 → 两份 hp1（直杀不清尸，断言只数活体）
            Enemy copy = FirstAlive(sim);
            sim.DamageEnemy(copy, 999f);
            int gen2 = 0;
            foreach (Enemy e in sim.Enemies)
                if (!e.Dead && e.Generation == 2 && e.MaxHp == 1f)
                    gen2++;
            Assert.AreEqual(2, gen2, "分裂出的两份是 hp1 世代");
            Assert.AreEqual(3, AliveCount(sim), "另一份 hp2 副本还活着");

            // hp1 死亡才是真死：打死一份 hp1，不再分裂
            Enemy lastGen = null;
            foreach (Enemy e in sim.Enemies)
                if (!e.Dead && e.Generation == 2)
                {
                    lastGen = e;
                    break;
                }
            sim.DamageEnemy(lastGen, 999f);
            Assert.AreEqual(2, AliveCount(sim), "hp1 副本不再分裂");
        }

        private static int AliveCount(EnemySim sim)
        {
            int n = 0;
            foreach (Enemy e in sim.Enemies)
                if (!e.Dead)
                    n++;
            return n;
        }

        private static Enemy FirstAlive(EnemySim sim)
        {
            foreach (Enemy e in sim.Enemies)
                if (!e.Dead)
                    return e;
            return null;
        }

        [Test]
        public void Watchdog_FuseExpiry_ExplodesAndDies()
        {
            var cfg = new EnemySimConfig { WatchdogFuse = 0.5f };
            cfg.Recalc();
            EnemySim sim = NewSim(cfg);
            // 放在接触圈外、自爆圈内（半径 4.5）：spd1 走 0.5s 只挪 0.6，不会贴身，隔离出纯爆炸伤害
            Enemy watchdog = sim.Spawn(EnemyKind.Watchdog, new Vec2(2.5f, 0f));

            Run(sim, 1f);

            Assert.IsTrue(watchdog.Dead);
            bool exploded = false;
            foreach (SimEvent ev in sim.DrainEvents())
                if (ev.Type == SimEventType.Exploded)
                    exploded = true;
            Assert.IsTrue(exploded, "自爆产生 Exploded 事件");
            Assert.AreEqual(10f - 3f, sim.PlayerHp, 0.001f, "只吃自爆（atk3），无接触干扰");
        }

        [Test]
        public void Watchdog_KilledBeforeFuse_NoExplosion()
        {
            var cfg = new EnemySimConfig { WatchdogFuse = 5f };
            cfg.Recalc();
            EnemySim sim = NewSim(cfg);
            Enemy watchdog = sim.Spawn(EnemyKind.Watchdog, new Vec2(1f, 0f));

            sim.DamageEnemy(watchdog, 999f);
            Run(sim, 6f);

            foreach (SimEvent ev in sim.DrainEvents())
                Assert.AreNotEqual(SimEventType.Exploded, ev.Type, "提前击杀则解除自爆");
            Assert.AreEqual(10f, sim.PlayerHp, 0.001f, "死怪不移动不碰撞");
        }

        [Test]
        public void Parent_SpawnsZombiesUpToCap_ThenRefills()
        {
            EnemySim sim = NewSim();
            Enemy parent = sim.Spawn(EnemyKind.Parent, new Vec2(8f, 0f));

            Run(sim, 3.2f);
            Assert.AreEqual(1, sim.CountChildren(parent), "3s 孵第一只");

            Run(sim, 21.2f);   // 每 3s 一只，24s 出满
            Assert.AreEqual(8, sim.CountChildren(parent), "封顶 8 只，多孵被拒");

            // 杀一只，父进程存活 → 按节拍补回来（"杀不干净"）
            Enemy victim = null;
            foreach (Enemy e in sim.Enemies)
                if (e.ParentId == parent.Id && !e.Dead)
                {
                    victim = e;
                    break;
                }
            sim.DamageEnemy(victim, 999f);
            Assert.AreEqual(7, sim.CountChildren(parent));
            Run(sim, 3.2f);
            Assert.AreEqual(8, sim.CountChildren(parent), "父进程重新孵化补位");
        }

        [Test]
        public void Parent_Killed_ReapsAllZombies()
        {
            EnemySim sim = NewSim();
            Enemy parent = sim.Spawn(EnemyKind.Parent, new Vec2(8f, 0f));
            Run(sim, 3.2f);
            Assume.That(sim.CountChildren(parent), Is.EqualTo(1));

            sim.DamageEnemy(parent, 999f);

            Assert.AreEqual(0, sim.CountChildren(parent), "父进程死亡，僵尸全部 reap");
            bool reaped = false;
            foreach (SimEvent ev in sim.DrainEvents())
                if (ev.Type == SimEventType.ChildrenReaped)
                    reaped = true;
            Assert.IsTrue(reaped);
        }

        [Test]
        public void EnemyBullets_SameTickHit_InvulnLetsOnlyOneThrough()
        {
            EnemySim sim = NewSim();
            // 用编译态怪当炮台：读条期站桩，排除追击碰撞的干扰
            Enemy shooter = sim.Spawn(EnemyKind.Compiling, new Vec2(0f, 5f));

            sim.SpawnEnemyBullet(shooter, sim.PlayerPosition, speed: 3f, atk: 5f);
            sim.SpawnEnemyBullet(shooter, sim.PlayerPosition, speed: 3f, atk: 5f);
            Run(sim, 2f);

            Assert.AreEqual(10f - 5f, sim.PlayerHp, 0.001f,
                "两发同帧命中：无敌帧只放进去一发，另一发穿过");
        }

        [Test]
        public void NearestEnemy_ReturnsClosest()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(3f, 0f));
            Enemy near = sim.Spawn(EnemyKind.Bug, new Vec2(1f, 0f));
            sim.Spawn(EnemyKind.Bug, new Vec2(-2f, 0f));

            Enemy found = sim.NearestEnemy(new Vec2(0f, 0f));
            Assert.AreEqual(near.Id, found.Id);
        }

        [Test]
        public void Contact_KnockbackPushesPlayerAwayFromEnemy()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(0.5f, 0f)); // 玩家右侧贴脸

            sim.Step(0.01f);

            Vec2 kb = sim.PlayerKnockback;
            Assert.AreEqual(sim.Config.ContactKnockbackSpeed, kb.Magnitude, 0.001f, "初速取配置值");
            Assert.AreEqual(-1f, kb.Normalized.X, 0.001f, "击退方向：远离敌人");
            Assert.AreEqual(0f, kb.Normalized.Y, 0.001f);
        }

        [Test]
        public void Knockback_DecaysToZero()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(0.5f, 0f));
            sim.Step(0.01f);
            Assume.That(sim.PlayerKnockback.Magnitude, Is.GreaterThan(0f));

            sim.Clear();   // 移走伤害源，避免持续重叠反复刷新击退
            Run(sim, sim.Config.KnockbackTime + 0.05f);
            Assert.LessOrEqual(sim.PlayerKnockback.Magnitude, 0.001f, "击退速度线性衰减归零");
        }

        [Test]
        public void Knockback_PerfectlyOverlapped_StillPushes()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(0f, 0f)); // 圆心完全重合，方向未定义

            sim.Step(0.01f);

            Assert.Greater(sim.PlayerKnockback.Magnitude, 0f, "完全重合也按固定方向弹开");
        }

        [Test]
        public void HealPlayer_ClampsAtMax()
        {
            EnemySim sim = NewSim();
            sim.Spawn(EnemyKind.Bug, new Vec2(0.5f, 0f));
            sim.Step(0.01f); // 先掉一次血

            sim.HealPlayer(999f);
            Assert.AreEqual(sim.PlayerMaxHp, sim.PlayerHp, 0.001f);
        }
    }
}
