using System;
using CPPRogue.Core.Codebase;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 主菜单（启动后第一屏）：启动进程 / 构建 / 词法树（禁用）/ 怪物图鉴 / 退出进程。
    /// 持有玩家局外档案（PlayerProfile：仓库 + 成长 + 战备 BD）——跨局保留、死亡不清；
    /// 由 GameBootstrap 在启动时从存档载入，改动即时落盘（SaveFile）。
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public static PlayerProfile Profile { get; set; }

        public static void Show()
        {
            if (Profile == null)
                Profile = PlayerProfile.NewGame();

            var go = new GameObject("MainMenuRoot");
            GameBootstrap.Track(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<GraphicRaycaster>();
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var menu = go.AddComponent<MainMenu>();
            menu.Build();
        }

        private void Build()
        {
            // 背景：近乎纯黑的"关机主机"
            var bgGo = new GameObject("Bg");
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.SetParent(transform, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 1f);

            var titleRect = UiFactory.Rect("Title", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, -110f), new Vector2(800, 90));
            UiFactory.Label(titleRect, "CRogue", UiFonts.Code, 60, new Color(0.35f, 0.85f, 1f));

            string[] bootLines =
            {
                "[boot] memory check ........ OK",
                "[boot] mount /dev/rogue .... OK",
                "[boot] shell ready —— waiting for input",
            };
            float y = -252f;
            foreach (string line in bootLines)
            {
                var rect = UiFactory.Rect("Boot", transform,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(224f, y), new Vector2(760, 20));
                UiFactory.Label(rect, line, UiFonts.Code, 13, new Color(0.40f, 0.44f, 0.50f));
                y -= 24f;
            }

            // 主按钮列
            MakeButton("启动进程", "选择目标主机，开始回收行动", new Color(0.30f, 0.55f, 0.80f), -386f, OpenMapSelect);
            MakeButton("构 建", "代码仓库 · 语块合成", new Color(0.50f, 0.52f, 0.58f), -474f, OpenBuild);
            MakeButton("词法树", "技能树 · 尚未挂载", new Color(0.17f, 0.18f, 0.20f), -562f, null, enabled: false);
            MakeButton("怪物图鉴", "废弃主机的进程表", new Color(0.58f, 0.47f, 0.85f), -650f, OpenCodex);
            MakeButton("退出进程", "exit(0)", new Color(0.45f, 0.30f, 0.30f), -738f, Quit);

            var footRect = UiFactory.Rect("Footer", transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(224f, 14f), new Vector2(900, 20));
            UiFactory.Label(footRect, "v0.2 演示版 · 存档自动保存（persistentDataPath/save.json）",
                UiFonts.Code, 12, new Color(0.35f, 0.38f, 0.44f));
        }

        private void MakeButton(string title, string desc, Color tint, float y, Action action, bool enabled = true)
        {
            RectTransform rect = UiFactory.Rect(title, transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, y), new Vector2(430, 64));
            rect.gameObject.AddComponent<Image>().color = enabled ? tint : new Color(0.14f, 0.15f, 0.17f, 0.95f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.interactable = enabled;

            var titleRect = UiFactory.Rect("T", rect,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(400, 26));
            UiFactory.Label(titleRect, title, UiFonts.Text, 19,
                enabled ? Color.black : new Color(0.42f, 0.44f, 0.48f));

            var descRect = UiFactory.Rect("D", rect,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -38f), new Vector2(400, 20));
            UiFactory.Label(descRect, desc, UiFonts.Text, 13,
                enabled ? new Color(0.15f, 0.15f, 0.17f) : new Color(0.36f, 0.38f, 0.43f));

            if (enabled && action != null)
                button.onClick.AddListener(() => action());
        }

        // —— 子页面 ——

        private void OpenMapSelect()
        {
            MapSelectPanel.Create(transform);
        }

        private void OpenBuild()
        {
            BuildPanel.Create(transform, Profile);
        }

        private void OpenCodex()
        {
            CodexPanel panel = null;
            panel = CodexPanel.Create(transform, () => Destroy(panel.gameObject), "返回主菜单");
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
