using System;
using System.Collections.Generic;
using CPPRogue.Core.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 怪物图鉴（ESC 菜单进入）：左列名单（按 普通/精英/附属 分组），右侧详情，底部材料说明。
    /// 数据来自 Assets/Resources/EnemyTable.json，解析失败回退代码内置表（EnemyCodex.Default）。
    /// 纯展示，不接触任何逻辑层状态；属性等级的实际换算在 StatTable，图鉴只报等级。
    /// </summary>
    public sealed class CodexPanel : MonoBehaviour
    {
        private const float RowStep = 46f;

        private readonly List<EntryRow> _rows = new List<EntryRow>();
        private Dictionary<string, CodexEntry> _byId;
        private CodexEntry _selected;

        private Text _nameZh;
        private Text _nameEn;
        private Text _tier;
        private Text _stats;
        private Text _drop;
        private Text _desc;
        private Text _note;
        private Text _summons;

        private struct EntryRow
        {
            public CodexEntry Entry;
            public Image Bg;
            public Color Idle;
        }

        public static CodexPanel Create(Transform canvasTransform, Action onBack, string backLabel = "返回游戏")
        {
            var go = new GameObject("CodexPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            var panel = go.AddComponent<CodexPanel>();
            panel.Build(onBack, backLabel);
            return panel;
        }

        /// <summary>数据加载：优先 Resources 里的 EnemyTable.json，坏了/没有就回退内置表。</summary>
        internal static CodexBook LoadBook()
        {
            TextAsset asset = Resources.Load<TextAsset>("EnemyTable");
            if (asset != null)
            {
                try
                {
                    return EnemyCodexJson.Parse(asset.text);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"EnemyTable.json 解析失败，图鉴回退内置表：{ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("找不到 Resources/EnemyTable.json，图鉴使用内置表");
            }
            return new CodexBook { Version = 1, Entries = EnemyCodex.Default() };
        }

        private void Build(Action onBack, string backLabel)
        {
            CodexBook book = LoadBook();
            _byId = new Dictionary<string, CodexEntry>();
            foreach (CodexEntry e in book.Entries)
                _byId[e.Id] = e;

            var titleRect = UiFactory.Rect("Title", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(900, 30));
            UiFactory.Label(titleRect, "怪物图鉴 —— 废弃主机的进程表", UiFonts.Text, 22,
                new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            var sourceRect = UiFactory.Rect("Source", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(900, 20));
            UiFactory.Label(sourceRect, $"数据源：Resources/EnemyTable.json（version {book.Version}）",
                UiFonts.Code, 13, new Color(0.5f, 0.55f, 0.6f), TextAnchor.MiddleCenter);

            // 右上角返回
            RectTransform backRect = UiFactory.Rect("Back", transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(150, 40));
            UiFactory.SolidButton(backRect, backLabel, new Color(0.35f, 0.38f, 0.44f), Color.white, UiFonts.Text, 16)
                .onClick.AddListener(() => onBack());

            BuildList(book);
            BuildDetail();
            BuildLegend();

            if (book.Entries.Count > 0)
                Select(book.Entries[0]);
        }

        // ---------- 左列：分组名单 ----------

        private void BuildList(CodexBook book)
        {
            RectTransform listRect = UiFactory.Rect("List", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -76f), new Vector2(330, 660));
            listRect.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

            float y = 8f;
            y = AddGroupHeader(listRect, "普通敌人", y);
            y = AddGroup(listRect, book, CodexTier.Normal, y);
            y = AddGroupHeader(listRect, "精英敌人", y);
            y = AddGroup(listRect, book, CodexTier.Elite, y);
            y = AddGroupHeader(listRect, "附属怪", y);
            AddGroup(listRect, book, CodexTier.Minion, y);
        }

        private float AddGroupHeader(RectTransform parent, string title, float y)
        {
            var rect = UiFactory.Rect("Header", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(314, 24));
            UiFactory.Label(rect, title, UiFonts.Text, 15, new Color(0.62f, 0.66f, 0.74f));
            return y + 28f;
        }

        private float AddGroup(RectTransform parent, CodexBook book, CodexTier tier, float y)
        {
            foreach (CodexEntry e in book.Entries)
            {
                if (e.Tier != tier)
                    continue;
                RectTransform rowRect = UiFactory.Rect(e.Id, parent,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(314, 40));
                Color idle = new Color(0.18f, 0.19f, 0.22f, 0.95f);
                Image bg = rowRect.gameObject.AddComponent<Image>();
                bg.color = idle;
                UiFactory.Label(rowRect, $"{e.NameZh}　{e.NameEn}", UiFonts.Text, 15,
                    new Color(0.86f, 0.89f, 0.95f), TextAnchor.MiddleLeft);
                CodexEntry captured = e;
                rowRect.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(captured));
                _rows.Add(new EntryRow { Entry = e, Bg = bg, Idle = idle });
                y += RowStep;
            }
            return y;
        }

        // ---------- 右侧：详情卡 ----------

        private void BuildDetail()
        {
            RectTransform card = UiFactory.Rect("Detail", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(392f, -76f), new Vector2(1488, 560));
            card.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

            _nameZh = MakeLabel(card, "NameZh", 26, new Color(0.95f, 0.96f, 1f), 12f, -8f, 460, 40);
            _tier = MakeLabel(card, "Tier", 17, Color.white, 490f, -14f, 220, 28, TextAnchor.MiddleCenter);
            _nameEn = MakeLabel(card, "NameEn", 16, new Color(0.55f, 0.6f, 0.68f), 14f, -50f, 460, 24);
            _stats = MakeLabel(card, "Stats", 17, new Color(0.75f, 0.85f, 1f), 14f, -84f, 900, 26, font: UiFonts.Code);
            _drop = MakeLabel(card, "Drop", 17, new Color(0.9f, 0.85f, 0.7f), 14f, -118f, 1440, 26);
            _desc = MakeLabel(card, "Desc", 20, new Color(0.88f, 0.9f, 0.82f), 14f, -168f, 1440, 60);
            _note = MakeLabel(card, "Note", 14, new Color(0.55f, 0.58f, 0.64f), 14f, -238f, 1440, 44);
            _summons = MakeLabel(card, "Summons", 15, new Color(0.7f, 0.85f, 0.7f), 14f, -290f, 1440, 24);
        }

        private static Text MakeLabel(RectTransform parent, string name, int size, Color color,
            float x, float y, float width, float height, TextAnchor align = TextAnchor.UpperLeft, Font font = null)
        {
            RectTransform rect = UiFactory.Rect(name, parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            return UiFactory.Label(rect, "", font ?? UiFonts.Text, size, color, align);
        }

        // ---------- 底部：材料说明 ----------

        private void BuildLegend()
        {
            RectTransform legend = UiFactory.Rect("Legend", transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1600, 96));
            legend.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

            var titleRect = UiFactory.Rect("Title", legend,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -4f), new Vector2(120, 24));
            UiFactory.Label(titleRect, "升级材料", UiFonts.Text, 15, new Color(0.62f, 0.66f, 0.74f));

            MaterialKind[] kinds = { MaterialKind.TimeSlice, MaterialKind.Ram, MaterialKind.Driver };
            float x = 140f;
            foreach (MaterialKind kind in kinds)
            {
                var rect = UiFactory.Rect(kind.ToString(), legend,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -28f), new Vector2(470, 26));
                UiFactory.Label(rect,
                    $"{Materials.Name(kind)}：{Materials.Effect(kind)}", UiFonts.Text, 15, MaterialColor(kind));
                x += 480f;
            }
        }

        // ---------- 选中与刷新 ----------

        private void Select(CodexEntry e)
        {
            _selected = e;
            _nameZh.text = e.NameZh;
            _nameEn.text = e.NameEn;
            _tier.text = $"[ {TierName(e.Tier)} ]";
            _tier.color = TierColor(e.Tier);
            _stats.text = $"属性等级  hp{e.HpLv} · atk{e.AtkLv} · spd{e.SpdLv}      出怪积分 {e.SpawnScore}";
            _drop.text = $"主掉落  {Materials.Name(e.MainDrop)}  ——  {EnemyCodex.TierRule(e.Tier)}";
            _desc.text = $"「{e.Desc}」";
            _note.text = string.IsNullOrEmpty(e.Note) ? "" : $"注：{e.Note}";
            _summons.text = e.Summons == null || e.Summons.Length == 0 ? "" : $"附属：{SummonsText(e)}";

            foreach (EntryRow row in _rows)
                row.Bg.color = row.Entry.Id == e.Id ? new Color(0.24f, 0.38f, 0.55f, 0.98f) : row.Idle;
        }

        private string SummonsText(CodexEntry e)
        {
            var names = new List<string>();
            foreach (string id in e.Summons)
                names.Add(_byId.TryGetValue(id, out CodexEntry s) ? s.NameZh : id);
            return string.Join("、", names);
        }

        private static string TierName(CodexTier tier)
        {
            switch (tier)
            {
                case CodexTier.Normal: return "普通";
                case CodexTier.Elite: return "精英";
                case CodexTier.Minion: return "附属";
                default: return tier.ToString();
            }
        }

        private static Color TierColor(CodexTier tier)
        {
            switch (tier)
            {
                case CodexTier.Elite: return new Color(0.78f, 0.62f, 1f);
                case CodexTier.Minion: return new Color(0.62f, 0.85f, 0.62f);
                default: return new Color(0.85f, 0.87f, 0.9f);
            }
        }

        internal static Color MaterialColor(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.TimeSlice: return new Color(0.45f, 0.85f, 0.95f);
                case MaterialKind.Ram: return new Color(0.58f, 0.85f, 0.58f);
                case MaterialKind.Driver: return new Color(0.95f, 0.72f, 0.42f);
                default: return Color.white;
            }
        }
    }
}
