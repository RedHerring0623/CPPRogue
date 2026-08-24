namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 怪物原型表：EnemyDesign.md 里每种怪的等级属性（hp/atk/spd lv）→ StatTable 数值，
    /// 外加碰撞半径与 Brain 装配。EnemySim.Spawn 只从这里取配置，
    /// 保证"设计文档 → 原型表 → 实例"一条线，改平衡不散落在业务代码里。
    /// </summary>
    public static class EnemyArchetypes
    {
        /// <summary>按原型创建实例。generation 只对深拷贝有意义。</summary>
        public static Enemy Create(EnemyKind kind, Vec2 position, int generation, System.Random rng, EnemySimConfig cfg)
        {
            switch (kind)
            {
                case EnemyKind.Bug:
                    return Build(kind, "Bug", 2, 1, 2, 0.35f, position, ChaserBrain.Instance);

                case EnemyKind.NullPointer:
                    return Build(kind, "空指针", 1, 1, 3, 0.3f, position, ChaserBrain.Instance);

                case EnemyKind.Breakpoint:
                    return Build(kind, "断点", 2, 1, 2, 0.4f, position, new BreakpointBrain());

                case EnemyKind.Exception:
                    return Build(kind, "异常", 1, 2, 2, 0.35f, position, new RangedBrain());

                case EnemyKind.DeepCopy:
                    return BuildDeepCopy(generation, position);

                case EnemyKind.Watchdog:
                    return Build(kind, "看门狗", 3, 1, 1, 0.45f, position, new WatchdogBrain(cfg.WatchdogFuse));

                case EnemyKind.Compiling:
                    return BuildCompiling(position, cfg);

                case EnemyKind.Overheat:
                    return Build(kind, "过热", 2, 1, 4, 0.4f, position, new OverheatBrain(cfg));

                case EnemyKind.Storm:
                    return Build(kind, "风暴", 1, 1, 2, 0.35f, position,
                        new StormBrain(rng.Next(2)));

                case EnemyKind.Parent:
                    return Build(kind, "父进程", 4, 1, 1, 0.55f, position, new SpawnerBrain(rng, cfg));

                case EnemyKind.Zombie:
                    return Build(kind, "僵尸进程", 1, 1, 2, 0.3f, position, ChaserBrain.Instance);

                default:
                    return Build(kind, kind.ToString(), 1, 1, 1, 0.35f, position, ChaserBrain.Instance);
            }
        }

        /// <summary>深拷贝世代表（§2.5）：hp4 本体 → hp2 → hp1，hp1 死才是真死。</summary>
        private static readonly int[] DeepCopyHpLv = { 4, 2, 1 };

        private static Enemy BuildDeepCopy(int generation, Vec2 position)
        {
            int gen = generation < 0 ? 0 : (generation > 2 ? 2 : generation);
            string name = gen == 0 ? "深拷贝" : "深拷贝·副本";
            float radius = 0.5f - gen * 0.08f;
            Enemy e = Build(EnemyKind.DeepCopy, name, DeepCopyHpLv[gen], 1, 2, radius, position, ChaserBrain.Instance);
            e.Generation = gen;
            return e;
        }

        /// <summary>编译中（§2.7）：出生即编译态——hp1 atk0 spd0、受伤 ×2，读完条由 Brain 换成运行态数值。</summary>
        private static Enemy BuildCompiling(Vec2 position, EnemySimConfig cfg)
        {
            Enemy e = Build(EnemyKind.Compiling, "编译中", 1, 1, 2, 0.4f, position, new CompilingBrain(cfg.CompileTime));
            e.Atk = 0f;
            e.Speed = 0f;
            e.DamageTakenMultiplier = cfg.CompileDamageTakenMultiplier;
            return e;
        }

        private static Enemy Build(EnemyKind kind, string name, int hpLv, int atkLv, int spdLv,
            float radius, Vec2 position, IEnemyBrain brain)
        {
            return new Enemy
            {
                Kind = kind,
                DisplayName = name,
                Brain = brain,
                Position = position,
                Radius = radius,
                MaxHp = StatTable.Hp(hpLv),
                Hp = StatTable.Hp(hpLv),
                Atk = StatTable.Atk(atkLv),
                Speed = StatTable.Speed(spdLv),
            };
        }
    }
}
