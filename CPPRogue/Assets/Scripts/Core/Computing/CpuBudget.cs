namespace CPPRogue.Core.Computing
{
    /// <summary>
    /// CPU 周期预算（设计稿 §4 的核心平衡阀门）。
    /// 每条语句每 tick 消耗周期；周期不够的语句会被"优化掉"（不执行）。
    /// 本类只管预算的记账，不关心语句本身。
    /// </summary>
    public sealed class CpuBudget
    {
        private int _remaining;

        /// <summary>本 tick 的周期总量（由角色/装备决定）。</summary>
        public int Capacity { get; }

        /// <summary>本 tick 剩余可用周期。</summary>
        public int Remaining => _remaining;

        /// <summary>预算是否已耗尽。</summary>
        public bool IsExhausted => _remaining <= 0;

        public CpuBudget(int capacity)
        {
            if (capacity < 0)
                throw new System.ArgumentOutOfRangeException(nameof(capacity), "周期容量不能为负数。");
            Capacity = capacity;
            _remaining = capacity;
        }

        /// <summary>
        /// 尝试消耗 <paramref name="cycles"/> 个周期。
        /// 不足时返回 false，且不扣任何周期——对应"语句被优化掉，周期留给后面的语句"。
        /// </summary>
        public bool TrySpend(int cycles)
        {
            if (cycles < 0)
                throw new System.ArgumentOutOfRangeException(nameof(cycles), "消耗的周期数不能为负数。");
            if (_remaining < cycles)
                return false;
            _remaining -= cycles;
            return true;
        }

        /// <summary>新 tick 开始时恢复满额。</summary>
        public void Reset()
        {
            _remaining = Capacity;
        }
    }
}
