using System.Collections.Generic;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Code
{
    /// <summary>
    /// 源码行：Statement 为 null 表示括号行（"{"、"} else {"），不参与高亮。
    /// </summary>
    public sealed class SourceLine
    {
        public Block Statement { get; }
        public string Text { get; }
        public int Indent { get; }

        public SourceLine(Block statement, string text, int indent)
        {
            Statement = statement;
            Text = text;
            Indent = indent;
        }
    }

    /// <summary>
    /// 把 Routine 反排版成源码文本。每条语句对应一行，行持有 Block 引用——
    /// UI 拿 StepInfo.Statement 做引用比对就能定位高亮行。
    /// 这也是未来 Boss"屏显源码"的基础（所读即所跑）。
    /// </summary>
    public static class SourcePrinter
    {
        public static List<SourceLine> Print(Routine routine)
        {
            var lines = new List<SourceLine>();
            if (routine != null)
                PrintBody(routine.Lines, 0, lines);
            return lines;
        }

        private static void PrintBody(Block[] body, int indent, List<SourceLine> lines)
        {
            foreach (Block s in body)
                PrintStatement(s, indent, lines);
        }

        /// <summary>单行语句的文本（含分号）：Call/Assign/Return/Break/Continue；复合语句返回 null。</summary>
        public static string StatementText(Block s)
        {
            switch (s.Kind)
            {
                case BlockKind.Call: return CallText(s) + ";";
                case BlockKind.Assign: return $"{s.Target} = {ExprText(s.ValueExpr)};";
                case BlockKind.Return: return "return;";
                case BlockKind.Break: return "break;";
                case BlockKind.Continue: return "continue;";
                default: return null;
            }
        }

        /// <summary>复合语句的头一行（含 "{"）：If/For/While；单行语句返回 null。</summary>
        public static string HeaderText(Block s)
        {
            switch (s.Kind)
            {
                case BlockKind.If: return $"if ({ExprText(s.Condition)}) {{";
                case BlockKind.For: return $"for ({s.LoopVar} = {ExprText(s.Init)}; {ExprText(s.Condition)}; {s.LoopVar} += {ExprText(s.Step)}) {{";
                case BlockKind.While: return $"while ({(s.Condition == null ? "true" : ExprText(s.Condition))}) {{";
                default: return null;
            }
        }

        private static void PrintStatement(Block s, int indent, List<SourceLine> lines)
        {
            string single = StatementText(s);
            if (single != null)
            {
                lines.Add(new SourceLine(s, single, indent));
                return;
            }

            lines.Add(new SourceLine(s, HeaderText(s), indent));
            if (s.Kind == BlockKind.If)
            {
                PrintBody(s.Body, indent + 1, lines);
                if (s.ElseBody.Length > 0)
                {
                    lines.Add(new SourceLine(null, "} else {", indent));
                    PrintBody(s.ElseBody, indent + 1, lines);
                }
            }
            else
            {
                PrintBody(s.Body, indent + 1, lines);
            }
            lines.Add(new SourceLine(null, "}", indent));
        }

        private static string CallText(Block s)
        {
            if (s.Args.Length == 0)
                return $"{s.CallName}()";
            var args = new List<string>();
            foreach (Expr a in s.Args)
                args.Add(ExprText(a));
            return $"{s.CallName}({string.Join(", ", args)})";
        }

        public static string ExprText(Expr e)
        {
            switch (e.Kind)
            {
                case ExprKind.Literal:
                    if (e.Literal.Kind == ValueKind.Bool)
                        return e.Literal.AsBool() ? "true" : "false";
                    return FormatNumber(e.Literal.AsNumber());
                case ExprKind.Var:
                    return e.VarName;
                case ExprKind.Binary:
                    return $"{ExprText(e.Left)} {OpText(e.BinOp)} {ExprText(e.Right)}";
                case ExprKind.Unary:
                    return e.UnOp == UnaryOp.Not ? $"!{ExprText(e.Left)}" : $"-{ExprText(e.Left)}";
                default:
                    return "?";
            }
        }

        private static string OpText(BinaryOp op)
        {
            switch (op)
            {
                case BinaryOp.Add: return "+";
                case BinaryOp.Sub: return "-";
                case BinaryOp.Mul: return "*";
                case BinaryOp.Div: return "/";
                case BinaryOp.Mod: return "%";
                case BinaryOp.Lt: return "<";
                case BinaryOp.Gt: return ">";
                case BinaryOp.Le: return "<=";
                case BinaryOp.Ge: return ">=";
                case BinaryOp.Eq: return "==";
                case BinaryOp.Ne: return "!=";
                case BinaryOp.And: return "&&";
                case BinaryOp.Or: return "||";
                default: return "?";
            }
        }

        private static string FormatNumber(double n)
        {
            // 整数值去掉小数点：5 而不是 5.0
            if (n == System.Math.Floor(n) && !double.IsInfinity(n))
                return ((long)n).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return n.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
