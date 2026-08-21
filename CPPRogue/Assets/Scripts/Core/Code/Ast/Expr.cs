namespace CPPRogue.Core.Code.Ast
{
    public enum ExprKind
    {
        Literal,   // 字面量：50、0.3、true
        Var,       // 变量引用：n、x、hp
        Binary,    // 二元运算：i < x、a * 3
        Unary,     // 一元运算：!flag、-n
    }

    public enum BinaryOp
    {
        Add, Sub, Mul, Div, Mod,
        Lt, Gt, Le, Ge, Eq, Ne,
        And, Or,
    }

    public enum UnaryOp
    {
        Not,
        Neg,
    }

    /// <summary>
    /// 表达式节点（纯数据，由 Interpreter 求值）。
    /// 所有"参数位"都用 Expr：attack(n) 的 n、for(i=0; i&lt;x; i++) 的 x，
    /// 既可以写字面量，也可以写变量和运算——这就是参数化的扩展点。
    /// </summary>
    public sealed class Expr
    {
        public ExprKind Kind;
        public Value Literal;    // Literal：字面量的值
        public string VarName;   // Var：变量名（先查玩家变量，再查系统属性 hp/tick…）
        public BinaryOp BinOp;   // Binary：运算符
        public UnaryOp UnOp;     // Unary：运算符
        public Expr Left;        // Binary 左操作数 / Unary 操作数
        public Expr Right;       // Binary 右操作数

        public static Expr Num(double n) => new Expr { Kind = ExprKind.Literal, Literal = Value.Of(n) };
        public static Expr Bool(bool b) => new Expr { Kind = ExprKind.Literal, Literal = Value.Of(b) };
        public static Expr Var(string name) => new Expr { Kind = ExprKind.Var, VarName = name };
        public static Expr Bin(BinaryOp op, Expr left, Expr right) =>
            new Expr { Kind = ExprKind.Binary, BinOp = op, Left = left, Right = right };
        public static Expr Un(UnaryOp op, Expr operand) =>
            new Expr { Kind = ExprKind.Unary, UnOp = op, Left = operand };
    }
}
