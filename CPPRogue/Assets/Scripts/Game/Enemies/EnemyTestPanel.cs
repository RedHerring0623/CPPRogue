using System.Collections.Generic;
using CPPRogue.Core.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 怪物测试面板（验收台 UI）：右上角一列刷怪按钮 + 自动出怪/清场开关 + 场上怪物实时统计。
    /// 纯表现层，全部动作委托 EnemyDirector，与数字键 1-0 / G / C 等价。
    /// </summary>
    public sealed class EnemyTestPanel : MonoBehaviour
    {
        private const float PanelWidth = 190f;
        private const float PanelHeight = 668f;
        private const float ButtonWidth = 166f;
        private const float ButtonHeight = 36f;
        private const float RowStep = 42f;

        private EnemyDirector _director;
        private Text _autoLabel;
        private Text _status;
        private float _refreshTimer;

        public void Setup(EnemyDirector director)
        {
            _director = director;

            // 面板底：右上角
            RectTransform panel = UiFactory.Rect("EnemyTestPanel", transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f),
                new Vector2(PanelWidth, PanelHeight));
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // 标题
            var titleRect = UiFactory.Rect("Title", panel,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(PanelWidth - 12f, 26f));
            UiFactory.Label(titleRect, "怪物测试台", UiFonts.Text, 17, new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);

            // 十种怪的刷怪按钮，颜色即小怪本体色
            float y = 40f;
            foreach (var entry in SpawnEntries())
            {
                EnemyKind kind = entry.kind;
                MakeButton(entry.title, EnemyVisuals.KindColor(kind) * 0.85f + Color.white * 0.15f, panel, y)
                    .onClick.AddListener(() => director.SpawnManual(kind));
                y += RowStep;
            }

            // 控制按钮：自动出怪 / 清场
            y += 6f;
            Button autoButton = MakeButton("自动出怪", new Color(0.35f, 0.55f, 0.8f), panel, y);
            autoButton.onClick.AddListener(director.ToggleAutoSpawn);
            _autoLabel = autoButton.GetComponentInChildren<Text>();
            y += RowStep;
            MakeButton("清场", new Color(0.75f, 0.4f, 0.4f), panel, y).onClick.AddListener(director.ClearEnemies);

            // 场上统计
            var statusRect = UiFactory.Rect("Status", panel,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(PanelWidth - 20f, 96f));
            _status = UiFactory.Label(statusRect, "", UiFonts.Text, 14, new Color(0.85f, 0.88f, 0.95f), TextAnchor.UpperLeft);
        }

        private void Update()
        {
            if (_director == null || _director.Sim == null)
                return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f)
                return;
            _refreshTimer = 0.2f;

            if (_autoLabel != null)
                _autoLabel.text = _director.AutoSpawn ? "自动出怪：开" : "自动出怪：关";

            if (_status != null)
            {
                var counts = new Dictionary<string, int>();
                foreach (Enemy e in _director.Sim.Enemies)
                {
                    if (e.Dead)
                        continue;
                    counts.TryGetValue(e.DisplayName, out int n);
                    counts[e.DisplayName] = n + 1;
                }
                if (counts.Count == 0)
                {
                    _status.text = "场上：空";
                }
                else
                {
                    var parts = new List<string>();
                    foreach (var pair in counts)
                        parts.Add($"{pair.Key}×{pair.Value}");
                    _status.text = "场上：" + string.Join("  ", parts);
                }
            }
        }

        private static readonly (EnemyKind kind, string title)[] Menu =
        {
            (EnemyKind.Bug, "1 Bug"), (EnemyKind.NullPointer, "2 空指针"), (EnemyKind.Breakpoint, "3 断点"),
            (EnemyKind.Exception, "4 异常"), (EnemyKind.DeepCopy, "5 深拷贝"), (EnemyKind.Watchdog, "6 看门狗"),
            (EnemyKind.Compiling, "7 编译中"), (EnemyKind.Overheat, "8 过热"), (EnemyKind.Storm, "9 风暴"),
            (EnemyKind.Parent, "0 父进程"),
        };

        private static IEnumerable<(EnemyKind kind, string title)> SpawnEntries()
        {
            foreach (var entry in Menu)
                yield return entry;
        }

        /// <summary>创建一个纯色按钮（Image + Button + 居中黑字），返回 Button 供挂回调。</summary>
        private Button MakeButton(string label, Color tint, RectTransform parent, float y)
        {
            RectTransform rect = UiFactory.Rect(label, parent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y),
                new Vector2(ButtonWidth, ButtonHeight));
            rect.gameObject.AddComponent<Image>().color = tint;
            Button button = rect.gameObject.AddComponent<Button>();
            UiFactory.Label(rect, label, UiFonts.Text, 15, Color.black, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
