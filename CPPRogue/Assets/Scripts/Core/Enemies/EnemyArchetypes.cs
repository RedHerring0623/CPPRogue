using System.Collections.Generic;

namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 怪物原型表：EnemyDesign.md 里每种怪的 hp/atk（图鉴直值，2026-08-31 改版：
    /// bug 的 hp2 就是 2 点生命）+ spd 等级（1-5 → StatTable）+ 碰撞半径与 Brain 装配。
    /// 表里的数字与 EnemyTable.json / EnemyCodex.Default 三方一致（同步测试锁定）；
    /// 运行时可用 <see cref="EnemyStatOverride"/> 覆盖 hp/atk——表现层从图鉴数据注入，
    /// 改 JSON 数值即刻生效。EnemySim.Spawn 只从这里取配置，改平衡不散落在业务代码里。
    /// </summary>
    public static class EnemyArchetypes
    {
        /// <summary>按原型创建实例。generation 只对深拷贝有意义。</summary>
        public static Enemy Create(EnemyKind kind, Vec2 position, int generation, System.Random rng, EnemySimConfig cfg,
            IReadOnlyDictionary<EnemyKind, EnemyStatOverride> overrides = null)
        {
            switch (kind)
            {
                case EnemyKind.Bug:
                    return Build(kind, "Bug", 2, 1, 2, 0.35f, position, ChaserBrain.Instance, overrides);

                case EnemyKind.NullPointer:
                    return Build(kind, "空指针", 1, 1, 3, 0.3f, position, ChaserBrain.Instance, overrides);

                case EnemyKind.Breakpoint:
                    return Build(kind, "断点", 2, 1, 2, 0.4f, position, new BreakpointBrain(), overrides);

                case EnemyKind.Exception:
                    return Build(kind, "异常", 1, 2, 2, 0.35f, position, new RangedBrain(), overrides);

                case EnemyKind.DeepCopy:
                    return BuildDeepCopy(generation, position, overrides);

                case EnemyKind.Watchdog:
                    return Build(kind, "看门狗", 3, 1, 1, 0.45f, position, new WatchdogBrain(cfg.WatchdogFuse), overrides);

                case EnemyKind.Compiling:
                    return BuildCompiling(position, cfg, overrides);

                case EnemyKind.Overheat:
                    return Build(kind, "过热", 2, 1, 4, 0.4f, position, new OverheatBrain(cfg), overrides);

                case EnemyKind.Storm:
                    return Build(kind, "风暴", 1, 1, 2, 0.35f, position,
                        new StormBrain(rng.Next(2)), overrides);

                case EnemyKind.Parent:
                    return Build(kind, "父进程", 4, 1, 1, 0.55f, position, new SpawnerBrain(rng, cfg), overrides);

                case EnemyKind.Zombie:
                    return Build(kind, "僵尸进程", 1, 1, 2, 0.3f, position, ChaserBrain.Instance, overrides);

                default:
                    return Build(kind, kind.ToString(), 1, 1, 1, 0.35f, position, ChaserBrain.Instance, overrides);
            }
        }

        /// <summary>深拷贝世代表（§2.5）：hp4 本体 → hp2 → hp1，hp1 死才是真死（每代减半，下限 1）。</summary>
        private static Enemy BuildDeepCopy(int generation, Vec2 position,
            IReadOnlyDictionary<EnemyKind, EnemyStatOverride> overrides)
        {
            int gen = generation < 0 ? 0 : (generation > 2 ? 2 : generation);
            string name = gen == 0 ? "深拷贝" : "深拷贝·副本";
            float radius = 0.5f - gen * 0.08f;
            float baseHp = StatOf(overrides, EnemyKind.DeepCopy)?.Hp ?? 4f;
            float atk = StatOf(overrides, EnemyKind.DeepCopy)?.Atk ?? 1f;
            float hp = baseHp;
            for (int i = 0; i < gen; i++)
                hp *= 0.5f;
            if (hp < 1f)
                hp = 1f;
            Enemy e = Build(EnemyKind.DeepCopy, name, 0, 0, 2, radius, position, ChaserBrain.Instance, overrides);
            e.MaxHp = hp;
            e.Hp = hp;
            e.Atk = atk;
            e.Generation = gen;
            return e;
        }

        /// <summary>编译中（§2.7）：出生即编译态——hp1 atk0 spd0、受伤 ×2，读完条由 Brain 换成运行态数值（cfg 直值）。</summary>
        private static Enemy BuildCompiling(Vec2 position, EnemySimConfig cfg,
            IReadOnlyDictionary<EnemyKind, EnemyStatOverride> overrides)
        {
            Enemy e = Build(EnemyKind.Compiling, "编译中", 1, 1, 2, 0.4f, position, new CompilingBrain(cfg.CompileTime), overrides);
            e.Atk = 0f;
            e.Speed = 0f;
            e.DamageTakenMultiplier = cfg.CompileDamageTakenMultiplier;
            return e;
        }

        private static EnemyStatOverride StatOf(IReadOnlyDictionary<EnemyKind, EnemyStatOverride> overrides, EnemyKind kind)
        {
            return overrides != null && overrides.TryGetValue(kind, out EnemyStatOverride stat) ? stat : null;
        }

        /// <summary>hp/atk 是图鉴直值（覆盖表优先）；spdLv 仍是等级，走 StatTable。</summary>
        private static Enemy Build(EnemyKind kind, string name, int hp, int atk, int spdLv,
            float radius, Vec2 position, IEnemyBrain brain,
            IReadOnlyDictionary<EnemyKind, EnemyStatOverride> overrides = null)
        {
            EnemyStatOverride stat = StatOf(overrides, kind);
            float hpValue = stat?.Hp ?? hp;
            float atkValue = stat?.Atk ?? atk;
            return new Enemy
            {
                Kind = kind,
                DisplayName = name,
                Brain = brain,
                Position = position,
                Radius = radius,
                MaxHp = hpValue,
                Hp = hpValue,
                Atk = atkValue,
                Speed = StatTable.Speed(spdLv),
            };
        }
    }
}
