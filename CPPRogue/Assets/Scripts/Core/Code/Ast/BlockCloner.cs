namespace CPPRogue.Core.Code.Ast
{
    /// <summary>语句树深拷贝：预设加载/未来的复制粘贴用，防止编辑共享实例互相污染。</summary>
    public static class BlockCloner
    {
        public static Block[] Clone(Block[] body)
        {
            if (body == null)
                return System.Array.Empty<Block>();
            var arr = new Block[body.Length];
            for (int i = 0; i < body.Length; i++)
                arr[i] = Clone(body[i]);
            return arr;
        }

        public static Block Clone(Block s)
        {
            if (s == null)
                return null;
            return new Block
            {
                Kind = s.Kind,
                CallName = s.CallName,
                Args = Clone(s.Args),
                Target = s.Target,
                ValueExpr = Clone(s.ValueExpr),
                Condition = Clone(s.Condition),
                Body = Clone(s.Body),
                ElseBody = Clone(s.ElseBody),
                LoopVar = s.LoopVar,
                Init = Clone(s.Init),
                Step = Clone(s.Step),
            };
        }

        public static Expr[] Clone(Expr[] exprs)
        {
            if (exprs == null)
                return System.Array.Empty<Expr>();
            var arr = new Expr[exprs.Length];
            for (int i = 0; i < exprs.Length; i++)
                arr[i] = Clone(exprs[i]);
            return arr;
        }

        public static Expr Clone(Expr e)
        {
            if (e == null)
                return null;
            return new Expr
            {
                Kind = e.Kind,
                Literal = e.Literal,
                VarName = e.VarName,
                BinOp = e.BinOp,
                UnOp = e.UnOp,
                Left = Clone(e.Left),
                Right = Clone(e.Right),
            };
        }
    }
}
