using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Codebase
{
    /// <summary>
    /// 玩家局外档案：仓库 + 成长 + 战备 BD。跨局保留，局内死亡不清——
    /// 由 Game 层持有并负责落盘（SaveGame ↔ SaveFile）。
    /// </summary>
    public sealed class PlayerProfile
    {
        public CodebaseState Codebase = new CodebaseState();
        public MetaProgress Progress = new MetaProgress();

        /// <summary>战备 BD：进图时的初始 Routine 语句（局外编辑，进图后可热改）。</summary>
        public List<Block> Loadout = new List<Block>();

        /// <summary>
        /// 新档（数值定稿 2026-08-31）：一个 attack(1)、2 行、1s 时间窗、材料归零。
        /// </summary>
        public static PlayerProfile NewGame()
        {
            var profile = new PlayerProfile();
            profile.Codebase.Add(FragmentCatalog.TierId("attack", 1), 1);
            profile.Loadout.Add(Block.Call("attack", Expr.Num(1)));
            return profile;
        }
    }
}
