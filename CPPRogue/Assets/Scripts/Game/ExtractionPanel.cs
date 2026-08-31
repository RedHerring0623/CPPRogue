using System;
using System.Collections.Generic;
using CPPRogue.Core.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>撤离结算弹窗：编译出口读条完成后弹出，展示本局入库明细。</summary>
    public sealed class ExtractionPanel : MonoBehaviour
    {
        public static void Create(Transform canvasTransform, IReadOnlyDictionary<MaterialKind, int> bag, Action onConfirm)
        {
            var go = new GameObject("ExtractionPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0f, 0.08f, 0.03f, 0.8f);

            var panel = go.AddComponent<ExtractionPanel>();
            panel.Build(bag, onConfirm);
            rect.SetAsLastSibling();
        }

        private void Build(IReadOnlyDictionary<MaterialKind, int> bag, Action onConfirm)
        {
            var box = UiFactory.Rect("Box", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 400));
            box.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.16f, 0.12f, 0.98f);

            var titleRect = UiFactory.Rect("Title", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(600, 44));
            UiFactory.Label(titleRect, "撤离成功", UiFonts.Text, 32,
                new Color(0.45f, 1f, 0.65f), TextAnchor.MiddleCenter);

            var cmdRect = UiFactory.Rect("Cmd", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(600, 24));
            UiFactory.Label(cmdRect, "g++ exit_main.cpp -o you —— compiled, exit 0",
                UiFonts.Code, 15, new Color(0.6f, 0.7f, 0.62f), TextAnchor.MiddleCenter);

            // 入库明细
            MaterialKind[] kinds = { MaterialKind.TimeSlice, MaterialKind.Ram, MaterialKind.Driver };
            bool any = false;
            foreach (MaterialKind kind in kinds)
            {
                if (bag.TryGetValue(kind, out int n) && n > 0)
                {
                    any = true;
                    break;
                }
            }

            float y = -126f;
            if (!any)
            {
                var emptyRect = UiFactory.Rect("Empty", box,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600, 26));
                UiFactory.Label(emptyRect, "本局背包是空的（0 个材料入库）", UiFonts.Text, 17,
                    new Color(0.7f, 0.74f, 0.7f), TextAnchor.MiddleCenter);
            }
            else
            {
                var headRect = UiFactory.Rect("Head", box,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600, 24));
                UiFactory.Label(headRect, "本局入库：", UiFonts.Text, 17,
                    new Color(0.85f, 0.9f, 0.85f), TextAnchor.MiddleCenter);
                y -= 34f;
                foreach (MaterialKind kind in kinds)
                {
                    bag.TryGetValue(kind, out int n);
                    if (n <= 0)
                        continue;
                    var rowRect = UiFactory.Rect(kind.ToString(), box,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600, 28));
                    UiFactory.Label(rowRect, $"{Materials.Name(kind)}  ×{n}", UiFonts.Text, 19,
                        CodexPanel.MaterialColor(kind), TextAnchor.MiddleCenter);
                    y -= 34f;
                }
            }

            var noteRect = UiFactory.Rect("Note", box,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(600, 24));
            UiFactory.Label(noteRect, "局外仓库与成长在主菜单「构建」里查看", UiFonts.Text, 15,
                new Color(0.6f, 0.66f, 0.62f), TextAnchor.MiddleCenter);

            var okRect = UiFactory.Rect("Ok", box,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(280, 48));
            UiFactory.SolidButton(okRect, "返回主菜单", new Color(0.3f, 0.6f, 0.42f), Color.black, UiFonts.Text, 18)
                .onClick.AddListener(() => onConfirm());
        }
    }
}
