using System;
using System.Collections.Generic;
using CPPRogue.Core.Codebase;
using CPPRogue.Core.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 构建页（主菜单 → 构建）：局外代码仓库 + 合成台。
    /// 左列仓库语块（点击选家族），右侧合成台展示该家族的完整阶梯（两块同级 → 一块升一档，
    /// 2×顶档 → 自由形参），顶部显示三种材料余额。数据来自 CodebaseState，合成直接改它。
    /// </summary>
    public sealed class BuildPanel : MonoBehaviour
    {
        private CodebaseState _state;
        private string _family = "attack";
        private Text[] _materialLabels;
        private RectTransform _listArea;
        private RectTransform _ladderArea;

        public static BuildPanel Create(Transform canvasTransform, CodebaseState state)
        {
            var go = new GameObject("BuildPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            var panel = go.AddComponent<BuildPanel>();
            panel._state = state;
            panel.BuildSkeleton();
            panel.Refresh();
            rect.SetAsLastSibling();
            return panel;
        }

        // ---------- 骨架 ----------

        private void BuildSkeleton()
        {
            var titleRect = UiFactory.Rect("Title", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(900, 32));
            UiFactory.Label(titleRect, "代码仓库 Codebase", UiFonts.Text, 24,
                new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            var subRect = UiFactory.Rect("Sub", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900, 22));
            UiFactory.Label(subRect, "局外构建 —— 撤离入库的语块在这里整理、合成（演示数据）", UiFonts.Text, 14,
                new Color(0.6f, 0.64f, 0.7f), TextAnchor.MiddleCenter);

            // 材料余额（顶栏左）
            _materialLabels = new Text[3];
            MaterialKind[] kinds = { MaterialKind.TimeSlice, MaterialKind.Ram, MaterialKind.Driver };
            float mx = 60f;
            for (int i = 0; i < kinds.Length; i++)
            {
                var rect = UiFactory.Rect(kinds[i].ToString(), transform,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(mx, -84f), new Vector2(300, 26));
                _materialLabels[i] = UiFactory.Label(rect, "", UiFonts.Text, 16,
                    CodexPanel.MaterialColor(kinds[i]));
                mx += 320f;
            }

            // 左：仓库
            RectTransform listPanel = UiFactory.Rect("ListPanel", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -124f), new Vector2(520, 660));
            listPanel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            var listHeader = UiFactory.Rect("H", listPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(500, 24));
            UiFactory.Label(listHeader, "仓库 · 语块（点击选家族）", UiFonts.Text, 15, new Color(0.62f, 0.66f, 0.74f));
            _listArea = UiFactory.Rect("List", listPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -40f), new Vector2(504, 600));

            // 右：合成台
            RectTransform ladderPanel = UiFactory.Rect("LadderPanel", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(584f, -124f), new Vector2(1296, 660));
            ladderPanel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.96f);
            var ladderHeader = UiFactory.Rect("H", ladderPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(1276, 24));
            UiFactory.Label(ladderHeader, "合成台", UiFonts.Text, 15, new Color(0.62f, 0.66f, 0.74f));

            var ruleRect = UiFactory.Rect("Rule", ladderPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -40f), new Vector2(1276, 22));
            UiFactory.Label(ruleRect, "两块同级 → 一块升一档；2× 顶档 → 自由形参版（形参留空，可绑定任意变量，最稀有）",
                UiFonts.Text, 15, new Color(0.9f, 0.85f, 0.7f));

            _ladderArea = UiFactory.Rect("Ladder", ladderPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -72f), new Vector2(1280, 560));

            // 返回
            RectTransform backRect = UiFactory.Rect("Back", transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(150, 40));
            UiFactory.SolidButton(backRect, "返回主菜单", new Color(0.35f, 0.38f, 0.44f), Color.white, UiFonts.Text, 16)
                .onClick.AddListener(() => Destroy(gameObject));
        }

        // ---------- 刷新 ----------

        private void Refresh()
        {
            MaterialKind[] kinds = { MaterialKind.TimeSlice, MaterialKind.Ram, MaterialKind.Driver };
            for (int i = 0; i < kinds.Length; i++)
                _materialLabels[i].text = $"{Materials.Name(kinds[i])}  ×{_state.CountMaterial(kinds[i])}";

            RefreshList();
            RefreshLadder();
        }

        private void RefreshList()
        {
            ClearChildren(_listArea);

            var ids = new List<string>();
            foreach (var pair in _state.Blocks)
            {
                if (pair.Value > 0 && FragmentCatalog.Parse(pair.Key) != null)
                    ids.Add(pair.Key);
            }
            ids.Sort(CompareIds);

            float y = 0f;
            if (ids.Count == 0)
            {
                var empty = UiFactory.Rect("Empty", _listArea,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(496, 26));
                UiFactory.Label(empty, "（仓库是空的——撤离入库后这里会有语块）", UiFonts.Text, 14,
                    new Color(0.5f, 0.53f, 0.58f));
                return;
            }
            foreach (string id in ids)
            {
                FragmentDef def = FragmentCatalog.Parse(id);
                bool selected = def.Family == _family;
                var rect = UiFactory.Rect(id, _listArea,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(496, 38));
                rect.gameObject.AddComponent<Image>().color = selected
                    ? new Color(0.24f, 0.38f, 0.55f, 0.98f)
                    : new Color(0.18f, 0.19f, 0.22f, 0.95f);
                UiFactory.Label(rect, $"{id}   ×{_state.Count(id)}", UiFonts.Code, 16,
                    selected ? Color.white : new Color(0.82f, 0.85f, 0.92f));
                string family = def.Family;
                rect.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _family = family;
                    Refresh();
                });
                y -= 44f;
            }
        }

        private void RefreshLadder()
        {
            ClearChildren(_ladderArea);

            float y = 0f;
            for (int tier = 1; tier <= FragmentCatalog.MaxTier; tier++)
            {
                int captured = tier;
                string id = FragmentCatalog.TierId(_family, tier);
                int count = _state.Count(id);
                bool can = _state.CanMerge(_family, tier);

                var row = UiFactory.Rect(id, _ladderArea,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(1272, 44));
                row.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.16f, 0.18f, 0.9f);
                UiFactory.Label(row, $"{id,-12} ×{count}", UiFonts.Code, 17,
                    count > 0 ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.48f, 0.54f));

                var btnRect = UiFactory.Rect("Merge", row,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(220, 34));
                Button merge = UiFactory.SolidButton(btnRect,
                    $"合成 ×2 → {CodebaseState.MergeTargetId(_family, captured)}",
                    can ? new Color(0.32f, 0.56f, 0.82f) : new Color(0.2f, 0.21f, 0.24f),
                    can ? Color.white : new Color(0.45f, 0.47f, 0.52f), UiFonts.Code, 13);
                merge.interactable = can;
                if (can)
                    merge.onClick.AddListener(() =>
                    {
                        _state.Merge(_family, captured);
                        Refresh();
                    });
                y -= 52f;
            }

            // 自由形参档（只展示，不可再合成）
            string freeId = FragmentCatalog.FreeId(_family);
            int freeCount = _state.Count(freeId);
            var freeRow = UiFactory.Rect(freeId, _ladderArea,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(1272, 44));
            freeRow.gameObject.AddComponent<Image>().color =
                freeCount > 0 ? new Color(0.35f, 0.3f, 0.12f, 0.95f) : new Color(0.15f, 0.16f, 0.18f, 0.9f);
            UiFactory.Label(freeRow,
                $"{freeId,-12} ×{freeCount}    —— 形参留空，拼装时可绑定任意变量（最稀有）",
                UiFonts.Code, 17,
                freeCount > 0 ? new Color(1f, 0.85f, 0.4f) : new Color(0.45f, 0.48f, 0.54f));
        }

        private static void ClearChildren(RectTransform area)
        {
            for (int i = area.childCount - 1; i >= 0; i--)
                Destroy(area.GetChild(i).gameObject);
        }

        /// <summary>仓库排序：家族字母序，档位升序（自由形参排在顶档之后）。</summary>
        private static int CompareIds(string a, string b)
        {
            FragmentDef da = FragmentCatalog.Parse(a);
            FragmentDef db = FragmentCatalog.Parse(b);
            int fam = string.CompareOrdinal(da.Family, db.Family);
            if (fam != 0)
                return fam;
            int ta = da.Tier == 0 ? FragmentCatalog.MaxTier + 1 : da.Tier;
            int tb = db.Tier == 0 ? FragmentCatalog.MaxTier + 1 : db.Tier;
            return ta - tb;
        }
    }
}
