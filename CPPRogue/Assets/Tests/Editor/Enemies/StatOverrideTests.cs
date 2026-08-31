using System.Collections.Generic;
using CPPRogue.Core.Enemies;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Enemies
{
    /// <summary>
    /// 图鉴直值改版（2026-08-31）测试：hp/atk 不走等级换算；
    /// 覆盖表（EnemyStatOverride，来自 EnemyTable.json）优先于内置数值；
    /// 锚点：attack(1) 必须两下打死 bug（hp2）。
    /// </summary>
    [TestFixture]
    public class StatOverrideTests
    {
        [Test]
        public void 锚点_attack1两下打死Bug()
        {
            var sim = new EnemySim(new EnemySimConfig(), seed: 1);
            sim.SetPlayerPosition(new Vec2(0f, 0f));
            Enemy bug = sim.Spawn(EnemyKind.Bug, new Vec2(5f, 0f));

            Assert.AreEqual(2f, bug.MaxHp, "bug hp2 是 2 点生命（直值）");

            sim.DamageEnemy(bug, 1f);   // attack(1) = 1 点伤害
            Assert.IsFalse(bug.Dead, "第一下打死不了");
            sim.DamageEnemy(bug, 1f);
            Assert.IsTrue(bug.Dead, "第二下死");
        }

        [Test]
        public void 覆盖表_图鉴数值优先于内置()
        {
            var overrides = new Dictionary<EnemyKind, EnemyStatOverride>
            {
                [EnemyKind.Bug] = new EnemyStatOverride { Hp = 7f, Atk = 5f },
            };
            var sim = new EnemySim(new EnemySimConfig(), seed: 1, overrides);
            sim.SetPlayerPosition(new Vec2(0f, 0f));
            Enemy bug = sim.Spawn(EnemyKind.Bug, new Vec2(5f, 0f));

            Assert.AreEqual(7f, bug.MaxHp);
            Assert.AreEqual(5f, bug.Atk);
            Assert.AreEqual(StatTable.Speed(2), bug.Speed, "spd 仍走等级换算");

            // 没覆盖的怪用内置直值
            Enemy fast = sim.Spawn(EnemyKind.NullPointer, new Vec2(5f, 0f));
            Assert.AreEqual(1f, fast.MaxHp);
            Assert.AreEqual(1f, fast.Atk);
        }

        [Test]
        public void 覆盖表_深拷贝逐代减半()
        {
            var overrides = new Dictionary<EnemyKind, EnemyStatOverride>
            {
                [EnemyKind.DeepCopy] = new EnemyStatOverride { Hp = 8f, Atk = 2f },
            };
            var sim = new EnemySim(new EnemySimConfig(), seed: 1, overrides);
            sim.SetPlayerPosition(new Vec2(0f, 0f));

            Enemy original = sim.Spawn(EnemyKind.DeepCopy, new Vec2(0f, 6f));
            Assert.AreEqual(8f, original.MaxHp, "本体 hp8");

            sim.DamageEnemy(original, 999f);
            int children = 0;
            foreach (Enemy e in sim.Enemies)
            {
                if (!e.Dead && e.Generation == 1)
                {
                    Assert.AreEqual(4f, e.MaxHp, "子代减半");
                    Assert.AreEqual(2f, e.Atk);
                    children++;
                }
            }
            Assert.AreEqual(2, children);
        }
    }
}
