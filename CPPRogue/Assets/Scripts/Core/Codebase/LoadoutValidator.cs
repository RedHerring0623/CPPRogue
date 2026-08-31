using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Codebase
{
    /// <summary>
    /// 战备 BD 校验：①语句总数不超成长行数上限；②每种语块的用量不超仓库持有数
    /// （attack(1) = Call("attack", 字面量1)；attack() / attack(变量) = 自由形参档）。
    /// 局内 ESC 热编辑不做此校验（局内所得随死亡消失，只有落盘的 BD 需要守规矩）。
    /// </summary>
    public static class LoadoutValidator
    {
        public static bool Validate(Block[] loadout, CodebaseState warehouse, int maxLines, out string error)
        {
            int lines = BlockTree.CountStatements(loadout);
            if (lines > maxLines)
            {
                error = $"超过行数上限（{lines}/{maxLines}）";
                return false;
            }

            var usage = new Dictionary<string, int>();
            CountUsage(loadout, usage);
            foreach (KeyValuePair<string, int> pair in usage)
            {
                int owned = warehouse.Count(pair.Key);
                if (owned < pair.Value)
                {
                    error = $"{pair.Key} 用了 {pair.Value} 个，仓库只有 {owned} 个";
                    return false;
                }
            }
            error = null;
            return true;
        }

        /// <summary>把一个语块 ID 变成可拼装的 Block：attack(1) → Call("attack", 1)；attack() → Call("attack")。</summary>
        public static Block ToBlock(string fragmentId)
        {
            FragmentDef def = FragmentCatalog.Parse(fragmentId);
            if (def == null)
                return null;
            return def.Tier == 0
                ? Block.Call(def.Family)
                : Block.Call(def.Family, Expr.Num(def.Tier));
        }

        /// <summary>Call 语句对应的语块 ID；非阶梯家族或参数不是合法档位返回 null。</summary>
        public static string FragmentIdOf(Block b)
        {
            if (b == null || b.Kind != BlockKind.Call || !FragmentCatalog.IsFamily(b.CallName))
                return null;
            if (b.Args.Length == 0)
                return FragmentCatalog.FreeId(b.CallName);
            if (b.Args.Length == 1 && b.Args[0].Kind == ExprKind.Literal && b.Args[0].Literal.Kind == ValueKind.Number)
                return FragmentCatalog.TierId(b.CallName, (int)b.Args[0].Literal.AsNumber());
            // 参数是变量/表达式 → 自由形参档在使用中（attack(a) 就是 attack() 的绑定形态）
            return FragmentCatalog.FreeId(b.CallName);
        }

        /// <summary>统计一组语句里每种语块的用量（编辑器插入校验 / 存档校验共用）。</summary>
        public static Dictionary<string, int> CountUsage(Block[] blocks)
        {
            var usage = new Dictionary<string, int>();
            CountUsage(blocks, usage);
            return usage;
        }

        private static void CountUsage(Block[] blocks, Dictionary<string, int> usage)
        {
            if (blocks == null)
                return;
            foreach (Block b in blocks)
            {
                string id = FragmentIdOf(b);
                if (id != null)
                {
                    usage.TryGetValue(id, out int n);
                    usage[id] = n + 1;
                }
                CountUsage(b.Body, usage);
                CountUsage(b.ElseBody, usage);
            }
        }
    }
}
