using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;
using CPPRogue.Core.Combat;
using CPPRogue.Core.Computing;

namespace CPPRogue.DemoUI
{
    /// <summary>
    /// 代码执行可视化 + 拼装编辑 Demo：
    /// 左上面板拖语法块、左下源码区拼 Routine（缝隙投放/拖动换位/右键删除/双击改参数），
    /// 拼完直接 ▶ 运行看逐句执行。
    /// 调度规则：下一 Tick = max(本 Tick 开始 + TickInterval, 程序跑完时刻)。
    /// </summary>
    public sealed class MainForm : Form
    {
        private enum Phase { Idle, Executing, Waiting }

        private sealed class Preset
        {
            public string Name;
            public Block[] Lines;
            public Dictionary<string, Value> Vars;
        }

        private sealed class PaletteItem
        {
            public string Title;
            public Func<Block> Make;
        }

        private sealed class DragPayload
        {
            public bool FromPalette;
            public Block Block;
            public Slot? Source;
            public string Title;
        }

        private static readonly Preset[] Presets =
        {
            new Preset
            {
                Name = "① 基础输出",
                Lines = new[] { Block.Call("attack"), Block.Call("heal", Expr.Num(5)) },
            },
            new Preset
            {
                Name = "② 连击循环 for(i<x)",
                Lines = new[]
                {
                    Block.For("i", Expr.Num(0),
                        Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Var("x")), Expr.Num(1),
                        Block.Call("attack", Expr.Bin(BinaryOp.Mul, Expr.Var("i"), Expr.Num(5)))),
                },
                Vars = new Dictionary<string, Value> { { "x", Value.Of(4) } },
            },
            new Preset
            {
                Name = "③ 保命反击 if(hp<0.5)",
                Lines = new[]
                {
                    Block.If(
                        Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.5)),
                        new[] { Block.Call("heal", Expr.Num(30)), Block.Call("shield", Expr.Num(10)) },
                        new[] { Block.Call("attack"), Block.Call("attack") }),
                },
            },
            new Preset
            {
                Name = "④ 变量叠层 a=a+1",
                Lines = new[]
                {
                    Block.Assign("a", Expr.Bin(BinaryOp.Add, Expr.Var("a"), Expr.Num(1))),
                    Block.If(Expr.Bin(BinaryOp.Ge, Expr.Var("a"), Expr.Num(3)),
                        new[] { Block.Assign("a", Expr.Num(0)), Block.Call("attack", Expr.Num(25)) }),
                    Block.Call("attack", Expr.Num(4)),
                },
            },
            new Preset
            {
                Name = "⑤ 死循环警告 while(true)",
                Lines = new[] { Block.While(null, Block.Call("attack")) },
            },
        };

        private static readonly PaletteItem[] Palette =
        {
            new PaletteItem { Title = "attack()", Make = () => Block.Call("attack") },
            new PaletteItem { Title = "attack(n)", Make = () => Block.Call("attack", Expr.Num(10)) },
            new PaletteItem { Title = "heal(n)", Make = () => Block.Call("heal", Expr.Num(5)) },
            new PaletteItem { Title = "shield(n)", Make = () => Block.Call("shield", Expr.Num(10)) },
            new PaletteItem { Title = "a = 值", Make = () => Block.Assign("a", Expr.Num(1)) },
            new PaletteItem { Title = "if (hp < 0.5)", Make = () => Block.If(
                Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.5)),
                new[] { Block.Call("attack") }) },
            new PaletteItem { Title = "if / else", Make = () => Block.If(
                Expr.Bin(BinaryOp.Lt, Expr.Var("hp"), Expr.Num(0.5)),
                new[] { Block.Call("attack") },
                new[] { Block.Call("heal", Expr.Num(5)) }) },
            new PaletteItem { Title = "for (i < 3)", Make = () => Block.For("i", Expr.Num(0),
                Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)), Expr.Num(1)) },
            new PaletteItem { Title = "while (a < 3)", Make = () => Block.While(
                Expr.Bin(BinaryOp.Lt, Expr.Var("a"), Expr.Num(3))) },
            new PaletteItem { Title = "while (true)", Make = () => Block.While(null) },
            new PaletteItem { Title = "return;", Make = () => Block.Return() },
            new PaletteItem { Title = "break;", Make = () => Block.Break() },
            new PaletteItem { Title = "continue;", Make = () => Block.Continue() },
        };

        private static readonly Color Bg = Color.FromArgb(30, 30, 30);
        private static readonly Color CodeBg = Color.FromArgb(30, 30, 30);
        private static readonly Color PanelBg = Color.FromArgb(37, 37, 38);
        private static readonly Color GapBg = Color.FromArgb(44, 44, 47);
        private static readonly Color GapOk = Color.FromArgb(0, 145, 195);
        private static readonly Color GapBad = Color.FromArgb(120, 40, 40);
        private static readonly Color SelectedBg = Color.FromArgb(60, 70, 90);

        private readonly Interpreter _interp = new Interpreter();
        private readonly Font _codeFont = new Font("Consolas", 12.5F);
        private readonly Font _codeFontBold = new Font("Consolas", 12.5F, FontStyle.Bold);
        private readonly Timer _timer = new Timer { Interval = 50 };

        // 编辑状态
        private RoutineEditor _editor = new RoutineEditor();
        private readonly Dictionary<Block, Label> _stmtLabels = new Dictionary<Block, Label>();
        private readonly Dictionary<Block, Slot> _slotOf = new Dictionary<Block, Slot>();
        private readonly Dictionary<Block, string> _textOf = new Dictionary<Block, string>();
        private Label _selectedLabel;
        private Slot? _selectedSlot;
        private int _lineNo;
        private Point _dragStart;
        private Block _dragBlock;
        private Slot? _dragSlot;
        private ContextMenuStrip _lineMenu;

        // 执行状态
        private Routine _routine;
        private ExecContext _ctx;
        private DemoCombatWorld _world;
        private IEnumerator<StepInfo> _tickEnum;
        private Phase _phase = Phase.Idle;
        private bool _running;
        private DateTime _nextStatementAt;
        private DateTime _tickStartedAt;
        private DateTime _nextTickAt;
        private int _presetIndex;

        // UI 引用
        private FlowLayoutPanel _codePanel;
        private Label _currentLabel;
        private Label _tickLabel, _phaseLabel, _nextLabel, _budgetLabel, _killsLabel;
        private ProgressBar _hpBar, _enemyBar;
        private ListBox _varsList;
        private RichTextBox _logBox;
        private ComboBox _presetCombo;
        private Button _runBtn;
        private NumericUpDown _stmtInterval, _tickInterval;

        private double StatementInterval => (double)_stmtInterval.Value;
        private double TickIntervalSec => (double)_tickInterval.Value;

        public MainForm()
        {
            Text = "CRogue Demo — 拼装 + 执行可视化（真解释器驱动）";
            ClientSize = new Size(1280, 800);
            BackColor = Bg;
            ForeColor = Color.Gainsboro;
            Font = new Font("Microsoft YaHei UI", 9F);
            KeyPreview = true;

            _lineMenu = new ContextMenuStrip { BackColor = PanelBg, ForeColor = Color.Gainsboro };
            var deleteItem = new ToolStripMenuItem("删除此行（含子语句）") { ForeColor = Color.Gainsboro, BackColor = PanelBg };
            deleteItem.Click += (s, e) => DeleteSelected();
            _lineMenu.Items.Add(deleteItem);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };

            BuildUi();
            LoadPreset(0);

            _timer.Tick += OnTimer;
            _timer.Start();
        }

        // ---------- UI 构建 ----------

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Bg, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Bg };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

            // 左列：上语法块面板 / 下源码编辑区
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Bg };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var paletteGroup = NewGroup("语法块（拖到下面代码的缝隙里）");
            var palettePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PanelBg,
                Padding = new Padding(4),
            };
            foreach (PaletteItem item in Palette)
            {
                var btn = new Button
                {
                    Text = item.Title,
                    AutoSize = true,
                    BackColor = Color.FromArgb(52, 58, 68),
                    ForeColor = Color.Gainsboro,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Consolas", 10F),
                    Margin = new Padding(3),
                    Tag = item,
                    Cursor = Cursors.Hand,
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 120);
                btn.MouseDown += PaletteItem_MouseDown;
                palettePanel.Controls.Add(btn);
            }
            paletteGroup.Controls.Add(palettePanel);
            left.Controls.Add(paletteGroup, 0, 0);

            var codeGroup = NewGroup("你的 Routine（拼装区：拖入/拖动换位/右键删除/双击改参数；黄色=执行中 灰=优化掉 红=卡死）");
            _codePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = CodeBg,
                Padding = new Padding(10, 6, 0, 8),
            };
            codeGroup.Controls.Add(_codePanel);
            left.Controls.Add(codeGroup, 0, 1);
            top.Controls.Add(left, 0, 0);

            // 右列：状态 / 变量 / 日志
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Bg };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 68));

            var statusGroup = NewGroup("状态");
            var statusFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = PanelBg,
                Padding = new Padding(6, 2, 6, 2),
            };
            _tickLabel = NewInfoLabel("Tick：0");
            _phaseLabel = NewInfoLabel("未开始");
            _nextLabel = NewInfoLabel("-");
            _budgetLabel = NewInfoLabel("CPU 周期：-");
            _killsLabel = NewInfoLabel("击杀：0");
            _hpBar = NewBar(Color.LimeGreen);
            _hpBar.Size = new Size(430, 14);
            _enemyBar = NewBar(Color.OrangeRed);
            _enemyBar.Size = new Size(430, 14);
            statusFlow.Controls.Add(_phaseLabel);
            statusFlow.Controls.Add(_nextLabel);
            statusFlow.Controls.Add(_budgetLabel);
            statusFlow.Controls.Add(_killsLabel);
            statusFlow.Controls.Add(_tickLabel);
            statusFlow.Controls.Add(NewCaption("玩家 HP", 6));
            statusFlow.Controls.Add(_hpBar);
            statusFlow.Controls.Add(NewCaption("敌人 HP", 6));
            statusFlow.Controls.Add(_enemyBar);
            statusGroup.Controls.Add(statusFlow);
            right.Controls.Add(statusGroup, 0, 0);

            var varsGroup = NewGroup("变量黑板（tick 间持久）");
            _varsList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.MediumSeaGreen,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 11F),
                IntegralHeight = false,
            };
            varsGroup.Controls.Add(_varsList);
            right.Controls.Add(varsGroup, 0, 1);

            var logGroup = NewGroup("战斗日志");
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.Silver,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font("Microsoft YaHei UI", 9F),
                DetectUrls = false,
            };
            logGroup.Controls.Add(_logBox);
            right.Controls.Add(logGroup, 0, 2);

            top.Controls.Add(right, 1, 0);
            root.Controls.Add(top, 0, 0);

            // 底部控制条
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, WrapContents = false };
            _presetCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Margin = new Padding(4, 12, 12, 0) };
            foreach (Preset p in Presets)
                _presetCombo.Items.Add(p.Name);
            _presetCombo.SelectedIndex = 0;
            _presetCombo.SelectionChangeCommitted += (s, e) => LoadPreset(_presetCombo.SelectedIndex);

            _runBtn = NewButton("▶ 运行");
            _runBtn.Click += OnRunClicked;
            Button stepBtn = NewButton("⏭ 单步一条");
            stepBtn.Click += (s, e) => PullOne(true);
            Button fastBtn = NewButton("⏩ 快进本 Tick");
            fastBtn.Click += OnFastForward;
            Button resetBtn = NewButton("↺ 重置运行");
            resetBtn.Click += (s, e) => ResetRuntime();

            _stmtInterval = new NumericUpDown { Minimum = 0.05m, Maximum = 1.5m, Increment = 0.05m, Value = 0.20m, DecimalPlaces = 2, Width = 60, Margin = new Padding(4, 14, 4, 0) };
            _tickInterval = new NumericUpDown { Minimum = 0.5m, Maximum = 10m, Increment = 0.5m, Value = 3m, DecimalPlaces = 1, Width = 60, Margin = new Padding(4, 14, 4, 0) };

            bar.Controls.Add(_presetCombo);
            bar.Controls.Add(_runBtn);
            bar.Controls.Add(stepBtn);
            bar.Controls.Add(fastBtn);
            bar.Controls.Add(resetBtn);
            bar.Controls.Add(NewCaption("语句间隔(s)："));
            bar.Controls.Add(_stmtInterval);
            bar.Controls.Add(NewCaption("Tick间隔(s)："));
            bar.Controls.Add(_tickInterval);
            root.Controls.Add(bar, 0, 1);

            Controls.Add(root);
        }

        private static GroupBox NewGroup(string title)
        {
            return new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                BackColor = PanelBg,
                ForeColor = Color.DarkGray,
                Padding = new Padding(8, 4, 8, 8),
                Margin = new Padding(0, 0, 8, 0),
            };
        }

        private static Label NewCaption(string text, int topPad = 15)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Color.DarkGray, Margin = new Padding(2, topPad, 2, 0) };
        }

        private static Label NewInfoLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Color.Gainsboro, Margin = new Padding(2, 1, 2, 1) };
        }

        private static ProgressBar NewBar(Color color)
        {
            return new ProgressBar { Minimum = 0, Maximum = 100, Value = 100, ForeColor = color };
        }

        private static Button NewButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                BackColor = Color.FromArgb(60, 60, 62),
                ForeColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4, 10, 4, 0),
            };
        }

        // ---------- 预设与运行时 ----------

        private void LoadPreset(int index)
        {
            _presetIndex = index;
            Preset preset = Presets[index];
            _editor = new RoutineEditor();
            foreach (Block b in preset.Lines)
                _editor.Insert(new Slot(null, 0, _editor.Root.Length), BlockCloner.Clone(b));
            ResetRuntime();
            Log($"已加载预设：{preset.Name}——现在可以自由拖动修改它", Color.DodgerBlue);
        }

        /// <summary>重置运行时（保留拼装内容）：重建世界/上下文，Tick 从零开始。</summary>
        private void ResetRuntime()
        {
            _running = false;
            _runBtn.Text = "▶ 运行";
            _phase = Phase.Idle;
            _tickEnum = null;
            _currentLabel = null;

            _logBox.Clear();
            _world = new DemoCombatWorld(msg => Log(msg, Color.Silver));
            var vars = new Blackboard();
            Dictionary<string, Value> presetVars = Presets[_presetIndex].Vars;
            if (presetVars != null)
            {
                foreach (KeyValuePair<string, Value> kv in presetVars)
                    vars.TrySet(kv.Key, kv.Value);
            }
            _ctx = new ExecContext(_world, null, new CpuBudget(32), vars, null, null, new DemoPropertySource(_world));
            _ctx.Tick = 0;
            _routine = _editor.BuildRoutine();

            RenderRoutine();
            UpdateStatus();
        }

        /// <summary>拼装内容变化后调用：重建 Routine、重渲染、终止当前 tick。</summary>
        private void OnRoutineEdited(string hint)
        {
            _routine = _editor.BuildRoutine();
            _phase = Phase.Idle;
            _tickEnum = null;
            RenderRoutine();
            if (_running)
                StartTick();
            UpdateStatus();
            Log(hint, Color.DodgerBlue);
        }

        // ---------- 拼装区渲染（行 + 缝隙） ----------

        private void RenderRoutine()
        {
            _codePanel.Controls.Clear();
            _stmtLabels.Clear();
            _slotOf.Clear();
            _textOf.Clear();
            _selectedLabel = null;
            _selectedSlot = null;
            _lineNo = 0;

            RenderBody(_editor.Root, null, 0, 0);
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
            var gap = new Label
            {
                Text = "",
                Width = 470,
                Height = 8,
                BackColor = GapBg,
                Margin = new Padding(56, 2, 0, 2),
                AllowDrop = true,
                Tag = slot,
            };
            gap.DragEnter += Gap_DragEnter;
            gap.DragOver += Gap_DragEnter;
            gap.DragLeave += (s, e) => ((Label)s).BackColor = GapBg;
            gap.DragDrop += Gap_DragDrop;
            _codePanel.Controls.Add(gap);
        }

        private void AddLine(string text, Block statement, int depth, Slot? slot)
        {
            int no = ++_lineNo;
            var lbl = new Label
            {
                Text = $"{no,3} │ {new string(' ', depth * 4)}{text}",
                AutoSize = true,
                BackColor = CodeBg,
                ForeColor = BaseColorFor(statement),
                Font = _codeFont,
                Margin = new Padding(0, 1, 0, 1),
                Tag = statement,
                Cursor = Cursors.Hand,
            };
            if (statement != null && !_stmtLabels.ContainsKey(statement))
            {
                _stmtLabels[statement] = lbl;
                _textOf[statement] = text;
            }
            if (statement != null && slot.HasValue)
            {
                _slotOf[statement] = slot.Value;
                lbl.Click += (s, e) => Select(lbl, slot);
                lbl.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _dragStart = e.Location;
                        _dragBlock = statement;
                        _dragSlot = slot;
                    }
                };
                lbl.MouseMove += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left && _dragBlock != null
                        && (Math.Abs(e.X - _dragStart.X) > 4 || Math.Abs(e.Y - _dragStart.Y) > 4))
                    {
                        var payload = new DragPayload { FromPalette = false, Block = statement, Source = slot, Title = text };
                        lbl.DoDragDrop(payload, DragDropEffects.Move);
                        _dragBlock = null;
                    }
                };
                lbl.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        Select(lbl, slot);
                        _lineMenu.Show(lbl, e.Location);
                    }
                };
                lbl.DoubleClick += (s, e) => EditParams(statement);
            }
            _codePanel.Controls.Add(lbl);
        }

        private static Color BaseColorFor(Block s)
        {
            if (s == null)
                return Color.FromArgb(110, 110, 110); // 括号行
            switch (s.Kind)
            {
                case BlockKind.If:
                case BlockKind.For:
                case BlockKind.While:
                    return Color.FromArgb(86, 156, 214); // 控制流蓝
                case BlockKind.Return:
                case BlockKind.Break:
                case BlockKind.Continue:
                    return Color.FromArgb(197, 134, 192); // 流程紫
                default:
                    return Color.FromArgb(212, 212, 212);
            }
        }

        // ---------- 拖拽处理 ----------

        private void PaletteItem_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            var btn = (Button)sender;
            var item = (PaletteItem)btn.Tag;
            var payload = new DragPayload { FromPalette = true, Block = item.Make(), Title = item.Title };
            btn.DoDragDrop(payload, DragDropEffects.Copy);
        }

        private void Gap_DragEnter(object sender, DragEventArgs e)
        {
            var gap = (Label)sender;
            var slot = (Slot)gap.Tag;
            var payload = e.Data.GetData(typeof(DragPayload)) as DragPayload;
            if (payload == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            bool ok = payload.FromPalette
                ? _editor.CanInsert(slot, payload.Block)
                : payload.Source.HasValue && _editor.CanMove(payload.Source.Value, slot);
            e.Effect = ok
                ? (payload.FromPalette ? DragDropEffects.Copy : DragDropEffects.Move)
                : DragDropEffects.None;
            gap.BackColor = ok ? GapOk : GapBad;
        }

        private void Gap_DragDrop(object sender, DragEventArgs e)
        {
            var gap = (Label)sender;
            gap.BackColor = GapBg;
            var slot = (Slot)gap.Tag;
            var payload = e.Data.GetData(typeof(DragPayload)) as DragPayload;
            if (payload == null)
                return;
            try
            {
                if (payload.FromPalette)
                {
                    _editor.Insert(slot, payload.Block);
                    OnRoutineEdited($"已插入：{payload.Title}");
                }
                else if (payload.Source.HasValue)
                {
                    _editor.Move(payload.Source.Value, slot);
                    OnRoutineEdited($"已移动：{payload.Title}");
                }
            }
            catch (InvalidOperationException ex)
            {
                Log($"操作被拒绝：{ex.Message}", Color.Orange);
            }
        }

        // ---------- 选择 / 删除 / 参数编辑 ----------

        private void Select(Label lbl, Slot? slot)
        {
            if (_selectedLabel != null && !ReferenceEquals(_selectedLabel, lbl))
                ResetLabelStyle(_selectedLabel);
            _selectedLabel = lbl;
            _selectedSlot = slot;
            if (lbl != null && slot.HasValue && lbl.BackColor == CodeBg)
                lbl.BackColor = SelectedBg;
        }

        private void DeleteSelected()
        {
            if (_selectedSlot.HasValue && _editor.CanRemove(_selectedSlot.Value))
            {
                _editor.Remove(_selectedSlot.Value);
                OnRoutineEdited("已删除语句（含其子语句）");
            }
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
                    if (s.Condition != null && s.Condition.Kind == ExprKind.Binary
                        && s.Condition.Right != null && s.Condition.Right.Kind == ExprKind.Literal)
                    {
                        lit = s.Condition.Right;
                        title = "条件阈值";
                        return true;
                    }
                    return false;
                case BlockKind.For:
                    if (s.Condition != null && s.Condition.Kind == ExprKind.Binary
                        && s.Condition.Right != null && s.Condition.Right.Kind == ExprKind.Literal)
                    {
                        lit = s.Condition.Right;
                        title = "循环上限";
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private void EditParams(Block s)
        {
            if (!TryGetEditableLiteral(s, out Expr lit, out string title))
            {
                Log("这条语句没有可编辑的数字参数", Color.DarkGray);
                return;
            }

            using (var dlg = new Form
            {
                Text = $"编辑参数：{title}",
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(280, 110),
                BackColor = PanelBg,
                ForeColor = Color.Gainsboro,
                Font = Font,
            })
            {
                var tb = new TextBox
                {
                    Text = lit.Literal.AsNumber().ToString("0.###", CultureInfo.InvariantCulture),
                    Dock = DockStyle.Top,
                    BackColor = Color.FromArgb(24, 24, 24),
                    ForeColor = Color.Gainsboro,
                    Font = new Font("Consolas", 12F),
                };
                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom };
                dlg.Controls.Add(tb);
                dlg.Controls.Add(ok);
                dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                if (dlg.ShowDialog(this) == DialogResult.OK
                    && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    lit.Literal = Value.Of(v);
                    OnRoutineEdited($"参数已改为 {v:0.###}");
                }
            }
        }

        // ---------- 调度核心 ----------

        private void OnTimer(object sender, EventArgs e)
        {
            if (_running && _world != null)
            {
                // 世界在演化：玩家持续掉血（演示 hp 分支用）
                _world.PlayerHp -= 4f * 0.05f;
                if (_world.PlayerHp <= 0f)
                {
                    _world.PlayerHp = DemoCombatWorld.PlayerMaxHp;
                    Log("玩家被打空（Demo 不判负，重置满血）", Color.Orange);
                }
            }

            switch (_phase)
            {
                case Phase.Executing:
                    if (_running && DateTime.UtcNow >= _nextStatementAt)
                        PullOne(false);
                    break;
                case Phase.Waiting:
                    if (_running && DateTime.UtcNow >= _nextTickAt)
                        StartTick();
                    break;
            }
            UpdateStatus();
        }

        private void StartTick()
        {
            _ctx.Tick++;
            _tickStartedAt = DateTime.UtcNow;
            _tickEnum = _interp.Execute(_routine, _ctx).GetEnumerator();
            _phase = Phase.Executing;
            _nextStatementAt = DateTime.UtcNow; // 第一条立即执行
        }

        private void PullOne(bool manual)
        {
            if (_phase == Phase.Idle || _phase == Phase.Waiting)
                StartTick();

            if (_tickEnum.MoveNext())
            {
                ApplyStep(_tickEnum.Current);
                _nextStatementAt = DateTime.UtcNow.AddSeconds(StatementInterval);
            }
            else
            {
                FinishTick();
            }
        }

        private void FinishTick()
        {
            if (_currentLabel != null)
            {
                ResetLabelStyle(_currentLabel);
                _currentLabel = null;
            }
            _phase = Phase.Waiting;
            // max 规则：下一个 tick 取"固定间隔到点"与"程序实际跑完"的较大者
            _nextTickAt = _tickStartedAt.AddSeconds(TickIntervalSec);
            if (_nextTickAt < DateTime.UtcNow)
                _nextTickAt = DateTime.UtcNow.AddSeconds(0.3);
            string hung = _ctx.HungThisTick ? "，⚠ 本 tick 判定卡死（Hung）" : "";
            Log($"—— Tick {_ctx.Tick} 结束：{_ctx.StatementsExecuted} 条语句，优化掉 {_ctx.SkippedByBudget} 条{hung}",
                _ctx.HungThisTick ? Color.OrangeRed : Color.FromArgb(120, 140, 170));
        }

        private void OnFastForward(object sender, EventArgs e)
        {
            if (_phase == Phase.Idle || _phase == Phase.Waiting)
                StartTick();
            while (_tickEnum.MoveNext())
                ApplyStep(_tickEnum.Current);
            FinishTick();
        }

        private void OnRunClicked(object sender, EventArgs e)
        {
            _running = !_running;
            _runBtn.Text = _running ? "⏸ 暂停" : "▶ 运行";
            if (_running && _phase == Phase.Idle)
                StartTick();
        }

        // ---------- 步骤应用与高亮 ----------

        private void ApplyStep(StepInfo step)
        {
            ApplyHighlight(step);
            if (step.Status == StepStatus.OptimizedOut)
            {
                Log($"⚠ 周期不足，语句被优化掉：{LineTextOf(step.Statement)}", Color.FromArgb(200, 140, 60));
            }
            else if (step.Status == StepStatus.Hung)
            {
                Log($"⛔ 死循环/超限，本 tick 被 kill：{LineTextOf(step.Statement)}", Color.OrangeRed);
            }
        }

        private void ApplyHighlight(StepInfo step)
        {
            if (_currentLabel != null)
                ResetLabelStyle(_currentLabel);

            if (step.Statement == null)
                return;
            Label lbl;
            if (!_stmtLabels.TryGetValue(step.Statement, out lbl))
                return;

            switch (step.Status)
            {
                case StepStatus.Executed:
                    lbl.BackColor = Color.FromArgb(255, 213, 79); // 明黄：正在执行
                    lbl.ForeColor = Color.Black;
                    lbl.Font = _codeFontBold;
                    break;
                case StepStatus.OptimizedOut:
                    lbl.BackColor = Color.FromArgb(70, 70, 70);
                    lbl.ForeColor = Color.FromArgb(150, 150, 150);
                    lbl.Font = _codeFont;
                    break;
                case StepStatus.Hung:
                    lbl.BackColor = Color.FromArgb(170, 40, 40);
                    lbl.ForeColor = Color.White;
                    lbl.Font = _codeFontBold;
                    break;
            }
            _currentLabel = lbl;
        }

        private void ResetLabelStyle(Label lbl)
        {
            lbl.BackColor = CodeBg;
            lbl.ForeColor = BaseColorFor((Block)lbl.Tag);
            lbl.Font = _codeFont;
        }

        private string LineTextOf(Block statement)
        {
            string text;
            return statement != null && _textOf.TryGetValue(statement, out text) ? text : "?";
        }

        // ---------- 状态刷新 ----------

        private void UpdateStatus()
        {
            if (_ctx == null || _world == null)
                return;

            _tickLabel.Text = $"Tick：{_ctx.Tick}｜行数 {_editor.StatementCount}/{_editor.MaxLines}｜攻击×{_world.Attacks} 治疗×{_world.Heals} 护盾×{_world.Shields}";

            switch (_phase)
            {
                case Phase.Executing:
                    _phaseLabel.Text = _running ? "执行中（语句间隔 " + StatementInterval.ToString("0.00") + "s）" : "已暂停";
                    double toNext = Math.Max(0, (_nextStatementAt - DateTime.UtcNow).TotalSeconds);
                    _nextLabel.Text = $"下一条语句 {toNext:0.0}s 后";
                    break;
                case Phase.Waiting:
                    _phaseLabel.Text = _running ? "等待下一 Tick（max 规则）" : "已暂停";
                    double toTick = Math.Max(0, (_nextTickAt - DateTime.UtcNow).TotalSeconds);
                    _nextLabel.Text = $"下一 Tick {toTick:0.0}s 后";
                    break;
                default:
                    _phaseLabel.Text = "未开始（拼好后点 ▶ 运行）";
                    _nextLabel.Text = "-";
                    break;
            }

            _budgetLabel.Text = $"CPU 周期：{_ctx.Budget.Remaining}/{_ctx.Budget.Capacity}（已优化掉 {_ctx.SkippedByBudget}）";
            _killsLabel.Text = $"击杀：{_world.Kills}";
            _hpBar.Value = (int)Math.Max(0, Math.Min(100, _world.PlayerHp));
            _enemyBar.Value = (int)Math.Max(0, Math.Min(100, _world.EnemyHp / DemoCombatWorld.EnemyMaxHp * 100));

            _varsList.BeginUpdate();
            _varsList.Items.Clear();
            var snapshot = _ctx.Vars.Snapshot();
            var names = new List<string>(snapshot.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names)
                _varsList.Items.Add($"{name} = {snapshot[name]}");
            _varsList.EndUpdate();
        }

        private void Log(string msg, Color color)
        {
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = color;
            _logBox.AppendText($"[tick {_ctx?.Tick ?? 0}] {msg}\r\n");
            _logBox.SelectionColor = Color.Silver;
            _logBox.ScrollToCaret();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            base.OnFormClosed(e);
        }
    }
}
