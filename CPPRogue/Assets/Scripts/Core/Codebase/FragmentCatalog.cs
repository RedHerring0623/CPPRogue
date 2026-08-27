namespace CPPRogue.Core.Codebase
{
    /// <summary>参数化语块阶梯（LootDesign.md §3 块合成）的一个条目。</summary>
    public sealed class FragmentDef
    {
        /// <summary>家族名："attack"。</summary>
        public string Family;

        /// <summary>阶梯档位 1..MaxTier；0 = 自由形参档（最高级，如 attack()）。</summary>
        public int Tier;

        /// <summary>稳定 ID："attack(1)" / "attack()"。</summary>
        public string Id;
    }

    /// <summary>
    /// 阶梯定义：哪些家族有合成阶梯、档位上限、ID 生成与解析。
    /// 命名纪律：块=Fragment（物品形态）；这里的"家族"指同一 builtin 的参数化掉落系列。
    /// </summary>
    public static class FragmentCatalog
    {
        /// <summary>档位上限：2×顶档合成自由形参版。档位数值曲线待实测（LootDesign.md §6）。</summary>
        public const int MaxTier = 5;

        /// <summary>有阶梯的家族。heal/shield 是推广规则（LootDesign.md §6 待确认）。</summary>
        public static readonly string[] Families = { "attack", "heal", "shield" };

        public static string TierId(string family, int tier) => $"{family}({tier})";

        public static string FreeId(string family) => $"{family}()";

        public static bool IsFamily(string family)
        {
            for (int i = 0; i < Families.Length; i++)
            {
                if (Families[i] == family)
                    return true;
            }
            return false;
        }

        /// <summary>解析 "attack(2)" / "attack()" → def；阶梯之外的 ID 返回 null。</summary>
        public static FragmentDef Parse(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            int open = id.IndexOf('(');
            if (open <= 0 || !id.EndsWith(")"))
                return null;
            string family = id.Substring(0, open);
            if (!IsFamily(family))
                return null;
            string param = id.Substring(open + 1, id.Length - open - 2);
            if (param.Length == 0)
                return new FragmentDef { Family = family, Tier = 0, Id = FreeId(family) };
            int tier;
            if (!int.TryParse(param, out tier) || tier < 1 || tier > MaxTier)
                return null;
            return new FragmentDef { Family = family, Tier = tier, Id = TierId(family, tier) };
        }
    }
}
