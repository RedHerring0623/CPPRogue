using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Builtins;

namespace CPPRogue.Core.Code.Runtime
{
    /// <summary>
    /// 唯一的执行者。安全阀（CPU 扣费、循环上限、语句数上限）全部集中在这一个文件里，
    /// 不会散落到各语句类；语句语义在 Block 数据 + builtin 里，这里只做控制流。
    /// </summary>
    public sealed class Interpreter
    {
        private enum Flow { None, Return, Break, Continue }

        /// <summary>
        /// 跑一个 tick。开头重置周期预算（§4：预算按 tick 结算）和统计计数。
        /// 返回 Completed / Hung；被优化掉的语句数见 ctx.SkippedByBudget。
        /// </summary>
        public ExecResult RunTick(Block[] program, ExecContext ctx)
        {
            ctx.Budget.Reset();
            ctx.SkippedByBudget = 0;
            ctx.StatementsExecuted = 0;
            try
            {
                ExecuteBody(program ?? System.Array.Empty<Block>(), ctx);
                return ExecResult.Completed;
            }
            catch (InterpreterHungException)
            {
                return ExecResult.Hung;
            }
        }

        /// <summary>表达式求值（公开给词缀等未来扩展用）。</summary>
        public Value Eval(Expr e, ExecContext ctx)
        {
            switch (e.Kind)
            {
                case ExprKind.Literal:
                    return e.Literal;

                case ExprKind.Var:
                    if (ctx.Vars.TryGet(e.VarName, out Value v))
                        return v;
                    if (ctx.Properties.TryRead(e.VarName, out Value p))
                        return p;
                    return Value.Zero; // 未定义变量按 0（"未初始化"的老传统）

                case ExprKind.Binary:
                    return EvalBinary(e, ctx);

                case ExprKind.Unary:
                    return EvalUnary(e, ctx);

                default:
                    throw new System.InvalidOperationException($"未知的表达式类型：{e.Kind}");
            }
        }

        private Flow ExecuteBody(Block[] body, ExecContext ctx)
        {
            for (int i = 0; i < body.Length; i++)
            {
                Flow flow = ExecuteStatement(body[i], ctx);
                if (flow != Flow.None)
                    return flow; // return/break/continue 向上传播，由对应层级消费
            }
            return Flow.None;
        }

        private Flow ExecuteStatement(Block s, ExecContext ctx)
        {
            ctx.StatementsExecuted++;
            if (ctx.StatementsExecuted > ctx.Limits.MaxStatementsPerTick)
                throw new InterpreterHungException("本 tick 语句数超过上限");

            switch (s.Kind)
            {
                case BlockKind.Call:
                    return DoCall(s, ctx);

                case BlockKind.Assign:
                    if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; }
                    ctx.Vars.TrySet(s.Target, Eval(s.ValueExpr, ctx));
                    return Flow.None;

                case BlockKind.If:
                    if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; }
                    return ExecuteBody(Eval(s.Condition, ctx).AsBool() ? s.Body : s.ElseBody, ctx);

                case BlockKind.For:
                    return DoFor(s, ctx);

                case BlockKind.While:
                    return DoWhile(s, ctx);

                case BlockKind.Return:
                    if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; }
                    return Flow.Return;

