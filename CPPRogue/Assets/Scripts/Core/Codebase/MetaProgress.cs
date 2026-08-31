using CPPRogue.Core.Loot;

namespace CPPRogue.Core.Codebase
{
    /// <summary>
    /// 局外成长：行数与时间窗的兑换进度（材料在兑换时从 CodebaseState 扣除）。
    /// 数值定稿 2026-08-31（锚点，待实测）：行数基础 2，+1 行消耗 RAM 1,2,4,8…（等比）；
    /// 时间窗基础 1s，+0.5s 消耗时间片 1,2,4,8…（等比）。
    /// </summary>
    public sealed class MetaProgress
    {
        public const int BaseLines = 2;
        public const float BaseWindowSeconds = 1f;
        public const float WindowStepSeconds = 0.5f;

        /// <summary>已兑换的 +1 行次数。</summary>
        public int LineBonus { get; set; }

        /// <summary>已兑换的 +0.5s 次数。</summary>
        public int WindowSteps { get; set; }

        public int MaxLines => BaseLines + LineBonus;

        public float WindowSeconds => BaseWindowSeconds + WindowStepSeconds * WindowSteps;

        /// <summary>下一次 +1 行的花费：1,2,4,8…（2^已兑换次数）。</summary>
        public int NextLineCost => LineBonus >= 30 ? int.MaxValue : 1 << LineBonus;

        /// <summary>下一次 +0.5s 的花费：1,2,4,8…。</summary>
        public int NextWindowCost => WindowSteps >= 30 ? int.MaxValue : 1 << WindowSteps;

        /// <summary>用 RAM 兑换 +1 行。材料不足返回 false。</summary>
        public bool TryUpgradeLines(CodebaseState warehouse)
        {
            if (!warehouse.Spend(MaterialKind.Ram, NextLineCost))
                return false;
            LineBonus++;
            return true;
        }

        /// <summary>用时间片兑换 +0.5s 执行时间窗。材料不足返回 false。</summary>
        public bool TryUpgradeWindow(CodebaseState warehouse)
        {
            if (!warehouse.Spend(MaterialKind.TimeSlice, NextWindowCost))
                return false;
            WindowSteps++;
            return true;
        }
    }
}
