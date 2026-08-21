using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>数值参数编辑弹窗（uGUI 版 InputBox）。ESC 暂停状态下也可输入。</summary>
    public static class ParamDialog
    {
        public static void Show(Transform parent, string title, double current, Action<double> onOk)
        {
            var dim = UiFactory.Rect("ParamDialog", parent,
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000));
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            dim.SetAsLastSibling();

            var panel = UiFactory.Rect("Panel", dim,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 170));
            panel.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.15f, 0.98f);

            var titleRect = UiFactory.Rect("Title", panel,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(360, 26));
            UiFactory.Label(titleRect, title, UiFonts.Text, 16, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

            var inputRect = UiFactory.Rect("Input", panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(340, 34));
            var inputGo = inputRect.gameObject;
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.08f, 0.08f, 0.09f);
            var input = inputGo.AddComponent<InputField>();
            input.contentType = InputField.ContentType.DecimalNumber;
            var inputText = UiFactory.Label(inputRect, "", UiFonts.Code, 18, Color.white);
            input.textComponent = inputText;
            input.text = current.ToString("0.###", CultureInfo.InvariantCulture);

            var okRect = UiFactory.Rect("OK", panel,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-90f, 26f), new Vector2(160, 32));
            var ok = okRect.gameObject.AddComponent<Button>();
            okRect.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.35f, 0.22f);
            UiFactory.Label(okRect, "确定", UiFonts.Text, 16, Color.white, TextAnchor.MiddleCenter);

            var cancelRect = UiFactory.Rect("Cancel", panel,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(90f, 26f), new Vector2(160, 32));
            var cancel = cancelRect.gameObject.AddComponent<Button>();
            cancelRect.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.18f, 0.18f);
            UiFactory.Label(cancelRect, "取消", UiFonts.Text, 16, Color.white, TextAnchor.MiddleCenter);

            ok.onClick.AddListener(() =>
            {
                if (double.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    || double.TryParse(input.text, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
                {
                    onOk(v);
                }
                UnityEngine.Object.Destroy(dim.gameObject);
            });
            cancel.onClick.AddListener(() => UnityEngine.Object.Destroy(dim.gameObject));
        }
    }
}
