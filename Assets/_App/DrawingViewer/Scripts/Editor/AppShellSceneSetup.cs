#if UNITY_EDITOR
using TMPro;
using Unity.XR.XREAL.App.Core;
using Unity.XR.XREAL.DrawingViewer;
using Unity.XR.XREAL.SwitchRecognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// Adds AppShell launcher, switch recognition stub, and return-to-menu controls to MainScene.
    /// Run from: Drawing Viewer > Setup App Shell
    /// </summary>
    public static class AppShellSceneSetup
    {
        private const string MainScenePath = "Assets/_App/DrawingViewer/Scenes/MainScene.unity";
        private const string SwitchClassDefinitionPath = "Assets/_App/SwitchRecognition/Resources/SwitchClassDefinition.asset";

        [MenuItem("Drawing Viewer/Setup App Shell")]
        public static void SetupAppShell()
        {
            if (!System.IO.File.Exists(MainScenePath))
            {
                EditorUtility.DisplayDialog("Setup Failed", "MainScene.unity not found. Run Setup Main Scene first.", "OK");
                return;
            }

            EditorSceneManager.OpenScene(MainScenePath);
            EnsureSwitchClassDefinitionAsset();

            var appShell = EnsureAppShell();
            EnsureModeLauncherUI(appShell);
            EnsureSwitchRecognitionModule();
            EnsureDrawingToolbarReturnButton();
            WireAppShell(appShell);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[AppShellSceneSetup] App shell setup complete.");
            EditorUtility.DisplayDialog("App Shell Ready",
                "MainScene now includes:\n\n" +
                "  - AppShell launcher menu\n" +
                "  - Switch recognition stub module\n" +
                "  - Return-to-menu buttons\n\n" +
                "Press Play to test the launcher flow.",
                "OK");
        }

        private static AppShell EnsureAppShell()
        {
            var existing = Object.FindObjectOfType<AppShell>();
            if (existing != null)
                return existing;

            var shellGo = new GameObject("AppShell");
            Undo.RegisterCreatedObjectUndo(shellGo, "Create AppShell");
            return shellGo.AddComponent<AppShell>();
        }

        private static void EnsureModeLauncherUI(AppShell appShell)
        {
            var existing = appShell.GetComponentInChildren<ModeLauncherUI>(true);
            if (existing != null)
                return;

            var canvasGo = CreateUIObject("ModeLauncherUI", appShell.transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var launcher = canvasGo.AddComponent<ModeLauncherUI>();
            canvasGo.AddComponent<WorldSpaceUiManipulator>();

            var panel = CreateUIObject("Panel", canvasGo.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 520f);
            panel.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.92f);

            CreateCenteredText(panel.transform, "TitleText", "Net Zero 工具箱", 48f, new Vector2(0f, 170f), new Vector2(640f, 80f));
            CreateLauncherButton(panel.transform, "Btn_DrawingViewer", "图纸查看器", new Vector2(0f, 20f), new Vector2(560f, 96f));
            CreateLauncherButton(panel.transform, "Btn_SwitchRecognition", "图像识别器", new Vector2(0f, -120f), new Vector2(560f, 96f));

            var so = new SerializedObject(launcher);
            so.FindProperty("_canvas").objectReferenceValue = canvas;
            so.FindProperty("_canvasRoot").objectReferenceValue = canvasGo.transform;
            so.FindProperty("_drawingViewerButton").objectReferenceValue = panel.transform.Find("Btn_DrawingViewer")?.GetComponent<Button>();
            so.FindProperty("_switchRecognitionButton").objectReferenceValue = panel.transform.Find("Btn_SwitchRecognition")?.GetComponent<Button>();
            so.FindProperty("_titleText").objectReferenceValue = panel.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSwitchRecognitionModule()
        {
            var existingApp = Object.FindObjectOfType<SwitchRecognitionApp>();
            if (existingApp != null)
                return;

            var root = new GameObject("SwitchRecognitionApp");
            Undo.RegisterCreatedObjectUndo(root, "Create SwitchRecognitionApp");

            var app = root.AddComponent<SwitchRecognitionApp>();
            root.AddComponent<SwitchLabelManager>();

            var uiCanvasGo = CreateUIObject("RecognitionUI_Canvas", root.transform);
            var canvas = uiCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            uiCanvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            uiCanvasGo.AddComponent<GraphicRaycaster>();

            var ui = uiCanvasGo.AddComponent<SwitchRecognitionUI>();

            var toolbar = CreateUIObject("Toolbar", uiCanvasGo.transform);
            var toolbarRect = toolbar.GetComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0, 1);
            toolbarRect.anchorMax = new Vector2(1, 1);
            toolbarRect.pivot = new Vector2(0.5f, 1);
            toolbarRect.anchoredPosition = Vector2.zero;
            toolbarRect.sizeDelta = new Vector2(0, 120);
            toolbar.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

            CreateCenteredText(toolbar.transform, "TitleText", "图像识别器", 34f, new Vector2(-180f, -18f), new Vector2(420f, 56f));
            CreateCenteredText(toolbar.transform, "StatusText", "演示模式：模型未就绪", 24f, new Vector2(120f, -18f), new Vector2(520f, 56f));
            CreateToolbarButton(toolbar.transform, "Btn_ReturnToMenu", "返回主菜单", new Vector2(620f, -18f), new Vector2(180f, 56f));

            var appSo = new SerializedObject(app);
            appSo.FindProperty("_ui").objectReferenceValue = ui;
            appSo.FindProperty("_labelManager").objectReferenceValue = root.GetComponent<SwitchLabelManager>();
            appSo.FindProperty("_classDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SwitchClassDefinition>(SwitchClassDefinitionPath);
            appSo.ApplyModifiedPropertiesWithoutUndo();

            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("_canvas").objectReferenceValue = canvas;
            uiSo.FindProperty("_canvasRoot").objectReferenceValue = uiCanvasGo.transform;
            uiSo.FindProperty("_returnToMenuButton").objectReferenceValue =
                toolbar.transform.Find("Btn_ReturnToMenu")?.GetComponent<Button>();
            uiSo.FindProperty("_statusText").objectReferenceValue =
                toolbar.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            uiSo.FindProperty("_titleText").objectReferenceValue =
                toolbar.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(true);
        }

        private static void EnsureDrawingToolbarReturnButton()
        {
            var toolbar = GameObject.Find("UI_Canvas")?.transform.Find("Toolbar");
            if (toolbar == null)
                return;

            if (toolbar.Find("Btn_ReturnToMenu") != null)
                return;

            CreateToolbarButton(toolbar, "Btn_ReturnToMenu", "返回主菜单", new Vector2(700f, -12f), new Vector2(180f, 56f));
        }

        private static void WireAppShell(AppShell appShell)
        {
            var drawingRoot = GameObject.Find("DrawingViewerApp");
            var recognitionRoot = GameObject.Find("SwitchRecognitionApp");
            var launcher = appShell.GetComponentInChildren<ModeLauncherUI>(true);

            var so = new SerializedObject(appShell);
            so.FindProperty("_drawingViewerRoot").objectReferenceValue = drawingRoot;
            so.FindProperty("_switchRecognitionRoot").objectReferenceValue = recognitionRoot;
            so.FindProperty("_launcherUI").objectReferenceValue = launcher;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSwitchClassDefinitionAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<SwitchClassDefinition>(SwitchClassDefinitionPath) != null)
                return;

            string directory = System.IO.Path.GetDirectoryName(SwitchClassDefinitionPath);
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            var asset = ScriptableObject.CreateInstance<SwitchClassDefinition>();
            AssetDatabase.CreateAsset(asset, SwitchClassDefinitionPath);
            AssetDatabase.SaveAssets();
        }

        private static void CreateLauncherButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var buttonGo = CreateUIObject(name, parent);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            buttonGo.AddComponent<Image>().color = new Color(0.22f, 0.45f, 0.82f, 0.95f);
            buttonGo.AddComponent<Button>();

            CreateCenteredText(buttonGo.transform, "Text", label, 34f, Vector2.zero, size);
        }

        private static void CreateToolbarButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var buttonGo = CreateUIObject(name, parent);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            buttonGo.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            buttonGo.AddComponent<Button>();

            CreateCenteredText(buttonGo.transform, "Text", label, 24f, Vector2.zero, size);
        }

        private static void CreateCenteredText(Transform parent, string name, string text, float fontSize, Vector2 position, Vector2 size)
        {
            var textGo = CreateUIObject(name, parent);
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

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
#endif
