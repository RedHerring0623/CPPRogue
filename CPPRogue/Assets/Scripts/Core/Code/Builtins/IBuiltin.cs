using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;

namespace CPPRogue.Core.Code.Builtins
{
    /// <summary>
    /// 内置函数（技能"动词"），词汇表的开放集。
    /// 实现规则：不持有状态，一切从 ExecContext 拿；副作用只通过 ICombatWorld 出去。
    /// 这样每个技能都能用 FakeWorld 单测，不需要 Unity。
    /// </summary>
    public interface IBuiltin
    {
        /// <summary>函数名（也是存档/掉落表引用的 ID，一旦发布就保持稳定）。</summary>
        string Name { get; }

        /// <summary>每次调用消耗的 CPU 周期（§4 的平衡阀门）。</summary>
        int CpuCost { get; }

        /// <summary>最大参数个数，拼装 UI 防呆用（attack 是 0 或 1 个参数，填 1）。</summary>
        int ParamCount { get; }

        void Invoke(ExecContext ctx, Value[] args);
    }
}
