using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.XREAL.App.Core;

namespace Unity.XR.XREAL.DrawingViewer
{
    public static class DrawingViewerUiFactory
    {
        private const float ToolbarButtonWidth = 72f;
        private const float ToolbarButtonHeight = 56f;
        private const float CycleLayoutButtonWidth = 86f;
        private const float CycleLayoutButtonHeight = 64f;
        private const float CycleLayoutFontSize = 22f;
        private const float MinButtonPixels = 40f;

        public static void EnsureDrawingViewerUi(Transform uiCanvasRoot)
        {
            if (uiCanvasRoot == null)
                return;

            EnsureToolbarButtons(uiCanvasRoot.Find("Toolbar"));
            EnsurePageNavigatorButtons(uiCanvasRoot.Find("PageNavigator"));
        }

        public static void EnsureToolbarButtons(Transform toolbar)
        {
            if (toolbar == null)
                return;

            EnsureButton(toolbar, "Btn_OpenFile", "\u6587\u4ef6", new Vector2(-420f, -12f));
            EnsureButton(toolbar, "Btn_ZoomOut", "-", new Vector2(-330f, -12f));
            EnsureButton(toolbar, "Btn_ZoomIn", "+", new Vector2(-240f, -12f));
            EnsureButton(toolbar, "Btn_RotateLeft", "\u5de6\u8f6c", new Vector2(-150f, -12f));
            EnsureButton(toolbar, "Btn_RotateRight", "\u53f3\u8f6c", new Vector2(-60f, -12f));
            EnsureButton(toolbar, "Btn_ResetView", "\u91cd\u7f6e", new Vector2(30f, -12f));
            EnsureButton(toolbar, "Btn_CycleLayout", "\u6392\u7248\n\u5355\u9875", new Vector2(120f, -12f), CycleLayoutButtonWidth, CycleLayoutButtonHeight);
            EnsureButton(toolbar, "Btn_ReturnToMenu", "\u8fd4\u56de\u4e3b\u83dc\u5355", new Vector2(700f, -12f), 180f, ToolbarButtonHeight);

            ApplyCycleLayoutButtonStyle(toolbar.Find("Btn_CycleLayout"));
            RepairBrokenButtonSizes(toolbar);
            AppUiTheme.ApplyToolbar(toolbar);
            BringButtonsToFront(toolbar);
        }

        public static void EnsurePageNavigatorButtons(Transform navigator)
        {
            if (navigator == null)
                return;

            EnsureButton(navigator, "Btn_Previous", "<", new Vector2(-180f, 0f), 56f, 56f);
            EnsureButton(navigator, "Btn_Next", ">", new Vector2(180f, 0f), 56f, 56f);

            var counter = navigator.Find("PageCounter");
            if (counter != null)
            {
                var rect = counter.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(220f, 56f);
                }
            }

            RepairBrokenButtonSizes(navigator);
            AppUiTheme.ApplyPageNavigator(navigator);
            BringButtonsToFront(navigator);
        }

        public static void RepairBrokenButtonSizes(Transform root)
        {
            if (root == null)
                return;

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var rect = button.GetComponent<RectTransform>();
                if (rect == null)
                    continue;

                if (rect.rect.width >= MinButtonPixels && rect.rect.height >= MinButtonPixels)
                    continue;

                if (button.name == "Btn_ReturnToMenu")
                    rect.sizeDelta = new Vector2(180f, ToolbarButtonHeight);
                else if (button.name == "Btn_CycleLayout")
                    rect.sizeDelta = new Vector2(CycleLayoutButtonWidth, CycleLayoutButtonHeight);
                else if (button.name == "Btn_Previous" || button.name == "Btn_Next")
                    rect.sizeDelta = new Vector2(56f, 56f);
                else
                    rect.sizeDelta = new Vector2(ToolbarButtonWidth, ToolbarButtonHeight);
            }
        }

        private static void BringButtonsToFront(Transform root)
        {
            if (root == null)
                return;

            foreach (var button in root.GetComponentsInChildren<Button>(true))
                button.transform.SetAsLastSibling();
        }

        private static void EnsureButton(Transform parent, string name, string label, Vector2 position, float width = ToolbarButtonWidth, float height = ToolbarButtonHeight)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                EnsureButtonLabel(existing, label);
                if (name == "Btn_CycleLayout")
                    ApplyCycleLayoutButtonStyle(existing);
                return;
            }

            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            buttonGo.transform.SetParent(parent, false);

            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);

            buttonGo.AddComponent<Image>();
            var button = buttonGo.AddComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(buttonGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = name == "Btn_ReturnToMenu" ? 22f : 26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            if (name == "Btn_CycleLayout")
                ApplyCycleLayoutButtonLabelStyle(tmp);

            DrawingViewerFontProvider.Apply(tmp);
            AppUiTheme.StyleButton(button, name == "Btn_OpenFile" ? AppUiTheme.ButtonStyle.Primary : AppUiTheme.ButtonStyle.Secondary);
        }

        public static void ApplyCycleLayoutButtonLabelStyle(TextMeshProUGUI text)
        {
            if (text == null)
                return;

            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontSize = CycleLayoutFontSize;
            text.lineSpacing = -6f;
        }

        private static void ApplyCycleLayoutButtonStyle(Transform buttonTransform)
        {
            if (buttonTransform == null)
                return;

            var rect = buttonTransform.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(CycleLayoutButtonWidth, CycleLayoutButtonHeight);

            var text = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                ApplyCycleLayoutButtonLabelStyle(text);
        }

        private static void EnsureButtonLabel(Transform buttonTransform, string label)
        {
            var text = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null)
                return;

            text.text = label;
            DrawingViewerFontProvider.Apply(text);

            if (buttonTransform.name == "Btn_CycleLayout")
                ApplyCycleLayoutButtonLabelStyle(text);
        }
    }
}
