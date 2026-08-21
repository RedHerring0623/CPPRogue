using System.Collections.Generic;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Builtins;

namespace CPPRogue.Core.Code.Runtime
{
    /// <summary>
    /// 唯一的执行者。安全阀（CPU 扣费、循环上限、语句数上限）全部集中在这一个文件里。
    /// 两种用法：
    /// ① RunTick —— 一口气跑完（单测/模拟器用，瞬间返回）；
    /// ② Execute —— 步骤机（UI 驱动用）：一次拉一步 = 执行一条语句，驱动层控制节奏，
    ///    语句在"被拉取的那一刻"才执行——代码跑的期间世界照常演化（走位、怪物移动）。
    /// 逻辑层没有时间概念，StatementInterval 之类的间隔属于驱动层。
    /// </summary>
    public sealed class Interpreter
    {
        private enum Flow { None, Return, Break, Continue }

        /// <summary>控制流信号 + 终止标志的传递盒（迭代器之间不能 return 值，用引用类型传）。</summary>
        private sealed class FlowBox
        {
            public Flow Signal = Flow.None;
        }

        /// <summary>兼容入口：同步跑完一整个 tick，返回执行结果。</summary>
        public ExecResult RunTick(Routine routine, ExecContext ctx)
        {
            if (routine == null)
                throw new System.ArgumentNullException(nameof(routine));
            foreach (StepInfo _ in Execute(routine, ctx)) { }
            return ctx.HungThisTick ? ExecResult.Hung : ExecResult.Completed;
        }

