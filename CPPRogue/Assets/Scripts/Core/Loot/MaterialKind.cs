namespace CPPRogue.Core.Loot
{
    /// <summary>
    /// 升级材料三件套（LootDesign.md §0，命名定稿 2026-08-27）。
    /// 三个都按数量计算：纯整数计数，无品质档位；合成升档只发生在代码块上。
    /// </summary>
    public enum MaterialKind
    {
        /// <summary>时间复杂度：延长单次 tick 的执行时间窗。</summary>
        TimeSlice,

        /// <summary>空间复杂度：提升 Routine 行数上限。</summary>
        Ram,

        /// <summary>技能树货币（技能树未开放，先记账）。</summary>
        Driver,
    }

    /// <summary>材料的稳定 ID、展示名与效果文案（图鉴 / 掉落 UI 共用）。</summary>
    public static class Materials
    {
        /// <summary>EnemyTable.json 与存档引用用的稳定 ID，发布后不变。</summary>
        public static string Id(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.TimeSlice: return "timeSlice";
                case MaterialKind.Ram: return "ram";
                case MaterialKind.Driver: return "driver";
                default: return kind.ToString();
            }
        }

        public static string Name(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.TimeSlice: return "时间片";
                case MaterialKind.Ram: return "RAM";
                case MaterialKind.Driver: return "驱动块";
                default: return kind.ToString();
            }
        }

        /// <summary>图鉴里的效果一句话（详细规则见 LootDesign.md §1/§2）。</summary>
        public static string Effect(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.TimeSlice: return "延长单次 tick 的执行时间窗（超窗：减速 → 掉血 → 眩晕）";
                case MaterialKind.Ram: return "提升 Routine 行数上限";
                case MaterialKind.Driver: return "技能树货币（技能树未开放）";
                default: return "";
            }
        }

        public static bool TryParseId(string id, out MaterialKind kind)
        {
            switch (id)
            {
                case "timeSlice": kind = MaterialKind.TimeSlice; return true;
                case "ram": kind = MaterialKind.Ram; return true;
                case "driver": kind = MaterialKind.Driver; return true;
                default: kind = MaterialKind.TimeSlice; return false;
            }
        }
    }
}
