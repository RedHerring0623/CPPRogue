using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Combat
{
    /// <summary>
    /// 变量黑板：玩家函数里的变量在 tick 之间持久（§3.1），死亡才由上层调用 Clear。
    /// 槽位上限是 §4 的平衡阀门之一，0 表示不限。
    /// </summary>
    public sealed class Blackboard
    {
        private readonly System.Collections.Generic.Dictionary<string, Value> _vars =
            new System.Collections.Generic.Dictionary<string, Value>();

        private readonly int _maxSlots;

        public Blackboard(int maxSlots = 0)
        {
            _maxSlots = maxSlots;
        }

        public int Count => _vars.Count;

        public int MaxSlots => _maxSlots;

        public bool Contains(string name) => _vars.ContainsKey(name);

        public bool TryGet(string name, out Value value)
        {
            return _vars.TryGetValue(name, out value);
        }

        /// <summary>写入变量。新建变量超过槽位上限时返回 false（语句被拒绝执行）。</summary>
        public bool TrySet(string name, Value value)
        {
            if (!_vars.ContainsKey(name) && _maxSlots > 0 && _vars.Count >= _maxSlots)
                return false;
            _vars[name] = value;
            return true;
        }

        public void Clear()
        {
            _vars.Clear();
        }

        /// <summary>变量快照（UI/调试展示用，拷贝一份避免边遍历边改）。</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, Value> Snapshot()
        {
            return new System.Collections.Generic.Dictionary<string, Value>(_vars);
        }
    }
}
