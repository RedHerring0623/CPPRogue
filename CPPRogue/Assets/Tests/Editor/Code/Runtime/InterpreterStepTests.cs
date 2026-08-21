using System.Collections.Generic;
using NUnit.Framework;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;
using CPPRogue.Core.Combat;
using CPPRogue.Core.Computing;
using CPPRogue.Core.Tests.Fakes;

namespace CPPRogue.Core.Tests.Code.Runtime
{
    /// <summary>
    /// 步骤机（Execute 迭代器）的契约测试：
    /// 懒执行（拉一步才执行一条语句）、步骤携带语句引用、状态正确。
    /// 这是 UI 高亮/实时演出依赖的底层保证。
    /// </summary>
    [TestFixture]
    public class InterpreterStepTests
    {
        private static ExecContext NewContext(
            FakeCombatWorld world,
            CpuBudget budget = null,
            Blackboard vars = null,
            InterpreterLimits limits = null)
        {
            return new ExecContext(world, BuiltinTable.CreateDefault(), budget, vars, null, limits, null);
        }

        [Test]
        public void Execute_IsLazy_OneStatementPerPull()
        {
            // 核心契约：拉一步只执行一条语句——驱动层控制节奏的根基
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);
            var routine = new Routine(new[] { Block.Call("attack"), Block.Call("heal", Expr.Num(5)) });

            IEnumerator<StepInfo> steps = new Interpreter().Execute(routine, ctx).GetEnumerator();

            Assert.IsTrue(steps.MoveNext());
            Assert.AreEqual(1, world.Attacks.Count);
            Assert.AreEqual(0, world.Heals.Count); // 第二条还没执行

            Assert.IsTrue(steps.MoveNext());
            Assert.AreEqual(1, world.Heals.Count); // 拉了才执行

            Assert.IsFalse(steps.MoveNext());
        }

        [Test]
        public void Execute_StepCarriesStatementReference()
        {
            // UI 靠引用比对定位高亮行
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);
            var attack = Block.Call("attack");
            var routine = new Routine(new[] { attack });

            foreach (StepInfo step in new Interpreter().Execute(routine, ctx))
            {
                Assert.AreSame(attack, step.Statement);
                Assert.AreEqual(StepStatus.Executed, step.Status);
            }
        }

        [Test]
        public void Execute_IfStepsComeBeforeBodySteps()
        {
            // if 行先亮，然后才轮到分支体内的语句
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);
            var routine = new Routine(new[]
            {
                Block.If(Expr.Bool(true), new[] { Block.Call("attack") }),
            });

            var statuses = new List<StepInfo>();
            foreach (StepInfo step in new Interpreter().Execute(routine, ctx))
                statuses.Add(step);

            Assert.AreEqual(2, statuses.Count);
            Assert.AreEqual(BlockKind.If, statuses[0].Statement.Kind);
            Assert.AreEqual(BlockKind.Call, statuses[1].Statement.Kind);
        }

        [Test]
        public void Execute_ForHighlightsOncePerIteration()
        {
            // for 行每圈亮一次 + 每圈一条 attack：3 圈 = 6 步
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);
            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)),
                Expr.Num(1),
                Block.Call("attack"));
            var routine = new Routine(new[] { loop });

            int count = 0;
            foreach (StepInfo _ in new Interpreter().Execute(routine, ctx))
                count++;

            Assert.AreEqual(6, count);
            Assert.AreEqual(3, world.Attacks.Count);
        }

        [Test]
        public void Execute_OptimizedOutStepSurfaced()
        {
            // 周期不足的语句会吐出 OptimizedOut 步骤（UI 灰显的依据）
            var world = new FakeCombatWorld();
            var ctx = NewContext(world, budget: new CpuBudget(2));
            var routine = new Routine(new[] { Block.Call("attack"), Block.Call("attack") });

            var statuses = new List<StepInfo>();
            foreach (StepInfo step in new Interpreter().Execute(routine, ctx))
                statuses.Add(step);

            Assert.AreEqual(2, statuses.Count);
            Assert.AreEqual(StepStatus.Executed, statuses[0].Status);
            Assert.AreEqual(StepStatus.OptimizedOut, statuses[1].Status);
        }

        [Test]
        public void Execute_HungStepIsLast()
        {
            // 死循环被 kill：最后一步是 Hung，之后不再有步骤
            var world = new FakeCombatWorld();
            var limits = new InterpreterLimits { MaxLoopIterations = 5 };
            var ctx = NewContext(world, budget: new CpuBudget(64), limits: limits);
            var routine = new Routine(new[] { Block.While(null, Block.Call("attack")) });

            StepInfo last = null;
            int count = 0;
            foreach (StepInfo step in new Interpreter().Execute(routine, ctx))
            {
                last = step;
                count++;
            }

            Assert.AreEqual(StepStatus.Hung, last.Status);
            Assert.AreEqual(BlockKind.While, last.Statement.Kind);
            Assert.IsTrue(ctx.HungThisTick);
            // 5 圈 ×（while 行 + attack）+ 最后的 Hung 步
            Assert.AreEqual(11, count);
        }

        [Test]
        public void Execute_VariablesVisibleBetweenPulls()
        {
            // 拉取之间世界/黑板在演化：tick1 写变量，tick2 读到（跨 tick 的懒语义）
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            var ctx = NewContext(world, vars: vars);
            var interp = new Interpreter();

            foreach (StepInfo _ in interp.Execute(new Routine(new[] { Block.Assign("a", Expr.Num(7)) }), ctx)) { }
            foreach (StepInfo _ in interp.Execute(new Routine(new[] { Block.Call("attack", Expr.Var("a")) }), ctx)) { }

            Assert.AreEqual(7f, world.Attacks[0].Damage, 0.0001f);
        }
    }
}
