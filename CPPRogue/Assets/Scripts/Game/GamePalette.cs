using System;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Game
{
    public sealed class PaletteEntry
    {
        public string Title;
        public Func<Block> Make;

        /// <summary>剩余可用数量（仓库持有 − BD 已用），实时计算；null = 不限量（测试 BD）。</summary>
        public Func<int?> Remaining;
    }

    /// <summary>可拖入的语法块清单（与 DemoUI 面板一致）。</summary>
    public static class GamePalette
    {
        public static readonly PaletteEntry[] Items =
        {
            new PaletteEntry { Title = "attack()", Make = () => Block.Call("attack") },
            new PaletteEntry { Title = "attack(n)", Make = () => Block.Call("attack", Expr.Num(10)) },
            new PaletteEntry { Title = "heal(n)", Make = () => Block.Call("heal", Expr.Num(5)) },
            new PaletteEntry { Title = "shield(n)", Make = () => Block.Call("shield", Expr.Num(10)) },
            new PaletteEntry { Title = "a = 值", Make = () => Block.Assign("a", Expr.Num(1)) },
            new PaletteEntry { Title = "if (hp < 0.5)", Make = () => Block.If(
                Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.5)),
                new[] { Block.Call("attack") }) },
            new PaletteEntry { Title = "if / else", Make = () => Block.If(
                Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.5)),
                new[] { Block.Call("attack") },
                new[] { Block.Call("heal", Expr.Num(5)) }) },
            new PaletteEntry { Title = "for (i < 3)", Make = () => Block.For("i", Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)), Expr.Num(1)) },
            new PaletteEntry { Title = "while (a < 3)", Make = () => Block.While(
                Expr.Bin(BinaryOp.Lt, Expr.Var("a"), Expr.Num(3))) },
            new PaletteEntry { Title = "while (true)", Make = () => Block.While(null) },
            new PaletteEntry { Title = "return;", Make = () => Block.Return() },
            new PaletteEntry { Title = "break;", Make = () => Block.Break() },
            new PaletteEntry { Title = "continue;", Make = () => Block.Continue() },
        };
    }
}
