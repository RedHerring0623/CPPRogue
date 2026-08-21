namespace CPPRogue.Core.Code.Runtime
{
    /// <summary>一次 tick 的执行结果。</summary>
    public enum ExecResult
    {
        /// <summary>正常执行完毕。部分语句可能因周期不足被"优化掉"，见 ExecContext.SkippedByBudget。</summary>
        Completed,

        /// <summary>死循环/超限被强制终止。游戏层据此施加"卡死"惩罚（§3.2 的 while 机制）。</summary>
        Hung,
    }

    /// <summary>循环上限或语句数上限被打爆——本 tick 判定为卡死。</summary>
    public sealed class InterpreterHungException : System.Exception
    {
        public InterpreterHungException(string reason) : base(reason) { }
    }

    /// <summary>调用了词汇表里不存在的函数（链接期应已拦截，这里是运行期双保险）。</summary>
    public sealed class UndefinedReferenceException : System.Exception
    {
        public string FunctionName { get; }

        public UndefinedReferenceException(string functionName)
            : base($"undefined reference to `{functionName}`")
        {
            FunctionName = functionName;
        }
    }
}
