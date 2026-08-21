using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>uGUI 控件的代码创建辅助（无 prefab 阶段用）。</summary>
    public static class UiFactory
    {
        /// <summary>创建一个手动定位的 RectTransform。</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>在父节点内创建拉伸填充的文本（不拦截射线）。</summary>
        public static Text Label(Transform parent, string content, Font font, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 2f);
            rect.offsetMax = new Vector2(-6f, -2f);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
