using System;
using System.Collections.Generic;
using CPPRogue.Core.Loot;

namespace CPPRogue.Core.Codebase
{
    /// <summary>
    /// 局外代码库：仓库语块计数 + 材料余额。会话级内存数据（存档系统未实现），
    /// 由 Game 层持有，不随局内死亡清空——"局外"的字面含义（LootDesign.md §3）。
    /// </summary>
    public sealed class CodebaseState
    {
        private readonly Dictionary<string, int> _blocks = new Dictionary<string, int>();
        private readonly Dictionary<MaterialKind, int> _materials = new Dictionary<MaterialKind, int>();

        public IReadOnlyDictionary<string, int> Blocks => _blocks;

        // —— 仓库 ——

        public int Count(string fragmentId)
        {
            return _blocks.TryGetValue(fragmentId, out int n) ? n : 0;
        }

        public void Add(string fragmentId, int amount = 1)
        {
            if (FragmentCatalog.Parse(fragmentId) == null)
                throw new ArgumentException($"未知语块 ID：{fragmentId}", nameof(fragmentId));
            _blocks[fragmentId] = Count(fragmentId) + amount;
        }

        // —— 材料 ——

        public int CountMaterial(MaterialKind kind)
        {
            return _materials.TryGetValue(kind, out int n) ? n : 0;
        }

        public void AddMaterial(MaterialKind kind, int amount = 1)
        {
            _materials[kind] = CountMaterial(kind) + amount;
        }

        /// <summary>花费材料（兑换成长等）。不足返回 false，不部分扣除。</summary>
        public bool Spend(MaterialKind kind, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (CountMaterial(kind) < amount)
                return false;
            _materials[kind] = CountMaterial(kind) - amount;
            return true;
        }

        // —— 合成（LootDesign.md §3：两块同级 → 一块升一档）——

        /// <summary>合成目标 ID：2×tier →（tier == 顶档 ? 自由形参 : tier+1）。</summary>
        public static string MergeTargetId(string family, int tier)
        {
            return tier >= FragmentCatalog.MaxTier
                ? FragmentCatalog.FreeId(family)
                : FragmentCatalog.TierId(family, tier + 1);
        }

        public bool CanMerge(string family, int tier)
        {
            if (!FragmentCatalog.IsFamily(family) || tier < 1 || tier > FragmentCatalog.MaxTier)
                return false;
            return Count(FragmentCatalog.TierId(family, tier)) >= 2;
        }

        /// <summary>执行一次合成。档位非法或数量不足返回 false，状态不变。</summary>
        public bool Merge(string family, int tier)
        {
            if (!CanMerge(family, tier))
                return false;
            string from = FragmentCatalog.TierId(family, tier);
            _blocks[from] = Count(from) - 2;
            string to = MergeTargetId(family, tier);
            _blocks[to] = Count(to) + 1;
            return true;
        }
    }
}
