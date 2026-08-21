using NUnit.Framework;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Tests.Code
{
    [TestFixture]
    public class BlockTreeTests
    {
        [Test]
        public void CountStatements_IncludesNestedBodies()
        {
            // if { attack, attack } else { heal } + for { attack } → 1+2+1 + 1+1 = 6
            var branch = Block.If(Expr.Bool(true),
                new[] { Block.Call("attack"), Block.Call("attack") },
                new[] { Block.Call("heal", Expr.Num(1)) });
            var loop = Block.For("i", Expr.Num(0), Expr.Num(1), Expr.Num(1), Block.Call("attack"));

            Assert.AreEqual(6, BlockTree.CountStatements(new[] { branch, loop }));
        }

        [Test]
        public void CountStatements_EmptyBody_IsZero()
        {
            Assert.AreEqual(0, BlockTree.CountStatements(System.Array.Empty<Block>()));
        }

        [Test]
        public void GetBody_ElseBranch_ReturnsElseBody()
        {
            var elseBody = new[] { Block.Call("heal", Expr.Num(1)) };
            var branch = Block.If(Expr.Bool(true), new[] { Block.Call("attack") }, elseBody);

            Assert.AreSame(elseBody, BlockTree.GetBody(branch, 1));
        }

        [Test]
        public void GetBody_LoopHasNoBranch1_Throws()
        {
            var loop = Block.While(null, Block.Call("attack"));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => BlockTree.GetBody(loop, 1));
        }

        [Test]
        public void Contains_SelfAndDescendants_True_Unrelated_False()
        {
            var inner = Block.Call("attack");
            var loop = Block.For("i", Expr.Num(0), Expr.Num(3), Expr.Num(1), inner);
            var outside = Block.Call("heal", Expr.Num(1));

            Assert.IsTrue(BlockTree.Contains(loop, loop));
            Assert.IsTrue(BlockTree.Contains(loop, inner));
            Assert.IsFalse(BlockTree.Contains(loop, outside));
        }
    }

    [TestFixture]
    public class BlockClonerTests
    {
        [Test]
        public void Clone_IsDeepCopy_OriginalUnaffectedByEdits()
        {
            var original = Block.For("i", Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)), Expr.Num(1),
                Block.Call("attack", Expr.Num(10)));

            Block copy = BlockCloner.Clone(original);
            copy.Condition.Right.Literal = Value.Of(99);

            // 改副本不影响原件
            Assert.AreEqual(3, original.Condition.Right.Literal.AsNumber(), 0.0001);
            Assert.AreEqual(99, copy.Condition.Right.Literal.AsNumber(), 0.0001);
            Assert.AreNotSame(original.Body, copy.Body);
        }
    }

    [TestFixture]
    public class RoutineEditorTests
    {
        [Test]
        public void Insert_RootGap_AddsStatement()
        {
            var editor = new RoutineEditor();

            editor.Insert(new Slot(null, 0, 0), Block.Call("attack"));

            Assert.AreEqual(1, editor.Root.Length);
            Assert.AreEqual(BlockKind.Call, editor.Root[0].Kind);
        }

        [Test]
        public void Insert_IntoIfBody_NestsProperly()
        {
            var editor = new RoutineEditor();
            var branch = Block.If(Expr.Bool(true), new[] { Block.Call("attack") });
            editor.Insert(new Slot(null, 0, 0), branch);

            editor.Insert(new Slot(branch, 0, 1), Block.Call("heal", Expr.Num(5)));

            Assert.AreEqual(2, branch.Body.Length);
            Assert.AreEqual("heal", branch.Body[1].CallName);
        }

        [Test]
        public void Insert_IntoElseBranch_UsesBranch1()
        {
            var editor = new RoutineEditor();
            var branch = Block.If(Expr.Bool(true),
                new[] { Block.Call("attack") }, new[] { Block.Call("heal", Expr.Num(1)) });
            editor.Insert(new Slot(null, 0, 0), branch);

            editor.Insert(new Slot(branch, 1, 0), Block.Call("shield", Expr.Num(2)));

            Assert.AreEqual(2, branch.ElseBody.Length);
            Assert.AreEqual("shield", branch.ElseBody[0].CallName);
        }

        [Test]
        public void Insert_OverMaxLines_Rejected()
        {
            var editor = new RoutineEditor(); // 默认 8 行
            for (int i = 0; i < 8; i++)
                editor.Insert(new Slot(null, 0, i), Block.Call("attack"));

            Assert.IsFalse(editor.CanInsert(new Slot(null, 0, 8), Block.Call("attack")));
            Assert.Throws<System.InvalidOperationException>(
                () => editor.Insert(new Slot(null, 0, 8), Block.Call("attack")));
        }

        [Test]
        public void Insert_NestedStatementsCountTowardMaxLines()
        {
            var editor = new RoutineEditor(); // 8 行
            // for + 7 条循环体 = 8，正好放满
            var body = new Block[7];
            for (int i = 0; i < body.Length; i++)
                body[i] = Block.Call("attack");
            var loop = Block.For("i", Expr.Num(0), Expr.Num(7), Expr.Num(1), body);
            Assert.IsTrue(editor.CanInsert(new Slot(null, 0, 0), loop));

            editor.Insert(new Slot(null, 0, 0), loop);

            // 再放任何东西都超行数
            Assert.IsFalse(editor.CanInsert(new Slot(null, 0, 1), Block.Call("attack")));
        }

        [Test]
        public void Remove_DeletesWholeSubtree()
        {
            var editor = new RoutineEditor();
            var branch = Block.If(Expr.Bool(true),
                new[] { Block.Call("attack"), Block.Call("attack") });
            editor.Insert(new Slot(null, 0, 0), branch);
            editor.Insert(new Slot(null, 0, 1), Block.Call("heal", Expr.Num(1)));
            Assert.AreEqual(4, editor.StatementCount);

            editor.Remove(new Slot(null, 0, 0));

            Assert.AreEqual(1, editor.Root.Length);
            Assert.AreEqual(1, editor.StatementCount);
        }

        [Test]
        public void Move_FromLoopBodyToRoot()
        {
            var editor = new RoutineEditor();
            var inner = Block.Call("attack");
            var loop = Block.For("i", Expr.Num(0), Expr.Num(3), Expr.Num(1), inner);
            editor.Insert(new Slot(null, 0, 0), loop);

            Assert.IsTrue(editor.CanMove(new Slot(loop, 0, 0), new Slot(null, 0, 1)));
            editor.Move(new Slot(loop, 0, 0), new Slot(null, 0, 1));

            Assert.AreEqual(2, editor.Root.Length);
            Assert.AreSame(inner, editor.Root[1]);
            Assert.AreEqual(0, loop.Body.Length);
        }

        [Test]
        public void Move_IntoOwnSubtree_Rejected()
        {
            var editor = new RoutineEditor();
            var loop = Block.For("i", Expr.Num(0), Expr.Num(3), Expr.Num(1), Block.Call("attack"));
            editor.Insert(new Slot(null, 0, 0), loop);

            // 把 for 拖进它自己的循环体里 = 非法
            Assert.IsFalse(editor.CanMove(new Slot(null, 0, 0), new Slot(loop, 0, 1)));
            Assert.Throws<System.InvalidOperationException>(
                () => editor.Move(new Slot(null, 0, 0), new Slot(loop, 0, 1)));
        }

        [Test]
        public void Move_NoOpPosition_Rejected()
        {
            var editor = new RoutineEditor();
            editor.Insert(new Slot(null, 0, 0), Block.Call("attack"));
            editor.Insert(new Slot(null, 0, 1), Block.Call("heal", Expr.Num(1)));

            // 原地和紧邻其后都算"没有变化"
            Assert.IsFalse(editor.CanMove(new Slot(null, 0, 0), new Slot(null, 0, 0)));
            Assert.IsFalse(editor.CanMove(new Slot(null, 0, 0), new Slot(null, 0, 1)));
            // 向前挪一格是合法的
            Assert.IsTrue(editor.CanMove(new Slot(null, 0, 1), new Slot(null, 0, 0)));
        }

        [Test]
        public void BuildRoutine_ProducesExecutableRoutine()
        {
            var editor = new RoutineEditor();
            editor.Insert(new Slot(null, 0, 0), Block.Call("attack"));

            Routine routine = editor.BuildRoutine();

            Assert.AreEqual(1, routine.LineCount);
            Assert.AreEqual(Routine.DefaultMaxLines, routine.MaxLines);
        }
    }
}
