using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 左上角 HUD：状态行 + 事件行 + Routine 源码逐行显示。
    /// 高亮由 TickDriver 的步骤驱动：黄 = 正在执行，灰 = 被优化掉，红 = 卡死。
    /// </summary>
    public sealed class RoutineHud : MonoBehaviour
    {
        private const float RowHeight = 24f;
        private const float RowWidth = 620f;

        private RectTransform _panel;
        private Text _status;
        private Text _event;

        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<Block, Row> _rowOf = new Dictionary<Block, Row>();
        private Row _current;

        private struct Row
        {
            public RectTransform Rect;
            public Image Bg;
            public Text Text;
            public Color BaseText;
            public Block Statement;
        }

        private void Awake()
        {
            _panel = UiFactory.Rect("HudPanel", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(RowWidth + 16f, 120f));
            _panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var statusRect = UiFactory.Rect("Status", _panel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(RowWidth, 22f));
            _status = UiFactory.Label(statusRect, "Tick 0", UiFonts.Code, 15, new Color(0.75f, 0.82f, 0.95f));

            var eventRect = UiFactory.Rect("Event", _panel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -28f), new Vector2(RowWidth, 20f));
            _event = UiFactory.Label(eventRect, "", UiFonts.Text, 14, new Color(0.7f, 0.75f, 0.8f));
        }

        /// <summary>按 Routine 重建源码行（ Routine 被编辑替换后调用）。</summary>
        public void Rebuild(Routine routine)
        {
            foreach (Row row in _rows)
            {
                if (row.Rect != null)
                    Destroy(row.Rect.gameObject);
            }
            _rows.Clear();
            _rowOf.Clear();
            _current = default(Row);

            var lines = SourcePrinter.Print(routine);
            float y = -52f;
            foreach (SourceLine line in lines)
            {
                Row row = MakeRow(line, y);
                _rows.Add(row);
                if (line.Statement != null && !_rowOf.ContainsKey(line.Statement))
                    _rowOf[line.Statement] = row;
                y -= RowHeight;
            }

            float height = 60f + lines.Count * RowHeight;
            _panel.sizeDelta = new Vector2(RowWidth + 16f, height);
        }

        private Row MakeRow(SourceLine line, float y)
        {
            string indent = new string(' ', line.Indent * 4);
            int number = _rows.Count + 1;
            RectTransform rect = UiFactory.Rect("Line", _panel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, y), new Vector2(RowWidth, RowHeight - 2f));
            Image bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            Text text = UiFactory.Label(rect, $"{number,3} | {indent}{line.Text}", UiFonts.Code, 15, CodeColors.BaseText(line.Statement));
            return new Row { Rect = rect, Bg = bg, Text = text, BaseText = CodeColors.BaseText(line.Statement), Statement = line.Statement };
        }

        /// <summary>TickDriver 每拉一步调用：高亮当前语句。</summary>
        public void ApplyStep(StepInfo step)
        {
            if (_current.Rect != null)
                ResetRow(_current);

            Row row;
            if (step == null || step.Statement == null || !_rowOf.TryGetValue(step.Statement, out row))
                return;

            switch (step.Status)
            {
                case StepStatus.Executed:
                    row.Bg.color = CodeColors.ExecutedBg;
                    row.Text.color = CodeColors.ExecutedText;
                    row.Text.fontStyle = FontStyle.Bold;
                    break;
                case StepStatus.OptimizedOut:
                    row.Bg.color = CodeColors.OptimizedBg;
                    row.Text.color = CodeColors.OptimizedText;
                    row.Text.fontStyle = FontStyle.Normal;
                    break;
                case StepStatus.Hung:
                    row.Bg.color = CodeColors.HungBg;
                    row.Text.color = CodeColors.HungText;
                    row.Text.fontStyle = FontStyle.Bold;
                    break;
            }
            _current = row;
        }

        /// <summary>tick 结束时清除高亮。</summary>
        public void ClearHighlight()
        {
            if (_current.Rect != null)
                ResetRow(_current);
            _current = default(Row);
        }

        private void ResetRow(Row row)
        {
            row.Bg.color = new Color(0f, 0f, 0f, 0.35f);
            row.Text.color = row.BaseText;
            row.Text.fontStyle = FontStyle.Normal;
        }

        public void SetStatus(string s)
        {
            _status.text = s;
        }

        public void Log(string s)
        {
            _event.text = s;
        }
    }
}
