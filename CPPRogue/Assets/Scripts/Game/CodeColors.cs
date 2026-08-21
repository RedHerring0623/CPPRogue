using CPPRogue.Core.Code.Ast;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>源码行配色（与 DemoUI 一致）：控制流蓝、流程关键字紫、括号灰。</summary>
    public static class CodeColors
    {
        public static readonly Color ControlFlow = FromHex(0x569CD6);
        public static readonly Color FlowKeyword = FromHex(0xC586C0);
        public static readonly Color Brace = FromHex(0x6E6E6E);
        public static readonly Color Plain = FromHex(0xD4D4D4);

        public static readonly Color ExecutedBg = FromHex(0xFFD54F); // 明黄：正在执行
        public static readonly Color ExecutedText = Color.black;
        public static readonly Color OptimizedBg = FromHex(0x464646);
        public static readonly Color OptimizedText = FromHex(0x969696);
        public static readonly Color HungBg = FromHex(0xAA2828);
        public static readonly Color HungText = Color.white;

        public static readonly Color GapIdle = FromHex(0x2B2B2E);
        public static readonly Color GapOk = FromHex(0x0091C3);
        public static readonly Color GapBad = FromHex(0x782828);

        public static Color BaseText(Block statement)
        {
            if (statement == null)
                return Brace;
            switch (statement.Kind)
            {
                case BlockKind.If:
                case BlockKind.For:
                case BlockKind.While:
                    return ControlFlow;
                case BlockKind.Return:
                case BlockKind.Break:
                case BlockKind.Continue:
                    return FlowKeyword;
                default:
                    return Plain;
            }
        }

        public static Color FromHex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
