using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 选择目标主机（主菜单 → 启动进程）：当前只有一张"测试地图"（演示场景），
    /// 点击卡片拆掉菜单世界、开始游戏（GameBootstrap.StartRun）。
    /// </summary>
    public sealed class MapSelectPanel : MonoBehaviour
    {
        public static void Create(Transform canvasTransform)
        {
            var go = new GameObject("MapSelectPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 1f);   // 完全不透明：主菜单不许透出来

            var panel = go.AddComponent<MapSelectPanel>();
            panel.Build();
            rect.SetAsLastSibling();
        }

        private void Build()
        {
            var titleRect = UiFactory.Rect("Title", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800, 34));
            UiFactory.Label(titleRect, "选择目标主机", UiFonts.Text, 26,
                new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            var subRect = UiFactory.Rect("Sub", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(800, 22));
            UiFactory.Label(subRect, "挂载目标磁盘，开始回收行动", UiFonts.Text, 15,
                new Color(0.6f, 0.64f, 0.7f), TextAnchor.MiddleCenter);

            // 唯一的地图卡：测试地图（当前演示场景）
            RectTransform card = UiFactory.Rect("MapCard", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(560, 300));
            card.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.15f, 0.19f, 0.98f);
            card.gameObject.AddComponent<Button>().onClick.AddListener(StartGame);

            var nameRect = UiFactory.Rect("Name", card,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(520, 40));
            UiFactory.Label(nameRect, "测试地图", UiFonts.Text, 28, Color.white, TextAnchor.MiddleCenter);

            var enRect = UiFactory.Rect("En", card,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(520, 22));
            UiFactory.Label(enRect, "Test Rig", UiFonts.Code, 14,
                new Color(0.55f, 0.6f, 0.68f), TextAnchor.MiddleCenter);

            var d1Rect = UiFactory.Rect("D1", card,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(520, 22));
            UiFactory.Label(d1Rect, "当前演示场景", UiFonts.Text, 16,
                new Color(0.78f, 0.82f, 0.9f), TextAnchor.MiddleCenter);

            var d2Rect = UiFactory.Rect("D2", card,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(520, 22));
            UiFactory.Label(d2Rect, "灰盒战斗验收台：自动出怪 · 十种怪物 · 3s tick", UiFonts.Text, 14,
                new Color(0.58f, 0.62f, 0.7f), TextAnchor.MiddleCenter);

            var hintRect = UiFactory.Rect("Hint", card,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(520, 24));
            UiFactory.Label(hintRect, "点击卡片进入 ▸", UiFonts.Text, 16,
                new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            // 返回
            RectTransform backRect = UiFactory.Rect("Back", transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(150, 40));
            UiFactory.SolidButton(backRect, "返回主菜单", new Color(0.35f, 0.38f, 0.44f), Color.white, UiFonts.Text, 16)
                .onClick.AddListener(() => Destroy(gameObject));
        }

        private static void StartGame()
        {
            GameBootstrap.StartRun();
        }
    }
}
