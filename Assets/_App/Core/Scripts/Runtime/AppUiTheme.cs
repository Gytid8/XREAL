using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.XR.XREAL.App.Core
{
    /// <summary>
    /// Shared sci-fi / tech visual styling for all app world-space UI.
    /// Visual only — does not change interaction logic.
    /// </summary>
    public static class AppUiTheme
    {
        public static readonly Color BackgroundDeep = new Color(0.03f, 0.06f, 0.11f, 0.96f);
        public static readonly Color PanelFill = new Color(0.05f, 0.09f, 0.16f, 0.94f);
        public static readonly Color PanelFillLight = new Color(0.08f, 0.13f, 0.22f, 0.92f);
        public static readonly Color ToolbarFill = new Color(0.04f, 0.08f, 0.14f, 0.9f);
        public static readonly Color ListItemFill = new Color(0.07f, 0.12f, 0.20f, 0.95f);
        public static readonly Color ListItemHover = new Color(0.10f, 0.18f, 0.30f, 1f);

        public static readonly Color AccentCyan = new Color(0.25f, 0.88f, 1f, 1f);
        public static readonly Color AccentCyanDim = new Color(0.15f, 0.55f, 0.85f, 0.55f);
        public static readonly Color AccentTeal = new Color(0.12f, 0.75f, 0.82f, 1f);

        public static readonly Color TextPrimary = new Color(0.92f, 0.97f, 1f, 1f);
        public static readonly Color TextSecondary = new Color(0.62f, 0.78f, 0.92f, 1f);
        public static readonly Color TextMuted = new Color(0.45f, 0.58f, 0.72f, 1f);

        public static readonly Color PrimaryButton = new Color(0.06f, 0.32f, 0.58f, 0.96f);
        public static readonly Color PrimaryButtonHighlight = new Color(0.12f, 0.48f, 0.78f, 1f);
        public static readonly Color PrimaryButtonPressed = new Color(0.04f, 0.22f, 0.42f, 1f);

        public static readonly Color SecondaryButton = new Color(0.10f, 0.16f, 0.26f, 0.95f);
        public static readonly Color SecondaryButtonHighlight = new Color(0.14f, 0.24f, 0.38f, 1f);
        public static readonly Color SecondaryButtonPressed = new Color(0.06f, 0.10f, 0.18f, 1f);

        public static readonly Color DangerButton = new Color(0.55f, 0.14f, 0.18f, 0.95f);
        public static readonly Color DangerButtonHighlight = new Color(0.72f, 0.22f, 0.28f, 1f);
        public static readonly Color DangerButtonPressed = new Color(0.38f, 0.08f, 0.12f, 1f);

        public enum ButtonStyle
        {
            Primary,
            Secondary,
            Danger,
            Ghost
        }

        public static void ApplyLauncher(Transform root)
        {
            if (root == null)
                return;

            var panel = root.Find("Panel");
            if (panel != null)
            {
                StylePanel(panel, PanelFill, true);
                EnsureInnerGlow(panel, 10f);
            }

            StyleTitle(root.Find("Panel/TitleText"));
            StyleButton(root.Find("Panel/Btn_DrawingViewer")?.GetComponent<Button>(), ButtonStyle.Primary);
            StyleButton(root.Find("Panel/Btn_SwitchRecognition")?.GetComponent<Button>(), ButtonStyle.Primary);
            StyleAllTextInChildren(root, skipButtons: true);
        }

        public static void ApplyDrawingViewerCanvas(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return;

            ApplyToolbar(canvasRoot.Find("Toolbar"));
            ApplyPageNavigator(canvasRoot.Find("PageNavigator"));
            ApplyFileBrowser(canvasRoot.Find("FileBrowser"));
            ApplyMessage(canvasRoot.Find("MessageText"));
        }

        public static void ApplyRecognitionToolbar(Transform toolbar)
        {
            if (toolbar == null)
                return;

            StylePanel(toolbar, ToolbarFill, true);
            EnsureInnerGlow(toolbar, 6f);
            StyleTitle(toolbar.Find("TitleText"));
            StyleBody(toolbar.Find("StatusText"), TextSecondary);
            StyleButton(toolbar.Find("Btn_ReturnToMenu")?.GetComponent<Button>(), ButtonStyle.Secondary);
        }

        public static void ApplyToolbar(Transform toolbar)
        {
            if (toolbar == null)
                return;

            StylePanel(toolbar, ToolbarFill, true);
            EnsureInnerGlow(toolbar, 6f);

            foreach (var button in toolbar.GetComponentsInChildren<Button>(true))
            {
                var style = button.name == "Btn_OpenFile" ? ButtonStyle.Primary : ButtonStyle.Secondary;
                StyleButton(button, style);
            }
        }

        public static void ApplyPageNavigator(Transform navigator)
        {
            if (navigator == null)
                return;

            StylePanel(navigator, PanelFillLight, true);
            EnsureInnerGlow(navigator, 5f);

            foreach (var button in navigator.GetComponentsInChildren<Button>(true))
                StyleButton(button, ButtonStyle.Secondary);

            StyleBody(navigator.GetComponentInChildren<TextMeshProUGUI>(true), TextPrimary);
        }

        public static void ApplyFileBrowser(Transform browser)
        {
            if (browser == null)
                return;

            StylePanel(browser, PanelFill, true);
            EnsureInnerGlow(browser, 10f);

            var header = browser.Find("Header");
            if (header != null)
            {
                var headerImage = header.GetComponent<Image>();
                if (headerImage != null)
                    headerImage.color = new Color(0.07f, 0.12f, 0.21f, 0.98f);

                EnsureAccentBar(header, "HeaderAccent", 3f, AccentCyan, 0.75f);
                StyleTitle(header.Find("TitleText"));
                StyleButton(header.Find("Btn_Close")?.GetComponent<Button>(), ButtonStyle.Ghost);
                StyleButton(header.Find("Btn_Refresh")?.GetComponent<Button>(), ButtonStyle.Secondary);
                StyleButton(header.Find("Btn_Import")?.GetComponent<Button>(), ButtonStyle.Primary);
            }

            StyleHint(browser.Find("EmptyHintText"));

            var viewport = browser.Find("ScrollView/Viewport");
            if (viewport != null)
            {
                var vpImage = viewport.GetComponent<Image>();
                if (vpImage != null)
                    vpImage.color = new Color(0.03f, 0.06f, 0.10f, 0.65f);
            }
        }

        public static void ApplyFileListItem(Transform item)
        {
            if (item == null)
                return;

            var bg = item.GetComponent<Image>();
            if (bg != null)
                bg.color = ListItemFill;

            EnsureAccentBar(item, "ItemAccent", 2f, AccentCyanDim, 0.35f);

            var openButton = item.Find("OpenArea")?.GetComponent<Button>() ?? item.GetComponent<Button>();
            if (openButton != null)
            {
                var colors = openButton.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0.01f);
                colors.highlightedColor = ListItemHover;
                colors.pressedColor = new Color(0.08f, 0.20f, 0.36f, 1f);
                colors.selectedColor = ListItemHover;
                openButton.colors = colors;
            }

            var label = item.GetComponentInChildren<TextMeshProUGUI>(true);
            StyleBody(label, TextPrimary);

            var deleteButton = item.Find("Btn_Delete")?.GetComponent<Button>();
            StyleButton(deleteButton, ButtonStyle.Danger);
        }

        public static void ApplyMessage(Transform message)
        {
            if (message == null)
                return;

            var bg = message.GetComponent<Image>();
            if (bg == null)
                bg = message.gameObject.AddComponent<Image>();

            bg.color = new Color(0.05f, 0.12f, 0.22f, 0.92f);
            bg.raycastTarget = false;

            EnsureAccentBar(message, "MessageAccent", 3f, AccentTeal, 0.8f);
            EnsureInnerGlow(message, 8f);

            var text = message.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = TextPrimary;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        public static void StylePanel(Transform target, Color fill, bool accentTop)
        {
            if (target == null)
                return;

            var image = target.GetComponent<Image>();
            if (image == null)
                image = target.gameObject.AddComponent<Image>();

            image.color = fill;
            image.raycastTarget = true;

            if (accentTop)
                EnsureAccentBar(target, "AccentTop", 4f, AccentCyan, 0.9f);
        }

        public static void StyleButton(Button button, ButtonStyle style)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
                button.targetGraphic = image;
            }

            image.raycastTarget = true;

            Color normal, highlight, pressed;
            switch (style)
            {
                case ButtonStyle.Primary:
                    normal = PrimaryButton;
                    highlight = PrimaryButtonHighlight;
                    pressed = PrimaryButtonPressed;
                    break;
                case ButtonStyle.Danger:
                    normal = DangerButton;
                    highlight = DangerButtonHighlight;
                    pressed = DangerButtonPressed;
                    break;
                case ButtonStyle.Ghost:
                    normal = new Color(0.12f, 0.18f, 0.28f, 0.35f);
                    highlight = new Color(0.18f, 0.28f, 0.42f, 0.65f);
                    pressed = new Color(0.08f, 0.12f, 0.20f, 0.85f);
                    break;
                default:
                    normal = SecondaryButton;
                    highlight = SecondaryButtonHighlight;
                    pressed = SecondaryButtonPressed;
                    break;
            }

            image.color = normal;

            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlight;
            colors.pressedColor = pressed;
            colors.selectedColor = highlight;
            colors.disabledColor = new Color(0.15f, 0.18f, 0.24f, 0.55f);
            button.colors = colors;

            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.color = TextPrimary;
                text.fontStyle = FontStyles.Bold;
            }
        }

        public static void StyleTitle(Transform textTransform)
        {
            var text = textTransform?.GetComponent<TextMeshProUGUI>();
            if (text == null)
                return;

            text.color = AccentCyan;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = Mathf.Max(text.fontSize, 28f);
            text.outlineWidth = 0.12f;
            text.outlineColor = new Color32(0, 60, 100, 140);
        }

        public static void StyleBody(Transform textTransform, Color color)
        {
            var text = textTransform?.GetComponent<TextMeshProUGUI>();
            if (text == null)
                return;

            text.color = color;
        }

        public static void StyleBody(TextMeshProUGUI text, Color color)
        {
            if (text == null)
                return;

            text.color = color;
        }

        public static void StyleHint(Transform textTransform)
        {
            var text = textTransform?.GetComponent<TextMeshProUGUI>();
            if (text == null)
                return;

            text.color = TextMuted;
            text.fontStyle = FontStyles.Italic;
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void StyleAllTextInChildren(Transform root, bool skipButtons)
        {
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (skipButtons && text.GetComponentInParent<Button>() != null)
                    continue;

                if (text.name.Contains("Title"))
                    StyleTitle(text.transform);
                else
                    StyleBody(text, TextSecondary);
            }
        }

        private static void RepairLauncherPanelButtons(Transform panel)
        {
            const float minPixels = 40f;
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                var rect = button.GetComponent<RectTransform>();
                if (rect == null)
                    continue;

                if (rect.rect.width < minPixels || rect.rect.height < minPixels)
                    rect.sizeDelta = new Vector2(560f, 96f);
            }
        }

        
private static void EnsureAccentBar(Transform parent, string name, float height, Color color, float alpha)
        {
            if (parent == null)
                return;

            var existing = parent.Find(name);
            if (existing != null)
                return;

            var barGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(parent, false);

            var rect = barGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);

            var image = barGo.GetComponent<Image>();
            Color c = color;
            c.a = alpha;
            image.color = c;
            image.raycastTarget = false;
        }

        private static void EnsureInnerGlow(Transform parent, float inset)
        {
            if (parent == null || parent.Find("InnerGlow") != null)
                return;

            var glowGo = new GameObject("InnerGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(parent, false);
            glowGo.transform.SetAsFirstSibling();

            var rect = glowGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);

            var image = glowGo.GetComponent<Image>();
            image.color = new Color(0.12f, 0.55f, 0.85f, 0.06f);
            image.raycastTarget = false;
        }
    }
}
