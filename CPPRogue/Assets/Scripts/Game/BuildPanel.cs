using System;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Codebase;
using CPPRogue.Core.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 构建页（主菜单 → 构建），两个分页：
    /// - 「战备 BD」：拖拽拼装（面板 = 仓库语块，用量 ≤ 持有，改动即时存档）——进图即生效；
    /// - 「升级 / 合成」：材料余额 + 行数/时间窗兑换 + 仓库列表 + 合成台。
    /// 任何改动立即落盘（SaveFile）。
    /// </summary>
    public sealed class BuildPanel : MonoBehaviour
    {
        private PlayerProfile _profile;
        private string _family = "attack";
        private Text _feedback;

        private readonly Text[] _materialLabels = new Text[3];
        private Text _lineLabel;
        private Text _windowLabel;
        private Button _lineButton;
        private Button _windowButton;
        private RectTransform _listArea;
        private RectTransform _ladderArea;

        private RectTransform _bdTab;
        private RectTransform _upgradeTab;
        private Image _bdTabBg;
        private Image _upgradeTabBg;
        private RoutineEditor _bdEditor;
        private EditorPanel _bdPanel;

        public static BuildPanel Create(Transform canvasTransform, PlayerProfile profile)
        {
            var go = new GameObject("BuildPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 1f);   // 完全不透明：主菜单不许透出来

            var panel = go.AddComponent<BuildPanel>();
            panel._profile = profile;
            panel.BuildSkeleton();
            panel.ShowBdTab();
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
            UiFactory.Label(subRect, "局外构建 —— 撤离入库的语块与材料在这里整理（改动自动存档）", UiFonts.Text, 14,
                new Color(0.6f, 0.64f, 0.7f), TextAnchor.MiddleCenter);

            // 分页签（Image 由 SolidButton 添加，这里只留引用换色——同一 GameObject 不能挂两个 Graphic）
            RectTransform bdTabRect = UiFactory.Rect("TabBd", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200f, -80f), new Vector2(190, 40));
            UiFactory.SolidButton(bdTabRect, "战备 BD", ActiveTabColor, Color.black, UiFonts.Text, 17)
                .onClick.AddListener(ShowBdTab);
            _bdTabBg = bdTabRect.GetComponent<Image>();

            RectTransform upgradeTabRect = UiFactory.Rect("TabUpgrade", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(10f, -80f), new Vector2(190, 40));
            UiFactory.SolidButton(upgradeTabRect, "升级 / 合成", ActiveTabColor, Color.black, UiFonts.Text, 17)
                .onClick.AddListener(ShowUpgradeTab);
            _upgradeTabBg = upgradeTabRect.GetComponent<Image>();

            // 返回
            RectTransform backRect = UiFactory.Rect("Back", transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(150, 40));
            UiFactory.SolidButton(backRect, "返回主菜单", new Color(0.35f, 0.38f, 0.44f), Color.white, UiFonts.Text, 16)
                .onClick.AddListener(() => Destroy(gameObject));

            // 分页内容区（同一块区域，切页显隐；顶边距页签 30px，顶栏元素一律在区域内、不探出）
            _bdTab = UiFactory.Rect("BdArea", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -150f), new Vector2(1860, 690));
            _upgradeTab = UiFactory.Rect("UpgradeArea", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -150f), new Vector2(1860, 690));
            _upgradeTab.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            BuildUpgradeSkeleton();

            var feedbackRect = UiFactory.Rect("Feedback", transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(700, 24));
            _feedback = UiFactory.Label(feedbackRect, "", UiFonts.Text, 15,
                new Color(0.9f, 0.75f, 0.4f), TextAnchor.MiddleRight);   // 右下角：避开 BD 编辑器底部的操作提示

            // BD 编辑器（RoutineEditor 整个构建页存活；面板随分页重建以刷新语块清单）
            _bdEditor = new RoutineEditor(_profile.Progress.MaxLines);
            for (int i = 0; i < _profile.Loadout.Count; i++)
                _bdEditor.Insert(new Slot(null, 0, i), BlockCloner.Clone(_profile.Loadout[i]));
        }

        private static Color ActiveTabColor => new Color(0.32f, 0.5f, 0.68f);
        private static Color IdleTabColor => new Color(0.25f, 0.27f, 0.31f);

        // ---------- 分页切换 ----------

        private void ShowBdTab()
        {
            _bdTab.gameObject.SetActive(true);
            _upgradeTab.gameObject.SetActive(false);
            _bdTabBg.color = ActiveTabColor;
            _upgradeTabBg.color = IdleTabColor;

            // 每次进页重建面板：升级页可能刚合成出新语块，面板清单要刷新
            if (_bdPanel != null)
                Destroy(_bdPanel.gameObject);
            _bdPanel = EditorPanel.Create(_bdEditor, _bdTab, new EditorPanelOptions
            {
                Title = "战备 BD —— 从左侧拖入仓库语块拼装（改动即时存档，进图即生效）",
                Hint = "左键拖动 = 插入/移动　　右键点击 = 删除　　双击 = 改参数　　每种语块用量不能超过仓库持有",
                Palette = FragmentPalette.Build(_profile.Codebase, _bdEditor),
                CanInsert = FragmentPalette.BuildCanInsert(_profile.Codebase, _bdEditor),
                Changed = PersistBd,
                Dim = false,
                CodeWidth = 1450f,   // 填满 1860 宽的内容区（语块面板 310 + 代码区 1470）
            });
        }

        private void ShowUpgradeTab()
        {
            // 离开 BD 页先把改动落袋（Changed 已即时存档，这里兜底）
            PersistBd();

            _bdTab.gameObject.SetActive(false);
            _upgradeTab.gameObject.SetActive(true);
            _bdTabBg.color = IdleTabColor;
            _upgradeTabBg.color = ActiveTabColor;
            Refresh();
        }

        private void PersistBd()
        {
            if (FragmentPalette.Persist(_bdEditor, _profile, out string error))
                return;
            Feedback($"战备 BD 未存档：{error}");
        }

        // ---------- 升级 / 合成分页 ----------

        private void BuildUpgradeSkeleton()
        {
            // 顶栏：材料余额 + 成长兑换（区域顶边 anchor——y 必须为负才是向下，正值会探出区域盖住页签）
            MaterialKind[] kinds = { MaterialKind.TimeSlice, MaterialKind.Ram, MaterialKind.Driver };
            float mx = 40f;
            for (int i = 0; i < kinds.Length; i++)
            {
                var rect = UiFactory.Rect(kinds[i].ToString(), _upgradeTab,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(mx, -6f), new Vector2(220, 26));
                _materialLabels[i] = UiFactory.Label(rect, "", UiFonts.Text, 16, CodexPanel.MaterialColor(kinds[i]));
                mx += 240f;
            }

            var lineLabelRect = UiFactory.Rect("LineLabel", _upgradeTab,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(780f, -6f), new Vector2(190, 26));
            _lineLabel = UiFactory.Label(lineLabelRect, "", UiFonts.Text, 16, new Color(0.85f, 0.88f, 0.95f));

            var lineBtnRect = UiFactory.Rect("LineUp", _upgradeTab,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(980f, -4f), new Vector2(300, 30));
            _lineButton = UiFactory.SolidButton(lineBtnRect, "", new Color(0.4f, 0.55f, 0.4f), Color.black, UiFonts.Text, 14);
            _lineButton.onClick.AddListener(() =>
            {
                if (_profile.Progress.TryUpgradeLines(_profile.Codebase))
                {
                    Feedback("行数 +1");
                    SaveFile.Save(_profile);
                    Refresh();
                }
                else
                    Feedback("RAM 不够");
            });

            var windowLabelRect = UiFactory.Rect("WindowLabel", _upgradeTab,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1310f, -6f), new Vector2(200, 26));
            _windowLabel = UiFactory.Label(windowLabelRect, "", UiFonts.Text, 16, new Color(0.85f, 0.88f, 0.95f));

            var windowBtnRect = UiFactory.Rect("WindowUp", _upgradeTab,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1520f, -4f), new Vector2(320, 30));
            _windowButton = UiFactory.SolidButton(windowBtnRect, "", new Color(0.35f, 0.55f, 0.7f), Color.black, UiFonts.Text, 14);
            _windowButton.onClick.AddListener(() =>
            {
                if (_profile.Progress.TryUpgradeWindow(_profile.Codebase))
                {
                    Feedback("时间窗 +0.5s");
                    SaveFile.Save(_profile);
                    Refresh();
                }
                else
                    Feedback("时间片不够");
            });

            // 左：仓库（顶栏下方，同样用负偏移向下）
            RectTransform listPanel = UiFactory.Rect("ListPanel", _upgradeTab,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -50f), new Vector2(700, 630));
            listPanel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            var listHeader = UiFactory.Rect("H", listPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(680, 24));
            UiFactory.Label(listHeader, "仓库 · 语块（点击选家族）", UiFonts.Text, 15, new Color(0.62f, 0.66f, 0.74f));
            _listArea = UiFactory.Rect("List", listPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -38f), new Vector2(684, 576));

            // 右：合成台
            RectTransform ladderPanel = UiFactory.Rect("LadderPanel", _upgradeTab,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(720f, -50f), new Vector2(1140, 630));
            ladderPanel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.96f);
            var ladderHeader = UiFactory.Rect("H", ladderPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(1120, 24));
            UiFactory.Label(ladderHeader, "合成台（两块同级 → 一块升一档；2× 顶档 → 自由形参）", UiFonts.Text, 15,
                new Color(0.62f, 0.66f, 0.74f));
            _ladderArea = UiFactory.Rect("Ladder", ladderPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -38f), new Vector2(1124, 576));
        }

        private void Refresh()
        {
            MaterialKind[] kinds = { MaterialKind.TimeSlice, MaterialKind.Ram, MaterialKind.Driver };
            for (int i = 0; i < kinds.Length; i++)
                _materialLabels[i].text = $"{Materials.Name(kinds[i])} ×{_profile.Codebase.CountMaterial(kinds[i])}";

            _lineLabel.text = $"行数上限 {_profile.Progress.MaxLines}";
            _lineButton.GetComponentInChildren<Text>().text = $"行数 +1（RAM ×{_profile.Progress.NextLineCost}）";
            _lineButton.interactable = _profile.Codebase.CountMaterial(MaterialKind.Ram) >= _profile.Progress.NextLineCost;

            _windowLabel.text = $"时间窗 {_profile.Progress.WindowSeconds:0.#}s";
            _windowButton.GetComponentInChildren<Text>().text = $"时间窗 +0.5s（时间片 ×{_profile.Progress.NextWindowCost}）";
            _windowButton.interactable = _profile.Codebase.CountMaterial(MaterialKind.TimeSlice) >= _profile.Progress.NextWindowCost;

            RefreshList();
            RefreshLadder();
        }

        private void RefreshList()
        {
            ClearChildren(_listArea);

            var ids = new List<string>();
            foreach (KeyValuePair<string, int> pair in _profile.Codebase.Blocks)
            {
                if (pair.Value > 0 && FragmentCatalog.Parse(pair.Key) != null)
                    ids.Add(pair.Key);
            }
            SortIds(ids);

            if (ids.Count == 0)
            {
                AddNote(_listArea, "（仓库是空的——语块掉落接入后这里会攒语块）", 0f);
                return;
            }

            float y = 0f;
            foreach (string id in ids)
            {
                FragmentDef def = FragmentCatalog.Parse(id);
                bool selected = def.Family == _family;
                var row = UiFactory.Rect(id, _listArea,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(676, 38));
                row.gameObject.AddComponent<Image>().color = selected
                    ? new Color(0.24f, 0.38f, 0.55f, 0.98f)
                    : new Color(0.18f, 0.19f, 0.22f, 0.95f);
                UiFactory.Label(row, $"{id}  ×{_profile.Codebase.Count(id)}", UiFonts.Code, 16,
                    selected ? Color.white : new Color(0.82f, 0.85f, 0.92f));
                string family = def.Family;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() =>
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
                int count = _profile.Codebase.Count(id);
                bool can = _profile.Codebase.CanMerge(_family, tier);

                var row = UiFactory.Rect(id, _ladderArea,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(1116, 44));
                row.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.16f, 0.18f, 0.9f);
                UiFactory.Label(row, $"{id,-12} ×{count}", UiFonts.Code, 16,
                    count > 0 ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.48f, 0.54f));

                var btnRect = UiFactory.Rect("Merge", row,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(260, 34));
                Button merge = UiFactory.SolidButton(btnRect,
                    $"合成 ×2 → {CodebaseState.MergeTargetId(_family, captured)}",
                    can ? new Color(0.32f, 0.56f, 0.82f) : new Color(0.2f, 0.21f, 0.24f),
                    can ? Color.white : new Color(0.45f, 0.47f, 0.52f), UiFonts.Code, 13);
                merge.interactable = can;
                if (can)
                    merge.onClick.AddListener(() =>
                    {
                        if (_profile.Codebase.Merge(_family, captured))
                        {
                            Feedback($"合成：×2 {_family}({captured}) → {CodebaseState.MergeTargetId(_family, captured)}");
                            SaveFile.Save(_profile);
                            Refresh();
                        }
                    });
                y -= 50f;
            }

            // 自由形参档（只展示，不可再合成）
            string freeId = FragmentCatalog.FreeId(_family);
            int freeCount = _profile.Codebase.Count(freeId);
            var freeRow = UiFactory.Rect(freeId, _ladderArea,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(1116, 44));
            freeRow.gameObject.AddComponent<Image>().color =
                freeCount > 0 ? new Color(0.35f, 0.3f, 0.12f, 0.95f) : new Color(0.15f, 0.16f, 0.18f, 0.9f);
            UiFactory.Label(freeRow, $"{freeId,-12} ×{freeCount}    —— 形参留空，拼装时可绑定任意变量（最稀有）",
                UiFonts.Code, 15,
                freeCount > 0 ? new Color(1f, 0.85f, 0.4f) : new Color(0.45f, 0.48f, 0.54f));
        }

        private static void AddNote(RectTransform area, string text, float y)
        {
            var rect = UiFactory.Rect("Note", area,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(676, 24));
            UiFactory.Label(rect, text, UiFonts.Text, 13, new Color(0.5f, 0.53f, 0.58f));
        }

        private void Feedback(string message)
        {
            _feedback.text = message;
        }

        private static void ClearChildren(RectTransform area)
        {
            for (int i = area.childCount - 1; i >= 0; i--)
                Destroy(area.GetChild(i).gameObject);
        }

        /// <summary>仓库排序：家族字母序，档位升序（自由形参排在顶档之后）。</summary>
        private static void SortIds(List<string> ids)
        {
            ids.Sort((a, b) =>
            {
                FragmentDef da = FragmentCatalog.Parse(a);
                FragmentDef db = FragmentCatalog.Parse(b);
                int fam = string.CompareOrdinal(da.Family, db.Family);
                if (fam != 0)
                    return fam;
                int ta = da.Tier == 0 ? FragmentCatalog.MaxTier + 1 : da.Tier;
                int tb = db.Tier == 0 ? FragmentCatalog.MaxTier + 1 : db.Tier;
                return ta - tb;
            });
        }
    }
}
