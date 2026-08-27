using System.Collections.Generic;
using CPPRogue.Core.Enemies;

namespace CPPRogue.Core.Loot
{
    /// <summary>
    /// 代码内置的图鉴表：与 Assets/Resources/EnemyTable.json 逐字段镜像，
    /// 作为 JSON 缺失/损坏时的回退。两边由 EnemyCodexTests 的同步测试锁死，
 /// 改表必须两处一起改（调参流程见 doc/CODEX.md）。
    /// </summary>
    public static class EnemyCodex
    {
        /// <summary>档位 → 掉落规则文案（LootDesign.md §4 定稿数值）。</summary>
        public static string TierRule(CodexTier tier)
        {
            switch (tier)
            {
                case CodexTier.Normal: return "普通：35% 掉 1×主材料";
                case CodexTier.Elite: return "精英：必掉 2（主材料×1 + 随机一种×1）";
                case CodexTier.Minion: return "附属：同普通规则；由母体生产，不占波次积分";
                default: return "";
            }
        }

        /// <summary>图鉴 ID → EnemyKind。图鉴数据与模拟层的连接点只有这一个映射。</summary>
        public static EnemyKind KindForId(string id)
        {
            switch (id)
            {
                case "bug": return EnemyKind.Bug;
                case "nullPointer": return EnemyKind.NullPointer;
                case "breakpoint": return EnemyKind.Breakpoint;
                case "exception": return EnemyKind.Exception;
                case "deepCopy": return EnemyKind.DeepCopy;
                case "watchdog": return EnemyKind.Watchdog;
                case "compiling": return EnemyKind.Compiling;
                case "overheat": return EnemyKind.Overheat;
                case "storm": return EnemyKind.Storm;
                case "parentProcess": return EnemyKind.Parent;
                case "zombie": return EnemyKind.Zombie;
                default: throw new KeyNotFoundException($"EnemyCodex: 未知图鉴 ID '{id}'");
            }
        }

        /// <summary>EnemyKind → 图鉴 ID（KindForId 的反查）。</summary>
        public static string IdForKind(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Bug: return "bug";
                case EnemyKind.NullPointer: return "nullPointer";
                case EnemyKind.Breakpoint: return "breakpoint";
                case EnemyKind.Exception: return "exception";
                case EnemyKind.DeepCopy: return "deepCopy";
                case EnemyKind.Watchdog: return "watchdog";
                case EnemyKind.Compiling: return "compiling";
                case EnemyKind.Overheat: return "overheat";
                case EnemyKind.Storm: return "storm";
                case EnemyKind.Parent: return "parentProcess";
                case EnemyKind.Zombie: return "zombie";
                default: throw new KeyNotFoundException($"EnemyCodex: 未知怪物种类 {kind}");
            }
        }

        /// <summary>内置图鉴表（与 EnemyTable.json 镜像；源头是 EnemyDesign.md / LootDesign.md）。</summary>
        public static List<CodexEntry> Default()
        {
            return new List<CodexEntry>
            {
                Entry("bug", "Bug", "Bug", CodexTier.Normal, 2, 1, 2, 1, MaterialKind.Driver,
                    "第一个被写进世界的缺陷。无法复现，却无处不在。"),
                Entry("nullPointer", "空指针", "Null Pointer", CodexTier.Normal, 1, 1, 3, 2, MaterialKind.TimeSlice,
                    "解引用它的那一刻，一切戛然而止。"),
                Entry("breakpoint", "断点", "Breakpoint", CodexTier.Normal, 2, 1, 2, 2, MaterialKind.TimeSlice,
                    "命中断点，检查变量，然后按 F5——朝着你继续。"),
                Entry("exception", "异常", "Exception", CodexTier.Normal, 1, 2, 2, 2, MaterialKind.Driver,
                    "它抛出的每个异常，都得由你来 catch。"),
                Entry("deepCopy", "深拷贝", "Deep Copy", CodexTier.Elite, 4, 1, 2, 4, MaterialKind.Ram,
                    "你以为解决了它，其实只是让它开始备份自己。"),
                Entry("watchdog", "看门狗", "Watchdog", CodexTier.Elite, 3, 1, 1, 3, MaterialKind.TimeSlice,
                    "没人喂它，它就打算复位整个世界。"),
                Entry("compiling", "编译中", "Compiling", CodexTier.Elite, 3, 2, 3, 3, MaterialKind.Driver,
                    "趁它还在读条动手。等条读完，性质就变了。",
                    note: "出场为编译态 hp1/atk0/spd0 且受伤×2，读条 5s 后转为上述运行态数值"),
                Entry("overheat", "过热", "Overheat", CodexTier.Elite, 2, 1, 4, 3, MaterialKind.TimeSlice,
                    "全速三秒，散热两秒。活过这个节奏你就赢了。"),
                Entry("storm", "风暴", "Interrupt Storm", CodexTier.Elite, 1, 1, 2, 4, MaterialKind.Driver,
                    "中断，中断，中断。你上一条指令还没执行完。"),
                Entry("parentProcess", "父进程", "Parent Process", CodexTier.Elite, 4, 1, 1, 4, MaterialKind.Ram,
                    "清理子进程没有意义，去找根，一次 reap 到位。",
                    summons: new[] { "zombie" }),
                Entry("zombie", "僵尸进程", "Zombie Process", CodexTier.Minion, 1, 1, 2, 0, MaterialKind.Ram,
                    "已经死了，却还占着进程表的一格。"),
            };
        }

        private static CodexEntry Entry(string id, string nameZh, string nameEn, CodexTier tier,
            int hp, int atk, int spd, int score, MaterialKind drop, string desc,
            string note = null, string[] summons = null)
        {
            return new CodexEntry
            {
                Id = id,
                NameZh = nameZh,
                NameEn = nameEn,
                Tier = tier,
                HpLv = hp,
                AtkLv = atk,
                SpdLv = spd,
                SpawnScore = score,
                MainDrop = drop,
                Desc = desc,
                Note = note,
                Summons = summons,
            };
        }
    }
}
