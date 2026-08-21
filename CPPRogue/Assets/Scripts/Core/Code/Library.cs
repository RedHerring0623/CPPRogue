using CPPRogue.Core.Code.Builtins;

namespace CPPRogue.Core.Code
{
    /// <summary>
    /// 词汇表：函数名 → 内置函数。拼装 UI、掉落表、解释器都查这一张表，
    /// 游戏有多少"动词"在这里一目了然。新技能 = Builtins/ 加一个类 + Register 一行。
    /// </summary>
    public sealed class BuiltinTable
    {
        private readonly System.Collections.Generic.Dictionary<string, IBuiltin> _functions =
            new System.Collections.Generic.Dictionary<string, IBuiltin>();

        public int Count => _functions.Count;

        /// <summary>默认词汇表：开局就有的基础语句。</summary>
        public static BuiltinTable CreateDefault()
        {
            var table = new BuiltinTable();
            table.Register(new AttackBuiltin());
            table.Register(new HealBuiltin());
            table.Register(new ShieldBuiltin());
            return table;
        }

        /// <summary>注册。重名直接抛异常——词汇表里一个名字只能有一个意思（好管理优先）。</summary>
        public void Register(IBuiltin function)
        {
            if (_functions.ContainsKey(function.Name))
                throw new System.ArgumentException($"词汇表里已有同名函数：{function.Name}", nameof(function));
            _functions[function.Name] = function;
        }

        public bool TryGet(string name, out IBuiltin function)
        {
            return _functions.TryGetValue(name, out function);
        }
    }
}
