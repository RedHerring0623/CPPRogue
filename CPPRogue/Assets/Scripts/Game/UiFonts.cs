using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>UI 字体：优先系统字体（代码用 Consolas、文案用微软雅黑），失败退回内置字体。</summary>
    public static class UiFonts
    {
        public static readonly Font Text = Load("Microsoft YaHei");
        public static readonly Font Code = Load("Consolas");

        private static Font Load(string osFont)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(osFont, 16);
            }
            catch
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }
    }
}
