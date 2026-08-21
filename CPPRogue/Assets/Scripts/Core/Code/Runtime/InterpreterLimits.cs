namespace CPPRogue.Core.Code.Runtime
{
    /// <summary>
    /// 解释器安全阀（§4：死循环保底、防超频滚雪球）。全部可配置，超限即判定本 tick 卡死。
    /// </summary>
    public sealed class InterpreterLimits
    {
        /// <summary>单个循环在一次执行中最多跑多少次迭代。while(true) 到顶 = 卡死（Hung）。</summary>
        public int MaxLoopIterations = 1000;

        /// <summary>单个 tick 最多执行多少条语句。</summary>
        public int MaxStatementsPerTick = 10000;

        public static InterpreterLimits Default => new InterpreterLimits();
    }
}