        /// <summary>
        /// 步骤机：开始一个 tick，逐条吐出语句执行步骤（懒执行）。
        /// 开头重置周期预算（§4：预算按 tick 结算）和统计计数。
        /// 未知函数仍抛 UndefinedReferenceException（由拉取方捕获）。
        /// </summary>
        public IEnumerable<StepInfo> Execute(Routine routine, ExecContext ctx)
        {
            if (routine == null)
                throw new System.ArgumentNullException(nameof(routine));
            if (ctx == null)
                throw new System.ArgumentNullException(nameof(ctx));

            ctx.Budget.Reset();
            ctx.SkippedByBudget = 0;
            ctx.StatementsExecuted = 0;
            ctx.HungThisTick = false;

            var box = new FlowBox();
            foreach (StepInfo step in ExecuteBody(routine.Lines, ctx, box))
                yield return step;
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

        private IEnumerable<StepInfo> ExecuteBody(Block[] body, ExecContext ctx, FlowBox box)
        {
            for (int i = 0; i < body.Length; i++)
            {
                foreach (StepInfo step in ExecuteStatement(body[i], ctx, box))
                {
                    yield return step;
                    if (ctx.HungThisTick || box.Signal != Flow.None)
                        yield break; // 卡死或 return/break/continue：后续语句不再执行，信号交给上层
                }
                if (ctx.HungThisTick || box.Signal != Flow.None)
                    yield break;
            }
        }

        private IEnumerable<StepInfo> ExecuteStatement(Block s, ExecContext ctx, FlowBox box)
        {
            ctx.StatementsExecuted++;
            if (ctx.StatementsExecuted > ctx.Limits.MaxStatementsPerTick)
            {
                ctx.HungThisTick = true;
                yield return new StepInfo(s, StepStatus.Hung);
                yield break;
            }

            switch (s.Kind)
            {
                case BlockKind.Call:
                    if (!ctx.Functions.TryGet(s.CallName, out IBuiltin fn))
                        throw new UndefinedReferenceException(s.CallName);
                    if (!ctx.Budget.TrySpend(fn.CpuCost))
                    {
                        ctx.SkippedByBudget++;
                        yield return new StepInfo(s, StepStatus.OptimizedOut);
                        yield break;
                    }
                    // 参数在调用前求值——attack(n) 的 n 在此刻从黑板/属性解析成 Value
                    var args = new Value[s.Args.Length];
                    for (int i = 0; i < args.Length; i++)
                        args[i] = Eval(s.Args[i], ctx);
                    fn.Invoke(ctx, args);
                    yield return new StepInfo(s, StepStatus.Executed);
                    yield break;

                case BlockKind.Assign:
                    if (!ctx.Budget.TrySpend(1))
                    {
                        ctx.SkippedByBudget++;
                        yield return new StepInfo(s, StepStatus.OptimizedOut);
                        yield break;
                    }
                    ctx.Vars.TrySet(s.Target, Eval(s.ValueExpr, ctx));
                    yield return new StepInfo(s, StepStatus.Executed);
                    yield break;

                case BlockKind.If:
                    if (!ctx.Budget.TrySpend(1))
                    {
                        ctx.SkippedByBudget++;
                        yield return new StepInfo(s, StepStatus.OptimizedOut);
                        yield break;
                    }
                    yield return new StepInfo(s, StepStatus.Executed);
                    foreach (StepInfo step in ExecuteBody(Eval(s.Condition, ctx).AsBool() ? s.Body : s.ElseBody, ctx, box))
                        yield return step;
                    yield break;

                case BlockKind.For:
                    foreach (StepInfo step in DoFor(s, ctx, box))
                        yield return step;
                    yield break;

                case BlockKind.While:
                    foreach (StepInfo step in DoWhile(s, ctx, box))
                        yield return step;
                    yield break;

                case BlockKind.Return:
                    if (!ctx.Budget.TrySpend(1))
                    {
                        ctx.SkippedByBudget++;
                        yield return new StepInfo(s, StepStatus.OptimizedOut);
                        yield break;
                    }
                    box.Signal = Flow.Return;
                    yield return new StepInfo(s, StepStatus.Executed);
                    yield break;

                case BlockKind.Break:
                    if (!ctx.Budget.TrySpend(1))
                    {
                        ctx.SkippedByBudget++;
                        yield return new StepInfo(s, StepStatus.OptimizedOut);
                        yield break;
                    }
                    box.Signal = Flow.Break;
                    yield return new StepInfo(s, StepStatus.Executed);
                    yield break;

                case BlockKind.Continue:
                    if (!ctx.Budget.TrySpend(1))
                    {
                        ctx.SkippedByBudget++;
                        yield return new StepInfo(s, StepStatus.OptimizedOut);
                        yield break;
                    }
                    box.Signal = Flow.Continue;
                    yield return new StepInfo(s, StepStatus.Executed);
                    yield break;

                default:
                    throw new System.InvalidOperationException($"未知的语句类型：{s.Kind}");
            }
        }

        // for (LoopVar = Init; Condition; LoopVar += Step) { Body }
        // for 行每圈高亮一次（调试器习惯：转一圈亮一下）
        private IEnumerable<StepInfo> DoFor(Block s, ExecContext ctx, FlowBox box)
        {
            if (s.LoopVar != null && s.Init != null)
                ctx.Vars.TrySet(s.LoopVar, Eval(s.Init, ctx));

            int iterations = 0;
            while (s.Condition == null || Eval(s.Condition, ctx).AsBool())
            {
                if (++iterations > ctx.Limits.MaxLoopIterations)
                {
                    ctx.HungThisTick = true;
                    yield return new StepInfo(s, StepStatus.Hung);
                    yield break;
                }
                if (!ctx.Budget.TrySpend(1)) // 每次迭代的循环开销
                {
                    ctx.SkippedByBudget++;
                    yield return new StepInfo(s, StepStatus.OptimizedOut);
                    yield break;
                }

                yield return new StepInfo(s, StepStatus.Executed);

                foreach (StepInfo step in ExecuteBody(s.Body, ctx, box))
                {
                    yield return step;
                    if (ctx.HungThisTick || box.Signal != Flow.None)
                        yield break;
                }
                if (ctx.HungThisTick)
                    yield break;
                if (box.Signal == Flow.Break)
                {
                    box.Signal = Flow.None; // break 被本循环消费
                    yield break;
                }
                if (box.Signal == Flow.Continue)
                {
                    box.Signal = Flow.None; // continue 被本循环消费，进入下一圈
                }
                else if (box.Signal == Flow.Return)
                {
                    yield break; // return 穿透到函数顶层
                }

                if (s.LoopVar != null && s.Step != null && ctx.Vars.TryGet(s.LoopVar, out Value current))
                    ctx.Vars.TrySet(s.LoopVar, Value.Of(current.AsNumber() + Eval(s.Step, ctx).AsNumber()));
            }
        }

        private IEnumerable<StepInfo> DoWhile(Block s, ExecContext ctx, FlowBox box)
        {
            int iterations = 0;
            while (s.Condition == null || Eval(s.Condition, ctx).AsBool())
            {
                if (++iterations > ctx.Limits.MaxLoopIterations)
                {
                    ctx.HungThisTick = true;
                    yield return new StepInfo(s, StepStatus.Hung);
                    yield break;
                }
                if (!ctx.Budget.TrySpend(1))
                {
                    ctx.SkippedByBudget++;
                    yield return new StepInfo(s, StepStatus.OptimizedOut);
                    yield break;
                }

                yield return new StepInfo(s, StepStatus.Executed);

                foreach (StepInfo step in ExecuteBody(s.Body, ctx, box))
                {
                    yield return step;
                    if (ctx.HungThisTick || box.Signal != Flow.None)
                        yield break;
                }
                if (ctx.HungThisTick)
                    yield break;
                if (box.Signal == Flow.Break)
                {
                    box.Signal = Flow.None;
                    yield break;
                }
                if (box.Signal == Flow.Continue)
                {
                    box.Signal = Flow.None;
                }
                else if (box.Signal == Flow.Return)
                {
                    yield break;
                }
            }
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
