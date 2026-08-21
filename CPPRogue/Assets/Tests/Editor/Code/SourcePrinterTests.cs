using NUnit.Framework;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Tests.Code
{
    /// <summary>SourcePrinter 的排版测试——它同时是 UI 源码面板和 Boss 屏显的基础。</summary>
    [TestFixture]
    public class SourcePrinterTests
    {
        [Test]
        public void Print_CallWithArgs_RendersLiteral()
        {
            var routine = new Routine(new[] { Block.Call("attack", Expr.Num(25)) });

            var lines = SourcePrinter.Print(routine);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("attack(25);", lines[0].Text);
        }

        [Test]
        public void Print_For_UsesVariableNames()
        {
            // for (i = 0; i < x; i += 1) { attack(); }
            var loop = Block.For("i",
                Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Var("x")),
                Expr.Num(1),
                Block.Call("attack"));
            var routine = new Routine(new[] { loop });

            var lines = SourcePrinter.Print(routine);

            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("for (i = 0; i < x; i += 1) {", lines[0].Text);
            Assert.AreEqual("attack();", lines[1].Text);
            Assert.AreEqual(1, lines[1].Indent);
            Assert.AreEqual("}", lines[2].Text);
        }

        [Test]
        public void Print_IfElse_RendersBothBranches()
        {
            var routine = new Routine(new[]
            {
                Block.If(
                    Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.5)),
                    new[] { Block.Call("heal", Expr.Num(30)) },
                    new[] { Block.Call("attack"), Block.Call("attack") }),
            });

            var lines = SourcePrinter.Print(routine);

            // if 行 / heal / } else { / attack / attack / }
            Assert.AreEqual(6, lines.Count);
            Assert.AreEqual("if (hp < 0.5) {", lines[0].Text);
            Assert.AreEqual("} else {", lines[2].Text);
            Assert.IsNull(lines[2].Statement); // 括号行不参与高亮
        }

        [Test]
        public void Print_WhileTrue_RendersTrue()
        {
            var routine = new Routine(new[] { Block.While(null, Block.Call("attack")) });

            var lines = SourcePrinter.Print(routine);

            Assert.AreEqual("while (true) {", lines[0].Text);
        }

        [Test]
        public void Print_EveryStatementHasStableReference()
        {
            // 高亮定位的根基：每条语句的行都持有它的 Block 引用
            var routine = new Routine(new[]
            {
                Block.Assign("a", Expr.Num(1)),
                Block.If(Expr.Bool(true), new[] { Block.Call("heal", Expr.Num(2)) }),
            });

            var lines = SourcePrinter.Print(routine);

            int withStatement = 0;
            foreach (var line in lines)
            {
                if (line.Statement != null)
                {
                    withStatement++;
                    Assert.IsNotNull(line.Statement.Kind);
                }
            }
            // assign + if + heal = 3 条语句行
            Assert.AreEqual(3, withStatement);
        }

        [Test]
        public void Print_Numbers_Format()
        {
            // 整数去小数点，小数保留
            var routine = new Routine(new[]
            {
                Block.Call("attack", Expr.Num(5)),
                Block.Call("attack", Expr.Num(2.5)),
            });

            var lines = SourcePrinter.Print(routine);

            Assert.AreEqual("attack(5);", lines[0].Text);
            Assert.AreEqual("attack(2.5);", lines[1].Text);
        }
    }
}