                case BlockKind.Break:
                    if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; }
                    return Flow.Break;

                case BlockKind.Continue:
                    if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; }
                    return Flow.Continue;

                default:
                    throw new System.InvalidOperationException($"未知的语句类型：{s.Kind}");
            }
        }

        private Flow DoCall(Block s, ExecContext ctx)
        {
            if (!ctx.Functions.TryGet(s.CallName, out IBuiltin fn))
                throw new UndefinedReferenceException(s.CallName);
            if (!ctx.Budget.TrySpend(fn.CpuCost)) { ctx.SkippedByBudget++; return Flow.None; }

            // 参数在调用前求值——attack(n) 的 n 在此刻从黑板/属性解析成 Value
            var args = new Value[s.Args.Length];
            for (int i = 0; i < args.Length; i++)
                args[i] = Eval(s.Args[i], ctx);

            fn.Invoke(ctx, args);
            return Flow.None;
        }

        private Flow DoFor(Block s, ExecContext ctx)
        {
            // for (LoopVar = Init; Condition; LoopVar += Step) { Body }
            if (s.LoopVar != null && s.Init != null)
                ctx.Vars.TrySet(s.LoopVar, Eval(s.Init, ctx));

            int iterations = 0;
            while (s.Condition == null || Eval(s.Condition, ctx).AsBool())
            {
                if (++iterations > ctx.Limits.MaxLoopIterations)
                    throw new InterpreterHungException("for 循环迭代超过上限");
                if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; } // 每次迭代的循环开销

                Flow flow = ExecuteBody(s.Body, ctx);
                if (flow == Flow.Break)
                    break;
                if (flow != Flow.None)
                    return flow; // return/continue 交给外层处理

                if (s.LoopVar != null && s.Step != null && ctx.Vars.TryGet(s.LoopVar, out Value current))
                    ctx.Vars.TrySet(s.LoopVar, Value.Of(current.AsNumber() + Eval(s.Step, ctx).AsNumber()));
            }
            return Flow.None;
        }

        private Flow DoWhile(Block s, ExecContext ctx)
        {
            int iterations = 0;
            while (s.Condition == null || Eval(s.Condition, ctx).AsBool())
            {
                if (++iterations > ctx.Limits.MaxLoopIterations)
                    throw new InterpreterHungException("while 死循环，迭代超过上限");
                if (!ctx.Budget.TrySpend(1)) { ctx.SkippedByBudget++; return Flow.None; }

                Flow flow = ExecuteBody(s.Body, ctx);
                if (flow == Flow.Break)
                    break;
                if (flow != Flow.None)
                    return flow;
            }
            return Flow.None;
        }

        private Value EvalBinary(Expr e, ExecContext ctx)
        {
            // && 和 || 短路求值
            if (e.BinOp == BinaryOp.And)
                return Eval(e.Left, ctx).AsBool() ? Value.Of(Eval(e.Right, ctx).AsBool()) : Value.Of(false);
            if (e.BinOp == BinaryOp.Or)
                return Eval(e.Left, ctx).AsBool() ? Value.Of(true) : Value.Of(Eval(e.Right, ctx).AsBool());

            double l = Eval(e.Left, ctx).AsNumber();
            double r = Eval(e.Right, ctx).AsNumber();
            switch (e.BinOp)
            {
                case BinaryOp.Add: return Value.Of(l + r);
                case BinaryOp.Sub: return Value.Of(l - r);
                case BinaryOp.Mul: return Value.Of(l * r);
                case BinaryOp.Div: return r == 0d ? Value.Zero : Value.Of(l / r); // 除零：UB 梗，按 0 处理
                case BinaryOp.Mod: return r == 0d ? Value.Zero : Value.Of(l % r);
                case BinaryOp.Lt: return Value.Of(l < r);
                case BinaryOp.Gt: return Value.Of(l > r);
                case BinaryOp.Le: return Value.Of(l <= r);
                case BinaryOp.Ge: return Value.Of(l >= r);
                case BinaryOp.Eq: return Value.Of(l == r);
                case BinaryOp.Ne: return Value.Of(l != r);
                default: throw new System.InvalidOperationException($"未知的二元运算：{e.BinOp}");
            }
        }

        private Value EvalUnary(Expr e, ExecContext ctx)
        {
            switch (e.UnOp)
            {
                case UnaryOp.Neg: return Value.Of(-Eval(e.Left, ctx).AsNumber());
                case UnaryOp.Not: return Value.Of(!Eval(e.Left, ctx).AsBool());
                default: throw new System.InvalidOperationException($"未知的一元运算：{e.UnOp}");
            }
        }
    }
}
