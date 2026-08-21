namespace CPPRogue.Core.Code.Ast
{
    public enum BlockKind
    {
        Call,      // attack(); heal(n);
        Assign,    // a = 值;
        If,        // if (条件) { } else { }
        For,       // for (i = 初值; 条件; i += 步长) { }
        While,     // while (条件) { }（条件为 null = while(true)）
        Return,    // return;
        Break,     // break;
        Continue,  // continue;
    }

    /// <summary>
    /// 语句块（纯数据，不自己执行，由 Interpreter 集中解释）。
    /// Call 存函数名字符串而不是对象引用：AST 可以直接 JSON 序列化（存档/仓库/Boss 屏显源码），
    /// 函数在"链接期"由 BuiltinTable 解析。
    /// </summary>
    public sealed class Block
    {
        public BlockKind Kind;

        // Call：函数名 + 参数（参数全是 Expr，支持变量/运算）
        public string CallName;
        public Expr[] Args = System.Array.Empty<Expr>();

        // Assign：Target = ValueExpr
        public string Target;
        public Expr ValueExpr;

        // If / While：条件
        public Expr Condition;
        public Block[] Body = System.Array.Empty<Block>();
        public Block[] ElseBody = System.Array.Empty<Block>();

        // For：for (LoopVar = Init; Condition; LoopVar += Step) { Body }
        public string LoopVar;
        public Expr Init;
        public Expr Step;

        // ---- 构造工厂（让拼程序的代码可读） ----

        public static Block Call(string name, params Expr[] args) =>
            new Block { Kind = BlockKind.Call, CallName = name, Args = args ?? System.Array.Empty<Expr>() };

        public static Block Assign(string target, Expr value) =>
            new Block { Kind = BlockKind.Assign, Target = target, ValueExpr = value };

        public static Block If(Expr condition, Block[] body, Block[] elseBody = null) =>
            new Block { Kind = BlockKind.If, Condition = condition, Body = body ?? System.Array.Empty<Block>(), ElseBody = elseBody ?? System.Array.Empty<Block>() };

        public static Block For(string loopVar, Expr init, Expr cond, Expr step, params Block[] body) =>
            new Block { Kind = BlockKind.For, LoopVar = loopVar, Init = init, Condition = cond, Step = step, Body = body ?? System.Array.Empty<Block>() };

        public static Block While(Expr condition, params Block[] body) =>
            new Block { Kind = BlockKind.While, Condition = condition, Body = body ?? System.Array.Empty<Block>() };

        public static Block Return() => new Block { Kind = BlockKind.Return };
        public static Block Break() => new Block { Kind = BlockKind.Break };
        public static Block Continue() => new Block { Kind = BlockKind.Continue };
    }
}
