using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Code
{
    /// <summary>
    /// Routine：拼装好的函数——每 tick 被解释器执行的东西（命名约定见 Core/README.md）。
    /// 玩家的 Build 是 Routine，怪物和 Boss 的程序也是 Routine，同一套东西。
    /// 行数上限是 §4 的平衡阀门之一：初始 8 行，装备/成长扩展。
    /// </summary>
    public sealed class Routine
    {
        /// <summary>§4：函数行数上限的初始值。</summary>
        public const int DefaultMaxLines = 8;

        public Block[] Lines { get; }

        public int MaxLines { get; }

        public int LineCount => Lines.Length;

        public Routine(Block[] lines, int maxLines = DefaultMaxLines)
        {
            Lines = lines ?? System.Array.Empty<Block>();
            if (maxLines < 0)
                throw new System.ArgumentOutOfRangeException(nameof(maxLines), "行数上限不能为负数。");
            int total = BlockTree.CountStatements(Lines);
            if (total > maxLines)
                throw new System.ArgumentOutOfRangeException(nameof(lines), $"函数超过行数上限：{total}/{maxLines} 行（嵌套语句计入行数）。");
            MaxLines = maxLines;
        }

        public static Routine Of(params Block[] lines)
        {
            return new Routine(lines);
        }
    }
}
