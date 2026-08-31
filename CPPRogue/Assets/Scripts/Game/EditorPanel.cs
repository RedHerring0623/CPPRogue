using System;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>编辑面板的可配置项：正常 BD（仓库语块 + 校验 + 落盘）与测试 BD（全语句自由）共用这块 UI。</summary>
    public sealed class EditorPanelOptions
    {
        public string Title = "已暂停 —— 拼装你的 Routine（按 ESC 继续）";
        public string Hint = "左键拖动 = 插入/移动　　右键点击 = 删除　　双击 = 改参数　　ESC = 继续游戏";

        /// <summary>可拖入的语块清单；null = GamePalette 全部语句（测试 BD）。</summary>
        public IList<PaletteEntry> Palette;

        /// <summary>插入校验（正常 BD：语块用量 ≤ 仓库持有）；null = 不限。</summary>
        public Func<Block, bool> CanInsert;

        /// <summary>每次成功编辑（插入/移动/删除）后的回调（正常 BD 落盘用）。</summary>
        public Action Changed;

        /// <summary>false = 嵌入构建页，不压暗全屏。</summary>
        public bool Dim = true;

        /// <summary>代码拼装区宽度（默认 600；嵌入构建页的宽区域可加大）。</summary>
        public float CodeWidth = 600f;
    }

    /// <summary>
    /// 拼装编辑面板（DemoUI 拖拽编辑的 Unity 版）：
    /// 左侧语法块面板拖入右侧代码缝隙；已有行拖动换位；右键删除；双击改参数。
    /// 所有编辑操作走 Core 的 RoutineEditor（防呆：行数上限 / 子树规则）；
    /// 正常 BD（仓库语块受限 + 落盘）与测试 BD（全语句）靠 EditorPanelOptions 区分。
    /// </summary>
    public sealed class EditorPanel : MonoBehaviour
    {
        private const float LineHeight = 26f;
        private const float GapHeight = 12f;

        private RoutineEditor _editor;
        private EditorPanelOptions _options;
        private RectTransform _canvasRect;
        private RectTransform _codeArea;
        private Text _countLabel;
        private Text _feedback;

        public RoutineEditor Editor => _editor;

        private readonly List<GapRow> _gaps = new List<GapRow>();

        private struct GapRow
        {
            public Slot Slot;
            public Image Bg;
        }

        private struct PaletteRow
        {
            public PaletteEntry Entry;
            public Image Bg;
            public Text Label;
            public PaletteDrag Drag;
        }

        private readonly List<PaletteRow> _paletteRows = new List<PaletteRow>();

        private struct DragState
        {
            public Block NewBlock;     // FromPalette 时有效
            public Slot? Source;       // 移动已有语句时有效
            public string Title;
            public RectTransform Ghost;
        }

        private DragState _drag;
        private float _y;

        public static EditorPanel Create(RoutineEditor editor, Transform canvasTransform, EditorPanelOptions options = null)
        {
            var go = new GameObject("EditorPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (options == null || options.Dim)
                go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.86f);

            var panel = go.AddComponent<EditorPanel>();
            panel._editor = editor;
            panel._options = options ?? new EditorPanelOptions();
            panel._canvasRect = (RectTransform)canvasTransform;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var titleRect = UiFactory.Rect("Title", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1200, 30));
            UiFactory.Label(titleRect, _options.Title, UiFonts.Text, 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            // 左：语法块面板（590 高 = 容纳 18 种语块条目且不压底部提示行；26/32 行距）
            var paletteRect = UiFactory.Rect("Palette", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -64f), new Vector2(310, 590));
            paletteRect.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.17f, 0.95f);
            float y = -6f;
            IList<PaletteEntry> palette = _options.Palette ?? GamePalette.Items;
            foreach (PaletteEntry entry in palette)
            {
                var rowRect = UiFactory.Rect("Item", paletteRect,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, y), new Vector2(298, 26));
                Image rowBg = rowRect.gameObject.AddComponent<Image>();
                Text rowLabel = UiFactory.Label(rowRect, entry.Title, UiFonts.Code, 14, new Color(0.85f, 0.88f, 0.95f));
                var drag = rowRect.gameObject.AddComponent<PaletteDrag>();
                drag.Panel = this;
                drag.Entry = entry;
                _paletteRows.Add(new PaletteRow { Entry = entry, Bg = rowBg, Label = rowLabel, Drag = drag });
                y -= 32f;
            }

            // 右：代码拼装区
            var codeRect = UiFactory.Rect("Code", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(360f, -64f), new Vector2(_options.CodeWidth + 20f, 590));
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
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(1200, 22));
            UiFactory.Label(hintRect, _options.Hint, UiFonts.Text, 14,
                new Color(0.65f, 0.65f, 0.7f), TextAnchor.MiddleCenter);

            RefreshPalette();
            RefreshCode();
        }

        /// <summary>正常 BD：条目标注剩余数（持有 − 已用，随插入/删除实时变化）；×0 灰化禁拖。</summary>
        private void RefreshPalette()
        {
            foreach (PaletteRow row in _paletteRows)
            {
                if (row.Entry.Remaining == null)
                    continue;
                int left = row.Entry.Remaining().GetValueOrDefault();
                row.Label.text = $"{row.Entry.Title}  ×{left}";
                bool usable = left > 0;
                row.Bg.color = usable ? new Color(0.20f, 0.22f, 0.27f) : new Color(0.13f, 0.14f, 0.16f);
                row.Label.color = usable ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.42f, 0.44f, 0.48f);
                row.Drag.enabled = usable;
            }
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
            RefreshPalette();
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
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, _y), new Vector2(_options.CodeWidth - 46f, GapHeight - 2f));
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
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, _y), new Vector2(_options.CodeWidth - 46f, LineHeight - 2f));
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

            // anchor/pivot 对齐父 rect 的 pivot：ScreenPointToLocal 的局部原点在父 pivot，
            // 这样 anchoredPosition 才能和鼠标局部坐标同一原点（父是左上 pivot 的嵌入区时才不会飘走）
            var ghost = UiFactory.Rect("Ghost", _canvasRect,
                new Vector2(_canvasRect.pivot.x, _canvasRect.pivot.y),
                new Vector2(_canvasRect.pivot.x, _canvasRect.pivot.y),
                Vector2.zero, new Vector2(260, 30));
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
                _drag.Ghost.anchoredPosition = local + new Vector2(14f, -14f);   // 跟标显示在鼠标右下角
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
                            NotifyChanged();
                        }
                        else
                        {
                            Feedback("移动被拒绝（目标在自己的子树内或位置无效）");
                        }
                    }
                    else
                    {
                        bool fits = _editor.CanInsert(zone.Slot, _drag.NewBlock);
                        bool allowed = fits && CanUse(_drag.NewBlock);
                        if (allowed)
                        {
                            _editor.Insert(zone.Slot, _drag.NewBlock);
                            RefreshCode();
                            Feedback($"已插入：{_drag.Title}");
                            NotifyChanged();
                        }
                        else if (!fits)
                        {
                            Feedback("放不下：超过行数上限");
                        }
                        else
                        {
                            Feedback("正常 BD 限制：语块用量超过仓库持有，或该语句不在仓库中");
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

        /// <summary>插入校验钩子（正常 BD 用）；测试 BD（无钩子）恒通过。</summary>
        private bool CanUse(Block block)
        {
            return _options.CanInsert == null || _options.CanInsert(block);
        }

        private void NotifyChanged()
        {
            if (_options.Changed != null)
                _options.Changed();
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
                        : _editor.CanInsert(gap.Slot, _drag.NewBlock) && CanUse(_drag.NewBlock);
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
                NotifyChanged();
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
