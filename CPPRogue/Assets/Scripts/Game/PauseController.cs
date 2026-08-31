using System;
using CPPRogue.Core.Code;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// ESC 暂停入口。第一次 ESC 弹出主菜单；编辑器或图鉴打开时再按 ESC = 关闭并继续。
    /// 测试地图有**两套 BD**：
    /// - 正常 BD：只能用仓库语块（用量 ≤ 持有），改动即时存档、热更新生效——正式地图也走这套；
    /// - 测试 BD：全语句自由拼，不存档，随局消失——只有测试地图有。
    /// 局已结束（GameRun.Over，死亡/撤离弹窗在场）时 ESC 失效。
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        private RoutineEditor _normalEditor;
        private RoutineEditor _testEditor;      // null = 正式地图（只有正常 BD）
        private TickDriver _driver;
        private RoutineHud _hud;
        private RectTransform _menu;
        private RectTransform _confirm;
        private EditorPanel _editorPanel;
        private CodexPanel _codexPanel;
        private RoutineEditor _active;          // 当前驱动运行 Routine 的 BD（默认正常 BD）

        public void Setup(RoutineEditor normalEditor, RoutineEditor testEditor, TickDriver driver, RoutineHud hud)
        {
            _normalEditor = normalEditor;
            _testEditor = testEditor;
            _driver = driver;
            _hud = hud;
            _active = normalEditor;
        }

        private void Update()
        {
            if (GameRun.Over)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
                Toggle();
        }

        private void Toggle()
        {
            if (_confirm != null)
            {
                CancelQuit();   // 确认弹窗开着：ESC 视为取消
                return;
            }
            if (_editorPanel != null)
            {
                CloseEditor();
                Resume();
                return;
            }
            if (_codexPanel != null)
            {
                CloseCodex();
                Resume();
                return;
            }
            if (_menu != null)
            {
                CloseMenu();
                Resume();
                return;
            }
            Time.timeScale = 0f;
            _menu = PauseMenu.Create(this, transform, _testEditor != null);
        }

        // —— 主菜单按钮回调 ——

        public void ContinueFromMenu()
        {
            CloseMenu();
            Resume();
        }

        public void OpenNormalEditor()
        {
            CloseMenu();
            _active = _normalEditor;
            _editorPanel = EditorPanel.Create(_normalEditor, transform, new EditorPanelOptions
            {
                Title = "正常 BD —— 只能用仓库语块（改动即时存档）",
                Palette = FragmentPalette.Build(MainMenu.Profile.Codebase, _normalEditor),
                CanInsert = FragmentPalette.BuildCanInsert(MainMenu.Profile.Codebase, _normalEditor),
                Changed = PersistNormalBd,
            });
        }

        public void OpenTestEditor()
        {
            CloseMenu();
            _active = _testEditor;
            _editorPanel = EditorPanel.Create(_testEditor, transform, new EditorPanelOptions
            {
                Title = "测试 BD —— 全语句自由拼（不存档，随局消失）",
            });
        }

        /// <summary>正常 BD 落盘（热更新）：每次编辑后校验并写回档案。</summary>
        private void PersistNormalBd()
        {
            if (FragmentPalette.Persist(_normalEditor, MainMenu.Profile, out string error))
                return;
            if (_hud != null)
                _hud.Log($"正常 BD 未存档：{error}");
        }

        public void OpenCodexFromMenu()
        {
            CloseMenu();
            _codexPanel = CodexPanel.Create(transform, CloseCodexAndResume);
        }

        /// <summary>「返回主菜单」：关掉暂停菜单，弹确认框（本局所得不保存）。</summary>
        public void AskQuitToMenu()
        {
            CloseMenu();
            _confirm = ConfirmBox.Create(this, transform);
        }

        public void ConfirmQuit()
        {
            CloseConfirm();
            GameBootstrap.QuitToMenu();   // 本控制器随局一起被拆
        }

        public void CancelQuit()
        {
            CloseConfirm();
            _menu = PauseMenu.Create(this, transform, _testEditor != null);   // 回到暂停菜单
        }

        private void CloseCodexAndResume()
        {
            CloseCodex();
            Resume();
        }

        private void Resume()
        {
            Time.timeScale = 1f;
            Routine routine = _active.BuildRoutine();
            _driver.ReplaceRoutine(routine);
            _hud.Rebuild(routine);
        }

        private void CloseMenu()
        {
            if (_menu != null)
            {
                Destroy(_menu.gameObject);
                _menu = null;
            }
        }

        private void CloseConfirm()
        {
            if (_confirm != null)
            {
                Destroy(_confirm.gameObject);
                _confirm = null;
            }
        }

        private void CloseEditor()
        {
            if (_editorPanel != null)
            {
                Destroy(_editorPanel.gameObject);
                _editorPanel = null;
            }
        }

        private void CloseCodex()
        {
            if (_codexPanel != null)
            {
                Destroy(_codexPanel.gameObject);
                _codexPanel = null;
            }
        }
    }

    /// <summary>ESC 主菜单：全屏压暗 + 居中卡片按钮（测试地图多一个"测试 BD"入口）。</summary>
    internal sealed class PauseMenu : MonoBehaviour
    {
        public static RectTransform Create(PauseController owner, Transform canvasTransform, bool showTestEditor)
        {
            var go = new GameObject("PauseMenu");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            var menu = go.AddComponent<PauseMenu>();
            menu.Build(owner, showTestEditor);
            rect.SetAsLastSibling();   // 保证压在其它 UI 之上接收点击
            return rect;
        }

        private void Build(PauseController owner, bool showTestEditor)
        {
            float boxHeight = showTestEditor ? 436f : 376f;
            var box = UiFactory.Rect("Box", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, boxHeight));
            box.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.14f, 0.16f, 0.98f);

            var titleRect = UiFactory.Rect("Title", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(340, 28));
            UiFactory.Label(titleRect, "已暂停 —— 主机时间已冻结", UiFonts.Text, 19,
                new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            // 按钮挂在卡片下、以卡片顶边为基准向下排——不会跑出卡片，也不和提示文字重叠
            float y = -62f;
            y = MakeButton(box, "继续游戏", new Color(0.30f, 0.55f, 0.80f), y, owner.ContinueFromMenu);
            y = MakeButton(box, "编辑 BD · 正常", new Color(0.50f, 0.52f, 0.58f), y, owner.OpenNormalEditor);
            if (showTestEditor)
                y = MakeButton(box, "编辑 BD · 测试", new Color(0.46f, 0.42f, 0.55f), y, owner.OpenTestEditor);
            y = MakeButton(box, "怪物图鉴", new Color(0.58f, 0.47f, 0.85f), y, owner.OpenCodexFromMenu);
            MakeButton(box, "返回主菜单", new Color(0.45f, 0.30f, 0.30f), y, owner.AskQuitToMenu);

            var hintRect = UiFactory.Rect("Hint", box,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(340, 22));
            UiFactory.Label(hintRect, "ESC = 继续", UiFonts.Text, 14,
                new Color(0.6f, 0.62f, 0.68f), TextAnchor.MiddleCenter);
        }

        /// <summary>自顶向下排一个按钮，返回下一个按钮的 y。</summary>
        private static float MakeButton(RectTransform parent, string label, Color tint, float y, Action action)
        {
            RectTransform rect = UiFactory.Rect(label, parent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(290, 50));
            UiFactory.SolidButton(rect, label, tint, Color.black, UiFonts.Text, 17).onClick.AddListener(() => action());
            return y - 64f;
        }
    }

    /// <summary>退回主菜单的确认弹窗：确认 / 取消；确认后本局所得不保存（搜打撤规则）。</summary>
    internal sealed class ConfirmBox : MonoBehaviour
    {
        public static RectTransform Create(PauseController owner, Transform canvasTransform)
        {
            var go = new GameObject("QuitConfirmBox");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var box = UiFactory.Rect("Box", rect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 230));
            box.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.13f, 0.12f, 0.98f);

            var askRect = UiFactory.Rect("Ask", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440, 30));
            UiFactory.Label(askRect, "确认退回到主菜单吗？", UiFonts.Text, 20,
                new Color(0.95f, 0.95f, 1f), TextAnchor.MiddleCenter);

            var warnRect = UiFactory.Rect("Warn", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(440, 26));
            UiFactory.Label(warnRect, "游戏内掉落不会保存！", UiFonts.Text, 17,
                new Color(1f, 0.55f, 0.4f), TextAnchor.MiddleCenter);

            var okRect = UiFactory.Rect("Ok", box,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-125f, -75f), new Vector2(220, 46));
            UiFactory.SolidButton(okRect, "确认返回", new Color(0.5f, 0.32f, 0.32f), Color.black, UiFonts.Text, 17)
                .onClick.AddListener(owner.ConfirmQuit);

            var cancelRect = UiFactory.Rect("Cancel", box,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(125f, -75f), new Vector2(220, 46));
            UiFactory.SolidButton(cancelRect, "取消", new Color(0.35f, 0.38f, 0.44f), Color.white, UiFonts.Text, 17)
                .onClick.AddListener(owner.CancelQuit);

            rect.SetAsLastSibling();
            return rect;
        }
    }
}
