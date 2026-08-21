using CPPRogue.Core.Code;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// ESC 暂停/继续：暂停时冻结世界（timeScale = 0）并显示拼装编辑面板；
    /// 继续时用编辑结果热替换 Routine（变量黑板保留，tick 从头开始）。
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        private RoutineEditor _editor;
        private TickDriver _driver;
        private RoutineHud _hud;
        private EditorPanel _panel;

        public void Setup(RoutineEditor editor, TickDriver driver, RoutineHud hud)
        {
            _editor = editor;
            _driver = driver;
            _hud = hud;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Toggle();
        }

        private void Toggle()
        {
            if (_panel == null)
            {
                Time.timeScale = 0f;
                _panel = EditorPanel.Create(_editor, transform);
            }
            else
            {
                Time.timeScale = 1f;
                Routine routine = _editor.BuildRoutine();
                _driver.ReplaceRoutine(routine);
                _hud.Rebuild(routine);
                Destroy(_panel.gameObject);
                _panel = null;
            }
        }
    }
}
