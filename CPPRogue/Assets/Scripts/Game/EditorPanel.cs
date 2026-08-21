using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 暂停时的拼装编辑面板（DemoUI 拖拽编辑的 Unity 版）：
    /// 左侧语法块面板拖入右侧代码缝隙；已有行拖动换位；右键删除；双击改参数。
    /// 所有编辑操作走 Core 的 RoutineEditor（防呆：行数上限 / 子树规则）。
    /// </summary>
    public sealed class EditorPanel : MonoBehaviour
    {
        private const float LineHeight = 26f;
        private const float GapHeight = 12f;
        private const float CodeWidth = 600f;

        private RoutineEditor _editor;
        private RectTransform _canvasRect;
        private RectTransform _codeArea;
        private Text _countLabel;
        private Text _feedback;

        private readonly List<GapRow> _gaps = new List<GapRow>();

        private struct GapRow
        {
            public Slot Slot;
            public Image Bg;
        }

        private struct DragState
        {
            public Block NewBlock;     // FromPalette 时有效
            public Slot? Source;       // 移动已有语句时有效
            public string Title;
            public RectTransform Ghost;
        }

        private DragState _drag;
        private float _y;

        public static EditorPanel Create(RoutineEditor editor, Transform canvasTransform)
        {
            var go = new GameObject("EditorPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.86f);

            var panel = go.AddComponent<EditorPanel>();
            panel._editor = editor;
            panel._canvasRect = (RectTransform)canvasTransform;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var titleRect = UiFactory.Rect("Title", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1200, 30));
            UiFactory.Label(titleRect, "已暂停 —— 拼装你的 Routine（按 ESC 继续）",
                UiFonts.Text, 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            // 左：语法块面板
            var paletteRect = UiFactory.Rect("Palette", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -64f), new Vector2(310, 640));
            paletteRect.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.17f, 0.95f);
            float y = -6f;
            foreach (PaletteEntry entry in GamePalette.Items)
            {
                var rowRect = UiFactory.Rect("Item", paletteRect,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, y), new Vector2(298, 32));
                rowRect.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.22f, 0.27f);
                UiFactory.Label(rowRect, entry.Title, UiFonts.Code, 15, new Color(0.85f, 0.88f, 0.95f));
                var drag = rowRect.gameObject.AddComponent<PaletteDrag>();
                drag.Panel = this;
                drag.Entry = entry;
                y -= 38f;
            }

            // 右：代码拼装区
            var codeRect = UiFactory.Rect("Code", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(360f, -64f), new Vector2(CodeWidth + 20, 640));
            codeRect.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.11f, 0.97f);
            _codeArea = codeRect;

            var countRect = UiFactory.Rect("Count", codeRect,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 6f), new Vector2(300, 22));
            _countLabel = UiFactory.Label(countRect, "", UiFonts.Text, 15, new Color(0.6f, 0.75f, 0.6f));

            var feedbackRect = UiFactory.Rect("Feedback", codeRect,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 6f), new Vector2(360, 22));
            _feedback = UiFactory.Label(feedbackRect, "", UiFonts.Text, 15, new Color(0.9f, 0.75f, 0.4f), TextAnchor.MiddleRight);

            // 底部操作提示
            var hintRect = UiFactory.Rect("Hint", transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1200, 26));
            UiFactory.Label(hintRect, "左键拖动 = 插入/移动　　右键点击 = 删除　　双击 = 改参数　　ESC = 继续游戏",
                UiFonts.Text, 16, new Color(0.65f, 0.65f, 0.7f), TextAnchor.MiddleCenter);

            RefreshCode();
        }

        // ---------- 代码区渲染（行 + 缝隙） ----------

        private void RefreshCode()
        {
            for (int i = _codeArea.childCount - 1; i >= 0; i--)
            {
                Transform child = _codeArea.GetChild(i);
                if (child.name != "Count" && child.name != "Feedback")
                    Destroy(child.gameObject);
            }
            _gaps.Clear();
            _y = -6f;
            RenderBody(_editor.Root, null, 0, 0);
            _countLabel.text = $"行数 {_editor.StatementCount}/{_editor.MaxLines}";
        }

        private void RenderBody(Block[] body, Block owner, int branch, int depth)
        {
            for (int i = 0; i <= body.Length; i++)
            {
                AddGap(new Slot(owner, branch, i), depth);
                if (i < body.Length)
                    RenderStatement(body[i], depth, new Slot(owner, branch, i));
            }
        }

        private void RenderStatement(Block s, int depth, Slot slot)
        {
            switch (s.Kind)
            {
                case BlockKind.If:
                    AddLine(SourcePrinter.HeaderText(s), s, depth, slot);
                    RenderBody(s.Body, s, 0, depth + 1);
                    if (s.ElseBody.Length > 0)
                    {
                        AddLine("} else {", null, depth, null);
                        RenderBody(s.ElseBody, s, 1, depth + 1);
                    }
                    AddLine("}", null, depth, null);
                    break;
                case BlockKind.For:
                case BlockKind.While:
                    AddLine(SourcePrinter.HeaderText(s), s, depth, slot);
                    RenderBody(s.Body, s, 0, depth + 1);
                    AddLine("}", null, depth, null);
                    break;
                default:
                    AddLine(SourcePrinter.StatementText(s), s, depth, slot);
                    break;
            }
        }

        private void AddGap(Slot slot, int depth)
        {
            RectTransform rect = UiFactory.Rect("Gap", _codeArea,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, _y), new Vector2(CodeWidth - 46, GapHeight - 2f));
            _y -= GapHeight;
            Image bg = rect.gameObject.AddComponent<Image>();
            bg.color = CodeColors.GapIdle;
            var zone = rect.gameObject.AddComponent<GapZone>();
            zone.Slot = slot;
            _gaps.Add(new GapRow { Slot = slot, Bg = bg });
        }

        private void AddLine(string text, Block statement, int depth, Slot? slot)
        {
            string indent = new string(' ', depth * 4);
            RectTransform rect = UiFactory.Rect("Line", _codeArea,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, _y), new Vector2(CodeWidth - 46, LineHeight - 2f));
            _y -= LineHeight;
            rect.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 0.9f);
            UiFactory.Label(rect, $"{indent}{text}", UiFonts.Code, 15, CodeColors.BaseText(statement));

            if (statement != null && slot.HasValue)
            {
                var drag = rect.gameObject.AddComponent<LineDrag>();
                drag.Panel = this;
                drag.Statement = statement;
                drag.Source = slot.Value;
                drag.Title = text;
                var click = rect.gameObject.AddComponent<LineClick>();
                click.Panel = this;
                click.Statement = statement;
                click.Slot = slot.Value;
            }
        }

        // ---------- 拖拽流程 ----------

        public void BeginDrag(Block newBlock, Slot? source, string title)
        {
            if (_drag.Ghost != null)
                Destroy(_drag.Ghost.gameObject);

            var ghost = UiFactory.Rect("Ghost", _canvasRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 30));
            Image img = ghost.gameObject.AddComponent<Image>();
            img.color = new Color(0.16f, 0.45f, 0.7f, 0.9f);
            img.raycastTarget = false;
            UiFactory.Label(ghost, title, UiFonts.Code, 15, Color.white, TextAnchor.MiddleCenter);
            ghost.SetAsLastSibling();

            _drag = new DragState { NewBlock = newBlock, Source = source, Title = title, Ghost = ghost };
        }

        public void UpdateDrag(PointerEventData e)
        {
            if (_drag.Ghost == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, e.position, e.pressEventCamera, out Vector2 local))
                _drag.Ghost.anchoredPosition = local;
            RecolorGaps(e);
        }

        public void EndDrag(PointerEventData e)
        {
            try
            {
                GapZone zone = GapUnder(e);
                if (zone != null)
                {
                    if (_drag.Source.HasValue)
                    {
                        if (_editor.CanMove(_drag.Source.Value, zone.Slot))
                        {
                            _editor.Move(_drag.Source.Value, zone.Slot);
                            RefreshCode();
                            Feedback($"已移动：{_drag.Title}");
                        }
                        else
                        {
                            Feedback("移动被拒绝（目标在自己的子树内或位置无效）");
                        }
                    }
                    else
                    {
                        if (_editor.CanInsert(zone.Slot, _drag.NewBlock))
                        {
                            _editor.Insert(zone.Slot, _drag.NewBlock);
                            RefreshCode();
                            Feedback($"已插入：{_drag.Title}");
                        }
                        else
                        {
                            Feedback("放不下：超过行数上限");
                        }
                    }
                }
            }
            finally
            {
                if (_drag.Ghost != null)
                    Destroy(_drag.Ghost.gameObject);
                _drag = default(DragState);
                ResetGapColors();
            }
        }

        private void RecolorGaps(PointerEventData e)
        {
            GapZone zone = GapUnder(e);
            foreach (GapRow gap in _gaps)
            {
                bool isTarget = zone != null && SameSlot(gap.Slot, zone.Slot);
                if (!isTarget)
                {
                    gap.Bg.color = CodeColors.GapIdle;
                }
                else
                {
                    bool ok = _drag.Source.HasValue
                        ? _editor.CanMove(_drag.Source.Value, gap.Slot)
                        : _editor.CanInsert(gap.Slot, _drag.NewBlock);
                    gap.Bg.color = ok ? CodeColors.GapOk : CodeColors.GapBad;
                }
            }
        }

        private void ResetGapColors()
        {
            foreach (GapRow gap in _gaps)
                gap.Bg.color = CodeColors.GapIdle;
        }

        private static bool SameSlot(Slot a, Slot b)
        {
            return ReferenceEquals(a.Owner, b.Owner) && a.Branch == b.Branch && a.Index == b.Index;
        }

        private static GapZone GapUnder(PointerEventData e)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(e, results);
            foreach (RaycastResult r in results)
            {
                GapZone zone = r.gameObject.GetComponent<GapZone>();
                if (zone != null)
                    return zone;
            }
            return null;
        }

        // ---------- 删除 / 参数编辑 ----------

        public void Delete(Slot slot)
        {
            if (_editor.CanRemove(slot))
            {
                _editor.Remove(slot);
                RefreshCode();
                Feedback("已删除语句（含其子语句）");
            }
        }

        public void EditParams(Block statement)
        {
            Expr lit;
            string title;
            if (!TryGetEditableLiteral(statement, out lit, out title))
            {
                Feedback("这条语句没有可编辑的数字参数");
                return;
            }
            ParamDialog.Show(transform, $"编辑参数：{title}", lit.Literal.AsNumber(), v =>
            {
                lit.Literal = Value.Of(v);
                RefreshCode();
                Feedback($"参数已改为 {v:0.###}");
            });
        }

        private static bool TryGetEditableLiteral(Block s, out Expr lit, out string title)
        {
            lit = null;
            title = null;
            switch (s.Kind)
            {
                case BlockKind.Call:
                    if (s.Args.Length == 1 && s.Args[0].Kind == ExprKind.Literal)
                    {
                        lit = s.Args[0];
                        title = $"{s.CallName}(n) 的 n";
                        return true;
                    }
                    return false;
                case BlockKind.Assign:
                    if (s.ValueExpr != null && s.ValueExpr.Kind == ExprKind.Literal)
                    {
                        lit = s.ValueExpr;
                        title = $"{s.Target} = 值";
                        return true;
                    }
                    return false;
                case BlockKind.If:
                case BlockKind.While:
                case BlockKind.For:
                    if (s.Condition != null && s.Condition.Kind == ExprKind.Binary
                        && s.Condition.Right != null && s.Condition.Right.Kind == ExprKind.Literal)
                    {
                        lit = s.Condition.Right;
                        title = s.Kind == BlockKind.For ? "循环上限" : "条件阈值";
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private void Feedback(string message)
        {
            _feedback.text = message;
        }
    }

    // ---------- 拖拽/点击的小组件（同文件，避免碎片化） ----------

    internal sealed class GapZone : MonoBehaviour
    {
        public Slot Slot;
    }

    internal sealed class PaletteDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public EditorPanel Panel;
        public PaletteEntry Entry;

        public void OnBeginDrag(PointerEventData e)
        {
            Panel.BeginDrag(Entry.Make(), null, Entry.Title);
        }

        public void OnDrag(PointerEventData e)
        {
            Panel.UpdateDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            Panel.EndDrag(e);
        }
    }

    internal sealed class LineDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public EditorPanel Panel;
        public Block Statement;
        public Slot Source;
        public string Title;

        public void OnBeginDrag(PointerEventData e)
        {
            Panel.BeginDrag(null, Source, Title);
        }

        public void OnDrag(PointerEventData e)
        {
            Panel.UpdateDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            Panel.EndDrag(e);
        }
    }

    internal sealed class LineClick : MonoBehaviour, IPointerClickHandler
    {
        public EditorPanel Panel;
        public Block Statement;
        public Slot Slot;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                Panel.Delete(Slot);
            }
            else if (e.clickCount >= 2)
            {
                Panel.EditParams(Statement);
            }
        }
    }
}
