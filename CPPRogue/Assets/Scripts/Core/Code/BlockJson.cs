using System;
using System.Collections.Generic;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Code
{
    /// <summary>
    /// Block / Expr ↔ MiniJson 数据结构（存档、战备 BD、未来的仓库与 Boss 屏显共用，
    /// 即 CODE_EXECUTION.md §7 规划的"Routine 的 JSON 序列化"）。
    /// 事实来源是 AST 的字段集：加新 BlockKind / ExprKind 时这里同步，测试锁往返。
    /// </summary>
    public static class BlockJson
    {
        // ---------- Block[] ----------

        public static object ToJson(Block[] blocks)
        {
            var list = new List<object>();
            if (blocks != null)
            {
                foreach (Block b in blocks)
                    list.Add(BlockToJson(b));
            }
            return list;
        }

        public static Block[] FromJson(object json)
        {
            if (!(json is List<object> list))
                throw new FormatException("BlockJson: 语句列表必须是数组");
            var blocks = new Block[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var map = list[i] as Dictionary<string, object>
                    ?? throw new FormatException("BlockJson: 语句元素必须是对象");
                blocks[i] = BlockFromJson(map);
            }
            return blocks;
        }

        private static object BlockToJson(Block b)
        {
            var map = new Dictionary<string, object> { ["kind"] = KindName(b.Kind) };
            switch (b.Kind)
            {
                case BlockKind.Call:
                    map["name"] = b.CallName;
                    map["args"] = ExprsToJson(b.Args);
                    break;
                case BlockKind.Assign:
                    map["target"] = b.Target;
                    map["value"] = ExprToJson(b.ValueExpr);
                    break;
                case BlockKind.If:
                    map["cond"] = ExprToJson(b.Condition);
                    map["body"] = ToJson(b.Body);
                    map["else"] = ToJson(b.ElseBody);
                    break;
                case BlockKind.For:
                    map["var"] = b.LoopVar;
                    map["init"] = ExprToJson(b.Init);
                    map["cond"] = ExprToJson(b.Condition);
                    map["step"] = ExprToJson(b.Step);
                    map["body"] = ToJson(b.Body);
                    break;
                case BlockKind.While:
                    map["cond"] = b.Condition != null ? ExprToJson(b.Condition) : null;
                    map["body"] = ToJson(b.Body);
                    break;
                default:
                    break;   // Return / Break / Continue 只有 kind
            }
            return map;
        }

        private static Block BlockFromJson(Dictionary<string, object> map)
        {
            string kindName = GetString(map, "kind");
            Block b = new Block();
            switch (kindName)
            {
                case "call":
                    b.Kind = BlockKind.Call;
                    b.CallName = GetString(map, "name");
                    b.Args = ExprsFromJson(map, "args");
                    break;
                case "assign":
                    b.Kind = BlockKind.Assign;
                    b.Target = GetString(map, "target");
                    b.ValueExpr = ExprFromJson(map, "value");
                    break;
                case "if":
                    b.Kind = BlockKind.If;
                    b.Condition = ExprFromJson(map, "cond");
                    b.Body = FromJson(map["body"]);
                    b.ElseBody = map.TryGetValue("else", out object elseJson) && elseJson != null
                        ? FromJson(elseJson) : System.Array.Empty<Block>();
                    break;
                case "for":
                    b.Kind = BlockKind.For;
                    b.LoopVar = GetString(map, "var");
                    b.Init = ExprFromJson(map, "init");
                    b.Condition = ExprFromJson(map, "cond");
                    b.Step = ExprFromJson(map, "step");
                    b.Body = FromJson(map["body"]);
                    break;
                case "while":
                    b.Kind = BlockKind.While;
                    b.Condition = map.TryGetValue("cond", out object cond) && cond != null
                        ? ExprFromJson(cond) : null;   // null 条件 = while(true)
                    b.Body = FromJson(map["body"]);
                    break;
                case "return": b.Kind = BlockKind.Return; break;
                case "break": b.Kind = BlockKind.Break; break;
                case "continue": b.Kind = BlockKind.Continue; break;
                default:
                    throw new FormatException($"BlockJson: 未知语句类型 '{kindName}'");
            }
            return b;
        }

        private static string KindName(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Call: return "call";
                case BlockKind.Assign: return "assign";
                case BlockKind.If: return "if";
                case BlockKind.For: return "for";
                case BlockKind.While: return "while";
                case BlockKind.Return: return "return";
                case BlockKind.Break: return "break";
                case BlockKind.Continue: return "continue";
                default: throw new FormatException($"BlockJson: 未支持的语句类型 {kind}");
            }
        }

        // ---------- Expr ----------

        private static object ExprsToJson(Expr[] exprs)
        {
            var list = new List<object>();
            if (exprs != null)
            {
                foreach (Expr e in exprs)
                    list.Add(ExprToJson(e));
            }
            return list;
        }

        private static object ExprToJson(Expr e)
        {
            if (e == null)
                return null;
            var map = new Dictionary<string, object>();
            switch (e.Kind)
            {
                case ExprKind.Literal:
                    map["k"] = "lit";
                    if (e.Literal.Kind == ValueKind.Bool)
                    {
                        map["b"] = e.Literal.AsBool();
                    }
                    else
                    {
                        map["n"] = e.Literal.AsNumber();
                    }
                    break;
                case ExprKind.Var:
                    map["k"] = "var";
                    map["name"] = e.VarName;
                    break;
                case ExprKind.Binary:
                    map["k"] = "bin";
                    map["op"] = BinOpName(e.BinOp);
                    map["l"] = ExprToJson(e.Left);
                    map["r"] = ExprToJson(e.Right);
                    break;
                case ExprKind.Unary:
                    map["k"] = "un";
                    map["op"] = UnOpName(e.UnOp);
                    map["o"] = ExprToJson(e.Left);   // Unary 的操作数也存 Left（与 AST 一致）
                    break;
                default:
                    throw new FormatException($"BlockJson: 未支持的表达式类型 {e.Kind}");
            }
            return map;
        }

        private static Expr ExprFromJson(object json)
        {
            if (json == null)
                return null;
            var map = json as Dictionary<string, object>
                ?? throw new FormatException("BlockJson: 表达式必须是对象");
            string k = GetString(map, "k");
            switch (k)
            {
                case "lit":
                    if (map.TryGetValue("b", out object b))
                        return Expr.Bool((bool)b);
                    return Expr.Num(GetDouble(map, "n"));
                case "var":
                    return Expr.Var(GetString(map, "name"));
                case "bin":
                    return Expr.Bin(ParseBinOp(GetString(map, "op")),
                        ExprFromJson(map["l"]), ExprFromJson(map["r"]));
                case "un":
                    return Expr.Un(ParseUnOp(GetString(map, "op")), ExprFromJson(map["o"]));
                default:
                    throw new FormatException($"BlockJson: 未知表达式类型 '{k}'");
            }
        }

        /// <summary>从对象的指定字段读表达式；字段缺失视为 null（while(true) 的 cond 等）。</summary>
        private static Expr ExprFromJson(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out object value) ? ExprFromJson(value) : null;
        }

        private static Expr[] ExprsFromJson(Dictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out object json) || json == null)
                return System.Array.Empty<Expr>();
            var list = (List<object>)json;
            var exprs = new Expr[list.Count];
            for (int i = 0; i < list.Count; i++)
                exprs[i] = ExprFromJson(list[i]);
            return exprs;
        }

        private static string BinOpName(BinaryOp op)
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
                default: throw new FormatException($"BlockJson: 未支持的运算符 {op}");
            }
        }

        private static BinaryOp ParseBinOp(string op)
        {
            switch (op)
            {
                case "+": return BinaryOp.Add;
                case "-": return BinaryOp.Sub;
                case "*": return BinaryOp.Mul;
                case "/": return BinaryOp.Div;
                case "%": return BinaryOp.Mod;
                case "<": return BinaryOp.Lt;
                case ">": return BinaryOp.Gt;
                case "<=": return BinaryOp.Le;
                case ">=": return BinaryOp.Ge;
                case "==": return BinaryOp.Eq;
                case "!=": return BinaryOp.Ne;
                case "&&": return BinaryOp.And;
                case "||": return BinaryOp.Or;
                default: throw new FormatException($"BlockJson: 未知运算符 '{op}'");
            }
        }

        private static string UnOpName(UnaryOp op)
        {
            switch (op)
            {
                case UnaryOp.Not: return "!";
                case UnaryOp.Neg: return "-";
                default: throw new FormatException($"BlockJson: 未支持的一元运算符 {op}");
            }
        }

        private static UnaryOp ParseUnOp(string op)
        {
            switch (op)
            {
                case "!": return UnaryOp.Not;
                case "-": return UnaryOp.Neg;
                default: throw new FormatException($"BlockJson: 未知一元运算符 '{op}'");
            }
        }

        // ---------- 小工具 ----------

        private static string GetString(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out object value) && value is string s)
                return s;
            throw new FormatException($"BlockJson: 缺少字符串字段 '{key}'");
        }

        private static double GetDouble(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out object value) && value is double d)
                return d;
            throw new FormatException($"BlockJson: 缺少数字字段 '{key}'");
        }
    }
}
