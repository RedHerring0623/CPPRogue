using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Combat;
using CPPRogue.Core.Computing;

namespace CPPRogue.Core.Code.Runtime
{
    /// <summary>
    /// 只读属性源：hp、tick 这类系统量从这里读，不占玩家变量槽（§4 变量槽位）。
    /// </summary>
    public interface IPropertySource
    {
        bool TryRead(string name, out Value value);
    }

    internal sealed class NonePropertySource : IPropertySource
    {
        public static readonly NonePropertySource Instance = new NonePropertySource();

        public bool TryRead(string name, out Value value)
        {
            value = Value.Zero;
            return false;
        }
    }

    /// <summary>
    /// 一次 tick 执行的全部环境。builtin 不持有任何全局状态，一切从这里拿：
    /// 变量（Blackboard）、周期（CpuBudget）、世界出口（ICombatWorld）、随机源（可注入种子）。
    /// 换掉 World 的实现就能在"单测假世界 / Unity 真世界"之间切换。
    /// </summary>
    public sealed class ExecContext
    {
        public ICombatWorld World { get; }
        public BuiltinTable Functions { get; }
        public CpuBudget Budget { get; }
        public Blackboard Vars { get; }
        public System.Random Rng { get; }
        public InterpreterLimits Limits { get; }
        public IPropertySource Properties { get; }

        /// <summary>当前 tick 序号，由游戏循环驱动（解释器不自己加）。</summary>
        public int Tick { get; set; }

        /// <summary>本 tick 因周期不足被"优化掉"的语句数（给 UI 反馈"这行没跑"用）。</summary>
        public int SkippedByBudget { get; internal set; }

        /// <summary>本 tick 已执行语句数（安全阀用）。</summary>
        public int StatementsExecuted { get; internal set; }

        public ExecContext(
            ICombatWorld world,
            BuiltinTable functions = null,
            CpuBudget budget = null,
            Blackboard vars = null,
            System.Random rng = null,
            InterpreterLimits limits = null,
            IPropertySource properties = null)
        {
            World = world ?? throw new System.ArgumentNullException(nameof(world));
            Functions = functions ?? BuiltinTable.CreateDefault();
            Budget = budget ?? new CpuBudget(32);
            Vars = vars ?? new Blackboard();
            Rng = rng ?? new System.Random();
            Limits = limits ?? new InterpreterLimits();
            Properties = properties ?? NonePropertySource.Instance;
        }
    }
}
