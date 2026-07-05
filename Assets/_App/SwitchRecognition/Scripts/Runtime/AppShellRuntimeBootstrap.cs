using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.XREAL.App.Core;

using Unity.XR.XREAL.DrawingViewer;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// Ensures AppShell and launcher UI exist at runtime when the scene was not repaired yet.
    /// </summary>
    public static class AppShellRuntimeBootstrap
    {
        private const string ChineseFontResourcePath = "Fonts/DrawingViewerChinese SDF";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAppShell()
        {
            if (AppShell.Singleton != null)
                return;

            Debug.Log("[AppShellRuntimeBootstrap] AppShell missing in scene. Creating runtime launcher.");

            var shellGo = new GameObject("AppShell");
            CreateModeLauncherUI(shellGo.transform);
            EnsureSwitchRecognitionModule();
            EnsureDrawingToolbarReturnButton();
            shellGo.AddComponent<AppShell>();
        }

private static void CreateModeLauncherUI(Transform parent)
        {
            var canvasGo = CreateUiObject("ModeLauncherUI", parent);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<ModeLauncherUI>();
            canvasGo.AddComponent<WorldSpaceUiManipulator>();

            var panel = CreateUiObject("Panel", canvasGo.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 520f);
            panel.AddComponent<Image>().color = AppUiTheme.PanelFill;

            CreateCenteredText(panel.transform, "TitleText", "Net Zero \u5de5\u5177\u7bb1", 48f, new Vector2(0f, 170f), new Vector2(640f, 80f));
            CreateLauncherButton(panel.transform, "Btn_DrawingViewer", "\u56fe\u7eb8\u67e5\u770b\u5668", new Vector2(0f, 20f), new Vector2(560f, 96f));
            CreateLauncherButton(panel.transform, "Btn_SwitchRecognition", "\u56fe\u50cf\u8bc6\u522b\u5668", new Vector2(0f, -120f), new Vector2(560f, 96f));

            ApplyChineseFont(canvasGo.transform);
            AppUiTheme.ApplyLauncher(canvasGo.transform);
        }

private static void EnsureSwitchRecognitionModule()
        {
            if (Object.FindObjectOfType<SwitchRecognitionApp>() != null)
                return;

            var root = new GameObject("SwitchRecognitionApp");
            root.AddComponent<SwitchRecognitionApp>();
            root.AddComponent<SwitchLabelManager>();

            var uiCanvasGo = CreateUiObject("RecognitionUI_Canvas", root.transform);
            var canvas = uiCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 150;
            uiCanvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            uiCanvasGo.AddComponent<GraphicRaycaster>();
            uiCanvasGo.AddComponent<SwitchRecognitionUI>();

            var toolbar = CreateUiObject("Toolbar", uiCanvasGo.transform);
            var toolbarRect = toolbar.GetComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0, 1);
            toolbarRect.anchorMax = new Vector2(1, 1);
            toolbarRect.pivot = new Vector2(0.5f, 1);
            toolbarRect.anchoredPosition = Vector2.zero;
            toolbarRect.sizeDelta = new Vector2(0, 120);
            toolbar.AddComponent<Image>().color = AppUiTheme.ToolbarFill;

            CreateCenteredText(toolbar.transform, "TitleText", "图像识别器", 34f, new Vector2(-180f, -18f), new Vector2(420f, 56f));
            CreateCenteredText(toolbar.transform, "StatusText", "演示模式：模型未就绪", 24f, new Vector2(120f, -18f), new Vector2(520f, 56f));
            CreateToolbarButton(toolbar.transform, "Btn_ReturnToMenu", "返回主菜单", new Vector2(620f, -18f), new Vector2(180f, 56f));

            ApplyChineseFont(uiCanvasGo.transform);
            AppUiTheme.ApplyRecognitionToolbar(toolbar.transform);
            root.SetActive(true);
        }

private static void EnsureDrawingToolbarReturnButton()
        {
            var toolbar = GameObject.Find("UI_Canvas")?.transform.Find("Toolbar");
            if (toolbar == null)
                return;

            DrawingViewerUiFactory.EnsureToolbarButtons(toolbar);
            ApplyChineseFont(toolbar);
            // CreateToolbarButton(toolbar, "Btn_ReturnToMenu", "\u8fd4\u56de\u4e3b\u83dc\u5355", new Vector2(700f, -12f), new Vector2(180f, 56f));
            ApplyChineseFont(toolbar);
            AppUiTheme.ApplyToolbar(toolbar);
        }

        private static void ApplyChineseFont(Transform root)
        {
            var font = Resources.Load<TMP_FontAsset>(ChineseFontResourcePath);
            if (font == null || root == null)
                return;

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                text.font = font;
        }

private static void CreateLauncherButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var buttonGo = CreateUiObject(name, parent);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            buttonGo.AddComponent<Image>().color = AppUiTheme.PrimaryButton;
            var button = buttonGo.AddComponent<Button>();
            AppUiTheme.StyleButton(button, AppUiTheme.ButtonStyle.Primary);
            CreateCenteredText(buttonGo.transform, "Text", label, 34f, Vector2.zero, size);
        }

private static void CreateToolbarButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var buttonGo = CreateUiObject(name, parent);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            buttonGo.AddComponent<Image>().color = AppUiTheme.SecondaryButton;
            var button = buttonGo.AddComponent<Button>();
            AppUiTheme.StyleButton(button, AppUiTheme.ButtonStyle.Secondary);
            CreateCenteredText(buttonGo.transform, "Text", label, 24f, Vector2.zero, size);
        }

        private static void CreateCenteredText(Transform parent, string name, string text, float fontSize, Vector2 position, Vector2 size)
        {
            var textGo = CreateUiObject(name, parent);
            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
