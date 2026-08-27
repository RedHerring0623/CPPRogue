namespace CPPRogue.Core.Loot
{
    /// <summary>怪物的档位：掉落规则与出怪方式由档位决定（LootDesign.md §4）。</summary>
    public enum CodexTier
    {
        /// <summary>普通小怪：35% 掉 1×主材料。</summary>
        Normal,

        /// <summary>精英：必掉 2（主材料×1 + 随机一种×1）。</summary>
        Elite,

        /// <summary>附属怪：同普通规则，由母体生产，不占波次积分。</summary>
        Minion,
    }

    /// <summary>
    /// 图鉴条目：EnemyTable.json 的一行。只描述"是什么、掉什么"，
    /// 数值换算与行为仍归 StatTable / Brain——图鉴不参与战斗。
    /// </summary>
    public sealed class CodexEntry
    {
        /// <summary>稳定 ID（"bug"/"nullPointer"…），JSON 的 key，也是未来的存档引用。</summary>
        public string Id;

        /// <summary>中文显示名（短，EnemyDesign.md 命名纪律）。</summary>
        public string NameZh;

        /// <summary>英文梗名（出处注释）。</summary>
        public string NameEn;

        /// <summary>档位。</summary>
        public CodexTier Tier;

        /// <summary>属性等级 1-5（展示用；实际数值换算在 StatTable）。</summary>
        public int HpLv;
        public int AtkLv;
        public int SpdLv;

        /// <summary>出怪积分（波次配平，EnemyDesign.md §0）。</summary>
        public int SpawnScore;

        /// <summary>主题绑定的主材料。</summary>
        public MaterialKind MainDrop;

        /// <summary>图鉴描述（一句梗味介绍）。</summary>
        public string Desc;

        /// <summary>补充说明（可空：编译中的读条数值之类）。</summary>
        public string Note;

        /// <summary>附属怪 ID（可空：父进程 → 僵尸进程）。</summary>
        public string[] Summons;
    }
}
