using System;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Loot;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Code
{
    /// <summary>BlockJson 序列化往返测试：全部 BlockKind / ExprKind 都必须无损往返。</summary>
    [TestFixture]
    public class BlockJsonTests
    {
        [Test]
        public void 往返_全部语句与表达式()
        {
            Block[] program =
            {
                Block.Call("attack", Expr.Num(1)),
                Block.Call("heal"),
                Block.Assign("a", Expr.Bin(BinaryOp.Add, Expr.Var("a"), Expr.Num(3))),
                Block.If(
                    Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.3)),
                    new[] { Block.Call("heal", Expr.Num(5)) },
                    new[] { Block.Call("shield", Expr.Num(2)) }),
                Block.For("i", Expr.Num(0),
                    Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)), Expr.Num(1),
                    Block.Call("attack"),
                    Block.Break()),
                Block.While(null,
                    Block.Continue()),
                Block.While(
                    Expr.Un(UnaryOp.Not, Expr.Var("flag")),
                    Block.Return()),
                Block.Assign("neg", Expr.Un(UnaryOp.Neg, Expr.Num(2))),
                Block.Assign("t", Expr.Bool(true)),
                Block.Assign("m", Expr.Bin(BinaryOp.Mod, Expr.Num(7), Expr.Num(2))),
            };

            string json = MiniJson.Write(BlockJson.ToJson(program));
            Block[] parsed = BlockJson.FromJson(MiniJson.Parse(json));

            Assert.AreEqual(program.Length, parsed.Length);
            for (int i = 0; i < program.Length; i++)
                AssertBlockEqual(program[i], parsed[i], $"第 {i} 条");
        }

        [Test]
        public void 往返_空与嵌套体()
        {
            Block[] empty = { };
            Assert.AreEqual(0, BlockJson.FromJson(MiniJson.Parse(MiniJson.Write(BlockJson.ToJson(empty)))).Length);

            var nested = new[] { Block.If(Expr.Var("x"), new[] { Block.If(Expr.Var("y"), new[] { Block.Return() }) }) };
            Block[] parsed = BlockJson.FromJson(MiniJson.Parse(MiniJson.Write(BlockJson.ToJson(nested))));
            Assert.AreEqual(BlockKind.If, parsed[0].Kind);
            Assert.AreEqual(1, parsed[0].Body.Length);
            Assert.AreEqual(BlockKind.If, parsed[0].Body[0].Kind);
            Assert.AreEqual(BlockKind.Return, parsed[0].Body[0].Body[0].Kind);
            Assert.AreEqual(0, parsed[0].ElseBody.Length);
        }

        [Test]
        public void 解析_非法输入_抛异常()
        {
            Assert.Throws<FormatException>(() => BlockJson.FromJson(MiniJson.Parse("[42]")));
            Assert.Throws<FormatException>(() => BlockJson.FromJson(MiniJson.Parse("[{\"kind\":\"goto\"}]")));
            Assert.Throws<FormatException>(() => BlockJson.FromJson(MiniJson.Parse("[{\"kind\":\"call\"}]")));
            Assert.Throws<FormatException>(() => BlockJson.FromJson(MiniJson.Parse(
                "[{\"kind\":\"bin\"}]")), "顶层不是语句");
        }

        private static void AssertBlockEqual(Block a, Block b, string where)
        {
            Assert.AreEqual(a.Kind, b.Kind, $"{where} kind");
            Assert.AreEqual(a.CallName, b.CallName, $"{where} name");
            Assert.AreEqual(a.Target, b.Target, $"{where} target");
            Assert.AreEqual(a.LoopVar, b.LoopVar, $"{where} loopVar");
            if (a.Args.Length == 0)
                Assert.AreEqual(0, b.Args.Length, $"{where} args");
            else
            {
                Assert.AreEqual(a.Args.Length, b.Args.Length, $"{where} args 数量");
                for (int i = 0; i < a.Args.Length; i++)
                    AssertExprEqual(a.Args[i], b.Args[i], $"{where} arg{i}");
            }
            AssertExprEqual(a.ValueExpr, b.ValueExpr, $"{where} value");
            AssertExprEqual(a.Condition, b.Condition, $"{where} cond");
            AssertExprEqual(a.Init, b.Init, $"{where} init");
            AssertExprEqual(a.Step, b.Step, $"{where} step");
            Assert.AreEqual(a.Body.Length, b.Body.Length, $"{where} body");
            for (int i = 0; i < a.Body.Length; i++)
                AssertBlockEqual(a.Body[i], b.Body[i], $"{where} body[{i}]");
            Assert.AreEqual(a.ElseBody.Length, b.ElseBody.Length, $"{where} else");
            for (int i = 0; i < a.ElseBody.Length; i++)
                AssertBlockEqual(a.ElseBody[i], b.ElseBody[i], $"{where} else[{i}]");
        }

        private static void AssertExprEqual(Expr a, Expr b, string where)
        {
            if (a == null)
            {
                Assert.IsNull(b, where);
                return;
            }
            Assert.AreEqual(a.Kind, b.Kind, where);
            switch (a.Kind)
            {
                case ExprKind.Literal:
                    Assert.AreEqual(a.Literal.Kind, b.Literal.Kind, where);
                    Assert.AreEqual(a.Literal.AsNumber(), b.Literal.AsNumber(), 1e-9, where);
                    break;
                case ExprKind.Var:
                    Assert.AreEqual(a.VarName, b.VarName, where);
                    break;
                case ExprKind.Binary:
                    Assert.AreEqual(a.BinOp, b.BinOp, where);
                    AssertExprEqual(a.Left, b.Left, where);
                    AssertExprEqual(a.Right, b.Right, where);
                    break;
                case ExprKind.Unary:
                    Assert.AreEqual(a.UnOp, b.UnOp, where);
                    AssertExprEqual(a.Left, b.Left, where);
                    break;
            }
        }
    }
}
