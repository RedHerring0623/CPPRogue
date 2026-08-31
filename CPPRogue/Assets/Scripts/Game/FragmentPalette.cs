using System;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Codebase;

namespace CPPRogue.Game
{
    /// <summary>
    /// 正常 BD 的语块面板与持久化（测试 BD 不走这里——那边用 GamePalette 全语句、不存档）：
    /// 面板 = 仓库持有的语块；插入校验 = 每种语块用量 ≤ 仓库持有；
    /// Persist = 编辑器内容经 LoadoutValidator 校验后克隆进档案并落盘。
    /// </summary>
    public static class FragmentPalette
    {
        /// <summary>仓库持有的语块 → 可拖条目（家族字母序，档位升序，自由形参最后）。
        /// editor 用于实时统计 BD 内已用量（剩余 = 持有 − 已用，拖进拖出即时变化）。</summary>
        public static List<PaletteEntry> Build(CodebaseState warehouse, RoutineEditor editor)
        {
            var ids = new List<string>();
            foreach (KeyValuePair<string, int> pair in warehouse.Blocks)
            {
                if (pair.Value > 0 && FragmentCatalog.Parse(pair.Key) != null)
                    ids.Add(pair.Key);
            }
            ids.Sort(CompareIds);

            var entries = new List<PaletteEntry>();
            foreach (string id in ids)
            {
                string fragmentId = id;
                entries.Add(new PaletteEntry
                {
                    Title = fragmentId,
                    Make = () => LoadoutValidator.ToBlock(fragmentId),
                    Remaining = () =>
                    {
                        Dictionary<string, int> usage = LoadoutValidator.CountUsage(editor.Root);
                        usage.TryGetValue(fragmentId, out int used);
                        return warehouse.Count(fragmentId) - used;
                    },
                });
            }
            return entries;
        }

        /// <summary>插入校验：非语块语句（if/for 等控制流）在正常 BD 里没有来源，拒；语块超持有，拒。</summary>
        public static Func<Block, bool> BuildCanInsert(CodebaseState warehouse, RoutineEditor editor)
        {
            return block =>
            {
                string id = LoadoutValidator.FragmentIdOf(block);
                if (id == null)
                    return false;
                Dictionary<string, int> usage = LoadoutValidator.CountUsage(editor.Root);
                usage.TryGetValue(id, out int used);
                return used + 1 <= warehouse.Count(id);
            };
        }

        /// <summary>正常 BD 落盘：校验通过后克隆进档案（局内编辑与档案隔离）。失败返回 false（不动档案）。</summary>
        public static bool Persist(RoutineEditor editor, PlayerProfile profile, out string error)
        {
            Block[] root = editor.Root;
            if (!LoadoutValidator.Validate(root, profile.Codebase, profile.Progress.MaxLines, out error))
                return false;
            profile.Loadout = new List<Block>(BlockCloner.Clone(root));
            SaveFile.Save(profile);
            error = null;
            return true;
        }

        private static int CompareIds(string a, string b)
        {
            FragmentDef da = FragmentCatalog.Parse(a);
            FragmentDef db = FragmentCatalog.Parse(b);
            int fam = string.CompareOrdinal(da.Family, db.Family);
            if (fam != 0)
                return fam;
            int ta = da.Tier == 0 ? FragmentCatalog.MaxTier + 1 : da.Tier;
            int tb = db.Tier == 0 ? FragmentCatalog.MaxTier + 1 : db.Tier;
            return ta - tb;
        }
    }
}
