using System;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 死亡弹窗：死因 + 本局统计 + 重新开始 / 继续围观。
    /// 搜打撤规则：局内所得随死亡全部消失（LootDesign.md §3），重开从干净局面开始，
    /// 只有拼装结果（RoutineEditor）跨局保留。
    /// </summary>
    public sealed class DeathPanel : MonoBehaviour
    {
        public static DeathPanel Create(Transform canvasTransform, float survivedSeconds, int kills,
            Action onRestart, Action onSpectate)
        {
            var go = new GameObject("DeathPanel");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.12f, 0f, 0f, 0.72f);

            var panel = go.AddComponent<DeathPanel>();
            panel.Build(survivedSeconds, kills, onRestart, onSpectate);
            return panel;
        }

        private void Build(float survivedSeconds, int kills, Action onRestart, Action onSpectate)
        {
            var box = UiFactory.Rect("Box", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 400));
            box.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.10f, 0.10f, 0.98f);

            var titleRect = UiFactory.Rect("Title", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(600, 44));
            UiFactory.Label(titleRect, "进程已终止", UiFonts.Text, 32,
                new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);

            var causeRect = UiFactory.Rect("Cause", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(600, 24));
            UiFactory.Label(causeRect, "uncaught exception —— Segmentation fault (core dumped)",
                UiFonts.Code, 15, new Color(0.72f, 0.62f, 0.62f), TextAnchor.MiddleCenter);

            var lostRect = UiFactory.Rect("Lost", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(600, 24));
            UiFactory.Label(lostRect, "局内回收的代码块与材料已随进程一起被回收",
                UiFonts.Text, 16, new Color(0.9f, 0.75f, 0.45f), TextAnchor.MiddleCenter);

            var statsRect = UiFactory.Rect("Stats", box,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(600, 30));
            UiFactory.Label(statsRect, $"存活 {survivedSeconds:0.0}s　　回收进程 {kills} 个",
                UiFonts.Text, 18, new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);

            var restartRect = UiFactory.Rect("Restart", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-145f, -90f), new Vector2(260, 50));
            UiFactory.SolidButton(restartRect, "重新开始", new Color(0.32f, 0.56f, 0.82f), Color.white, UiFonts.Text, 18)
                .onClick.AddListener(() => onRestart());

            var spectateRect = UiFactory.Rect("Spectate", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(145f, -90f), new Vector2(260, 50));
            UiFactory.SolidButton(spectateRect, "继续围观", new Color(0.36f, 0.37f, 0.42f), Color.white, UiFonts.Text, 18)
                .onClick.AddListener(() => onSpectate());
        }
    }
}
