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
    /// 解释器的单元测试：验证"拼一段程序 → 跑若干 tick → 世界收到正确的调用序列"整条链。
    /// 重点覆盖参数化：attack(n)、for(i=0; i&lt;x; i++) 的 n、x 都来自变量。
    /// </summary>
    [TestFixture]
    public class InterpreterTests
    {
        private static ExecContext NewContext(
            FakeCombatWorld world,
            CpuBudget budget = null,
            Blackboard vars = null,
            IPropertySource properties = null,
            InterpreterLimits limits = null)
        {
            return new ExecContext(world, BuiltinTable.CreateDefault(), budget, vars, null, limits, properties);
        }

        [Test]
        public void Call_Attack_NoArgs_UsesDefaultDamage()
        {
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);

            new Interpreter().RunTick(new[] { Block.Call("attack") }, ctx);

            Assert.AreEqual(1, world.Attacks.Count);
            Assert.AreEqual(10f, world.Attacks[0].Damage, 0.0001f);
        }

        [Test]
        public void Call_Attack_LiteralArg_UsesArgAsDamage()
        {
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);

            new Interpreter().RunTick(new[] { Block.Call("attack", Expr.Num(25)) }, ctx);

            Assert.AreEqual(1, world.Attacks.Count);
            Assert.AreEqual(25f, world.Attacks[0].Damage, 0.0001f);
        }

        [Test]
        public void Call_Attack_VariableArg_ResolvesFromBlackboard()
        {
            // attack(n)：n 是玩家变量
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            vars.TrySet("n", Value.Of(30));
            var ctx = NewContext(world, vars: vars);

            new Interpreter().RunTick(new[] { Block.Call("attack", Expr.Var("n")) }, ctx);

            Assert.AreEqual(30f, world.Attacks[0].Damage, 0.0001f);
        }

        [Test]
        public void Call_Attack_ExpressionArg_ComputesValue()
        {
            // attack(n * m)：参数位可以是任意表达式
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            vars.TrySet("n", Value.Of(2));
            vars.TrySet("m", Value.Of(5));
            var ctx = NewContext(world, vars: vars);

            var arg = Expr.Bin(BinaryOp.Mul, Expr.Var("n"), Expr.Var("m"));
            new Interpreter().RunTick(new[] { Block.Call("attack", arg) }, ctx);

            Assert.AreEqual(10f, world.Attacks[0].Damage, 0.0001f);
        }

        [Test]
        public void Assign_Variable_PersistsAcrossTicks()
        {
            // tick1 写 a=5，tick2 用 a——变量在 tick 之间持久（§3.1）
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            var ctx = NewContext(world, vars: vars);
            var interp = new Interpreter();

            interp.RunTick(new[] { Block.Assign("a", Expr.Num(5)) }, ctx);
            interp.RunTick(new[] { Block.Call("attack", Expr.Var("a")) }, ctx);

            Assert.AreEqual(5f, world.Attacks[0].Damage, 0.0001f);
        }

        [Test]
        public void Assign_Expression_StoresComputedValue()
        {
            var vars = new Blackboard();
            var ctx = NewContext(new FakeCombatWorld(), vars: vars);

            new Interpreter().RunTick(new[] { Block.Assign("a", Expr.Bin(BinaryOp.Add, Expr.Num(2), Expr.Num(3))) }, ctx);

            Assert.IsTrue(vars.TryGet("a", out Value v));
            Assert.AreEqual(5, v.AsNumber(), 0.0001);
        }

        [Test]
        public void For_BodyRunsNTimes()
        {
            // for (i = 0; i < 3; i += 1) attack();
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);

            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)),
                Expr.Num(1),
                Block.Call("attack"));
            new Interpreter().RunTick(new[] { loop }, ctx);

            Assert.AreEqual(3, world.Attacks.Count);
        }

        [Test]
        public void For_LimitFromVariable_RespectsVariable()
        {
            // for (i = 0; i < x; i += 1) heal(1); —— 上限 x 是玩家变量
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            vars.TrySet("x", Value.Of(4));
            var ctx = NewContext(world, vars: vars);

            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Var("x")),
                Expr.Num(1),
                Block.Call("heal", Expr.Num(1)));
            new Interpreter().RunTick(new[] { loop }, ctx);

            Assert.AreEqual(4, world.Heals.Count);
        }

        [Test]
        public void For_StepFromVariable_ComputesEachIteration()
        {
            // for (i = 0; i < 10; i += s) —— 步长 s=3 → i = 0,3,6,9 共 4 次
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            vars.TrySet("s", Value.Of(3));
            var ctx = NewContext(world, vars: vars);

            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(10)),
                Expr.Var("s"),
                Block.Call("attack"));
            new Interpreter().RunTick(new[] { loop }, ctx);

            Assert.AreEqual(4, world.Attacks.Count);
        }

        [Test]
        public void For_Break_StopsLoop()
        {
            // for (i = 0; i < 10; i += 1) { if (i > 1) break; attack(); }
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);

            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(10)),
                Expr.Num(1),
                Block.If(Expr.Bin(BinaryOp.Gt, Expr.Var("i"), Expr.Num(1)), new[] { Block.Break() }),
                Block.Call("attack"));
            new Interpreter().RunTick(new[] { loop }, ctx);

            Assert.AreEqual(2, world.Attacks.Count);
        }

        [Test]
        public void For_VariableSurvivesAfterLoop()
        {
            // 循环变量也是黑板变量，出循环后仍可读
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            var ctx = NewContext(world, vars: vars);

            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(5)),
                Expr.Num(1),
                Block.Call("attack"));
            new Interpreter().RunTick(new[] { loop }, ctx);

            Assert.IsTrue(vars.TryGet("i", out Value i));
            Assert.AreEqual(5, i.AsNumber(), 0.0001);
        }

        [Test]
        public void If_LowHp_TakesHealBranch()
        {
            // if (hp < 0.3) heal(50); else attack(); —— hp 来自系统属性源
            var world = new FakeCombatWorld();
            var props = new FakePropertySource(new System.Collections.Generic.Dictionary<string, Value>
            {
                { "hp", Value.Of(0.2) },
            });
            var ctx = NewContext(world, properties: props);

            var branch = Block.If(
                Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.3)),
                new[] { Block.Call("heal", Expr.Num(50)) },
                new[] { Block.Call("attack") });
            new Interpreter().RunTick(new[] { branch }, ctx);

            Assert.AreEqual(1, world.Heals.Count);
            Assert.AreEqual(50f, world.Heals[0], 0.0001f);
            Assert.AreEqual(0, world.Attacks.Count);
        }

        [Test]
        public void If_HighHp_TakesElseBranch()
        {
            var world = new FakeCombatWorld();
            var props = new FakePropertySource(new System.Collections.Generic.Dictionary<string, Value>
            {
                { "hp", Value.Of(0.8) },
            });
            var ctx = NewContext(world, properties: props);

            var branch = Block.If(
                Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.3)),
                new[] { Block.Call("heal", Expr.Num(50)) },
                new[] { Block.Call("attack") });
            new Interpreter().RunTick(new[] { branch }, ctx);

            Assert.AreEqual(0, world.Heals.Count);
            Assert.AreEqual(1, world.Attacks.Count);
        }

        [Test]
        public void UndefinedVariable_EvaluatesAsZero()
        {
            // 未定义变量按 0（"未初始化"的老传统），不炸解释器
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);

            new Interpreter().RunTick(new[] { Block.Call("attack", Expr.Var("nope")) }, ctx);

            Assert.AreEqual(0f, world.Attacks[0].Damage, 0.0001f);
        }

        [Test]
        public void Budget_Exhausted_StatementsOptimizedOut()
        {
            // 预算只够一次 attack（cost 2）：第二条被"优化掉"，tick 仍算 Completed
            var world = new FakeCombatWorld();
            var ctx = NewContext(world, budget: new CpuBudget(2));

            var result = new Interpreter().RunTick(
                new[] { Block.Call("attack"), Block.Call("attack") }, ctx);

            Assert.AreEqual(ExecResult.Completed, result);
            Assert.AreEqual(1, world.Attacks.Count);
            Assert.AreEqual(1, ctx.SkippedByBudget);
        }

        [Test]
        public void Budget_RestoresEachTick()
        {
            // 周期按 tick 结算：上一 tick 花光不影响下一 tick
            var world = new FakeCombatWorld();
            var ctx = NewContext(world, budget: new CpuBudget(2));
            var interp = new Interpreter();

            interp.RunTick(new[] { Block.Call("attack"), Block.Call("attack") }, ctx);
            interp.RunTick(new[] { Block.Call("attack") }, ctx);

            Assert.AreEqual(2, world.Attacks.Count);
        }

        [Test]
        public void While_TrueForever_ReportedAsHung()
        {
            // while(true) → 迭代到上限被 kill，游戏层据此施加"卡死"惩罚
            var world = new FakeCombatWorld();
            var limits = new InterpreterLimits { MaxLoopIterations = 5 };
            var ctx = NewContext(world, budget: new CpuBudget(64), limits: limits);

            var result = new Interpreter().RunTick(new[] { Block.While(null, Block.Call("attack")) }, ctx);

            Assert.AreEqual(ExecResult.Hung, result);
            Assert.AreEqual(5, world.Attacks.Count);
        }

        [Test]
        public void While_WithBreak_ExitsCleanly()
        {
            var world = new FakeCombatWorld();
            var vars = new Blackboard();
            var ctx = NewContext(world, vars: vars);

            // while (true) { a = a + 1; if (a > 2) break; }
            var loop = Block.While(null,
                Block.Assign("a", Expr.Bin(BinaryOp.Add, Expr.Var("a"), Expr.Num(1))),
                Block.If(Expr.Bin(BinaryOp.Gt, Expr.Var("a"), Expr.Num(2)), new[] { Block.Break() }));
            var result = new Interpreter().RunTick(new[] { loop }, ctx);

            Assert.AreEqual(ExecResult.Completed, result);
            Assert.IsTrue(vars.TryGet("a", out Value a));
            Assert.AreEqual(3, a.AsNumber(), 0.0001);
        }

        [Test]
        public void Return_StopsRestOfTick()
        {
            var world = new FakeCombatWorld();
            var ctx = NewContext(world);

            new Interpreter().RunTick(new[] { Block.Return(), Block.Call("attack") }, ctx);

            Assert.AreEqual(0, world.Attacks.Count);
        }

        [Test]
        public void Call_UnknownFunction_ThrowsUndefinedReference()
        {
            var ctx = NewContext(new FakeCombatWorld());

            Assert.Throws<UndefinedReferenceException>(
                () => new Interpreter().RunTick(new[] { Block.Call("fireball") }, ctx));
        }
    }
}
