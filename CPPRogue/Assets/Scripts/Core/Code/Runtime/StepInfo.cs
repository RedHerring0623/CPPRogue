using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Code.Runtime
{
    /// <summary>一条语句执行一步的结果——UI 高亮/演出的最小单元。</summary>
    public enum StepStatus
    {
        /// <summary>正常执行。</summary>
        Executed,

        /// <summary>周期不足，被"优化掉"（未执行，不扣周期）。</summary>
        OptimizedOut,

        /// <summary>死循环/超限，本 tick 被强制终止时正在执行的语句。</summary>
        Hung,
    }

    /// <summary>
    /// 步骤机吐出的一步：执行了哪条语句（Block 引用，UI 用它定位高亮行）、结果如何。
    /// </summary>
    public sealed class StepInfo
    {
        public Block Statement { get; }
        public StepStatus Status { get; }

        public StepInfo(Block statement, StepStatus status)
        {
            Statement = statement;
            Status = status;
        }
    }
}
