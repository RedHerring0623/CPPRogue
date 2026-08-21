using System;

namespace CPPRogue.Core.Code.Ast
{
    /// <summary>语句树工具集：计数、取体、子树包含判断。</summary>
    public static class BlockTree
    {
        /// <summary>统计语句总数（含嵌套体；括号行不算）——Routine 行数上限按这个算。</summary>
        public static int CountStatements(Block[] body)
        {
            if (body == null)
                return 0;
            int count = 0;
            foreach (Block s in body)
            {
                count += 1;
                count += CountStatements(s.Body);
                if (s.Kind == BlockKind.If)
                    count += CountStatements(s.ElseBody);
            }
            return count;
        }

        /// <summary>取语句体。branch：0 = then/循环体，1 = if 的 else。</summary>
        public static Block[] GetBody(Block owner, int branch)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            switch (owner.Kind)
            {
                case BlockKind.If:
                    return branch == 1 ? owner.ElseBody : owner.Body;
                case BlockKind.For:
                case BlockKind.While:
                    if (branch == 0)
                        return owner.Body;
                    throw new ArgumentOutOfRangeException(nameof(branch), "循环只有一条语句体。");
                default:
                    throw new ArgumentException($"语句 {owner.Kind} 没有语句体。", nameof(owner));
            }
        }

        /// <summary>candidate 是否在 root 的子树内（含 root 自身）——移动防呆用。</summary>
        public static bool Contains(Block root, Block candidate)
        {
            if (root == null || candidate == null)
                return false;
            if (ReferenceEquals(root, candidate))
                return true;
            if (Contains(root.Body, candidate))
                return true;
            return root.Kind == BlockKind.If && Contains(root.ElseBody, candidate);
        }

        private static bool Contains(Block[] body, Block candidate)
        {
            if (body == null)
                return false;
            foreach (Block s in body)
            {
                if (Contains(s, candidate))
                    return true;
            }
            return false;
        }
    }
}
