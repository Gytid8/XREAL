#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// Repairs and completes scene wiring without overwriting the entire MainScene.
    /// Run from: Drawing Viewer > Repair Main Scene
    /// </summary>
    public static class DrawingViewerSceneRepair
    {
        private const string MainScenePath = "Assets/_App/DrawingViewer/Scenes/MainScene.unity";
        private const string SettingsPath = "Assets/_App/DrawingViewer/Resources/DrawingViewerSettings.asset";

        [MenuItem("Drawing Viewer/Repair Main Scene")]
        public static void RepairMainScene()
        {
            if (!System.IO.File.Exists(MainScenePath))
            {
                EditorUtility.DisplayDialog("Repair Failed", "MainScene.unity not found. Run Setup Main Scene first.", "OK");
                return;
            }

            EditorSceneManager.OpenScene(MainScenePath);

            var uiCanvas = GameObject.Find("UI_Canvas");
            var app = Object.FindObjectOfType<DrawingViewerApp>();
            var uiManager = uiCanvas != null
                ? uiCanvas.GetComponentInChildren<UIManager>(true)
                : Object.FindObjectOfType<UIManager>();
            var fileBrowser = uiCanvas != null
                ? uiCanvas.transform.Find("FileBrowser")?.GetComponent<FileBrowserUI>()
                : Object.FindObjectOfType<FileBrowserUI>();

            if (app == null || uiCanvas == null || fileBrowser == null)
            {
                EditorUtility.DisplayDialog("Repair Failed", "Required scene objects are missing. Run Setup Main Scene first.", "OK");
                return;
            }

            WireSettings(app);
            DisableEditorARComponentsInScene();
            CompleteFileBrowserUI(fileBrowser, uiCanvas.transform);
            ConfigureWorldSpaceCanvas(uiCanvas);
            FixFileBrowserTransform(uiCanvas.transform.Find("FileBrowser"));
            FixToolbarTransform(uiCanvas.transform.Find("Toolbar"));
            FixPageNavigatorTransform(uiCanvas.transform.Find("PageNavigator"));
            EnsureEditModeSetup(uiCanvas);
            EnsureInteractionComponents(app);
            DrawingViewerChineseFontSetup.SetupChineseFontIfMissing();
            WireUIManager(uiManager, uiCanvas);

            AppShellSceneSetup.SetupAppShell();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[DrawingViewerSceneRepair] MainScene repair complete.");
            EditorUtility.DisplayDialog("Scene Repaired",
                "MainScene has been updated:\n\n" +
                "  - Settings asset wired\n" +
                "  - File browser header/controls added\n" +
                "  - Import (+) and delete controls for user drawings\n" +
                "  - World-space UI canvas scaled\n\n" +
                "Editor shortcuts: F=file browser, Tab=UI, R=reset, L=layout, +/-=zoom, Z/X=rotate, []=pages",
                "OK");
        }

        private static void DisableEditorARComponentsInScene()
        {
            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            if (bootstrapper == null) return;

            foreach (var behaviour in bootstrapper.GetComponents<Behaviour>())
            {
                if (behaviour is ARSession ||
                    behaviour is ARInputManager ||
                    behaviour is ARPlaneManager ||
                    behaviour is ARCameraManager ||
                    behaviour is ARCameraBackground)
                {
                    behaviour.enabled = false;
                }
            }

            var mainCamera = bootstrapper.GetComponentInChildren<Camera>(true);
            if (mainCamera != null)
            {
                foreach (var behaviour in mainCamera.GetComponents<Behaviour>())
                {
                    if (behaviour is ARCameraManager || behaviour is ARCameraBackground)
                        behaviour.enabled = false;
                }
            }
        }

        private static void WireSettings(DrawingViewerApp app)
        {
            var settings = AssetDatabase.LoadAssetAtPath<DrawingViewerSettings>(SettingsPath);
            if (settings == null)
            {
                DrawingViewerResourceSetup.CreateSettingsAsset();
                settings = AssetDatabase.LoadAssetAtPath<DrawingViewerSettings>(SettingsPath);
            }

            var so = new SerializedObject(app);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CompleteFileBrowserUI(FileBrowserUI fileBrowser, Transform uiCanvas)
        {
            if (fileBrowser == null) return;

            Transform browserRoot = fileBrowser.transform;

            EnsureHeader(browserRoot);
            EnsureScrollView(browserRoot);

            var fbSo = new SerializedObject(fileBrowser);
            fbSo.FindProperty("_listContentParent").objectReferenceValue =
                browserRoot.Find("ScrollView/Viewport/Content");
            fbSo.FindProperty("_scrollRect").objectReferenceValue =
                browserRoot.Find("ScrollView")?.GetComponent<ScrollRect>();
            fbSo.FindProperty("_closeButton").objectReferenceValue =
                browserRoot.Find("Header/Btn_Close")?.GetComponent<Button>();
            fbSo.FindProperty("_refreshButton").objectReferenceValue =
                browserRoot.Find("Header/Btn_Refresh")?.GetComponent<Button>();
            fbSo.FindProperty("_importButton").objectReferenceValue =
                browserRoot.Find("Header/Btn_Import")?.GetComponent<Button>();
            fbSo.FindProperty("_titleText").objectReferenceValue =
                browserRoot.Find("Header/TitleText")?.GetComponent<TextMeshProUGUI>();
            fbSo.FindProperty("_emptyHintText").objectReferenceValue =
                browserRoot.Find("EmptyHintText")?.GetComponent<TextMeshProUGUI>();
            fbSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureHeader(Transform browserRoot)
        {
            var header = browserRoot.Find("Header");
            if (header == null)
            {
                var headerGo = CreateUIObject("Header", browserRoot);
                var headerRect = headerGo.GetComponent<RectTransform>();
                headerRect.anchorMin = new Vector2(0, 1);
                headerRect.anchorMax = new Vector2(1, 1);
                headerRect.pivot = new Vector2(0.5f, 1);
                headerRect.anchoredPosition = Vector2.zero;
                headerRect.sizeDelta = new Vector2(0, 80);

                var bg = headerGo.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

                header = headerGo.transform;
            }

            CreateHeaderButton(header, "Btn_Close", "X", new Vector2(-260, 0), 60);
            CreateHeaderButton(header, "Btn_Import", "+", new Vector2(-180, 0), 60);
            CreateHeaderButton(header, "Btn_Refresh", "↻", new Vector2(260, 0), 60);

            var title = header.Find("TitleText");
            if (title == null)
            {
                var titleGo = CreateUIObject("TitleText", header);
                var titleRect = titleGo.GetComponent<RectTransform>();
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = new Vector2(80, 0);
                titleRect.offsetMax = new Vector2(-80, 0);

                var tmp = titleGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "选择图纸";
                tmp.fontSize = 32;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }

            EnsureEmptyHint(browserRoot);
        }

        private static void EnsureEmptyHint(Transform browserRoot)
        {
            var hint = browserRoot.Find("EmptyHintText");
            if (hint != null) return;

            var hintGo = CreateUIObject("EmptyHintText", browserRoot);
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0.35f);
            hintRect.anchorMax = new Vector2(0.9f, 0.65f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            var tmp = hintGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "暂无图纸\n点击右上角 + 上传图纸";
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.8f, 0.8f, 0.85f, 0.9f);
            tmp.raycastTarget = false;
            hintGo.SetActive(false);
        }

        private static void EnsureScrollView(Transform browserRoot)
        {
            var scrollView = browserRoot.Find("ScrollView");
            if (scrollView != null) return;

            var legacyContent = browserRoot.Find("Content");
            if (legacyContent != null)
                Undo.DestroyObjectImmediate(legacyContent.gameObject);

            var scrollGo = CreateUIObject("ScrollView", browserRoot);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(10, 10);
            scrollRectTransform.offsetMax = new Vector2(-10, -90);

            var viewportGo = CreateUIObject("Viewport", scrollGo.transform);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = CreateUIObject("Content", viewportGo.transform);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 400);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }

        private static void CreateHeaderButton(Transform header, string name, string label, Vector2 position, float size)
        {
            var existing = header.Find(name);
            if (existing != null) return;

            var buttonGo = CreateUIObject(name, header);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            buttonGo.AddComponent<Button>();

            var textGo = CreateUIObject("Text", buttonGo.transform);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        private static void ConfigureWorldSpaceCanvas(GameObject uiCanvas)
        {
            var settings = AssetDatabase.LoadAssetAtPath<DrawingViewerSettings>(SettingsPath);
            float canvasWidth = settings != null ? settings.UICanvasWidth : DrawingViewerUILayout.DefaultCanvasWidthMeters;
            float bottomLift = settings != null ? settings.UIBottomBarLiftPixels : DrawingViewerUILayout.DefaultBottomBarLiftPixels;

            DrawingViewerUILayout.ConfigureCanvasRoot(uiCanvas.transform, canvasWidth);
            DrawingViewerUILayout.ApplyAll(uiCanvas.transform, bottomLift);
            DrawingViewerUILayout.ApplyEditorSceneTransform(uiCanvas.transform);

            var scaler = uiCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
                scaler.dynamicPixelsPerUnit = 10f;
        }

        private static void EnsureEditModeSetup(GameObject uiCanvas)
        {
            if (uiCanvas.GetComponent<DrawingViewerUIEditModeSetup>() == null)
                uiCanvas.AddComponent<DrawingViewerUIEditModeSetup>();
        }

        private static void EnsureInteractionComponents(DrawingViewerApp app)
        {
            if (app == null) return;

            if (app.GetComponent<DrawingLaserController>() == null)
                app.gameObject.AddComponent<DrawingLaserController>();

            if (app.GetComponent<DrawingFilePicker>() == null)
                app.gameObject.AddComponent<DrawingFilePicker>();

            if (app.GetComponent<DrawingUploadReceiver>() == null)
                app.gameObject.AddComponent<DrawingUploadReceiver>();

            if (app.GetComponent<XREALInput>() == null)
                app.gameObject.AddComponent<XREALInput>();

            var handler = app.GetComponent<DrawingInteractionHandler>();
            if (handler == null) return;

            var handlerSo = new SerializedObject(handler);
            handlerSo.FindProperty("_gestureController").objectReferenceValue = app.GetComponent<DrawingGestureController>();
            handlerSo.FindProperty("_controllerInput").objectReferenceValue = app.GetComponent<DrawingControllerInput>();
            handlerSo.FindProperty("_laserController").objectReferenceValue = app.GetComponent<DrawingLaserController>();
            handlerSo.FindProperty("_panelManipulator").objectReferenceValue = app.GetComponent<PanelManipulator>();
            handlerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FixFileBrowserTransform(Transform fileBrowser)
        {
            DrawingViewerUILayout.ApplyFileBrowser(fileBrowser);
        }

        private static void FixToolbarTransform(Transform toolbar)
        {
            DrawingViewerUILayout.ApplyToolbar(toolbar);
        }

        private static void FixPageNavigatorTransform(Transform pageNavigator)
        {
            var settings = AssetDatabase.LoadAssetAtPath<DrawingViewerSettings>(SettingsPath);
            float bottomLift = settings != null ? settings.UIBottomBarLiftPixels : DrawingViewerUILayout.DefaultBottomBarLiftPixels;
            DrawingViewerUILayout.ApplyPageNavigator(pageNavigator, bottomLift);
        }

        private static void WireUIManager(UIManager uiManager, GameObject uiCanvas)
        {
            if (uiManager == null) return;

            var so = new SerializedObject(uiManager);
            so.FindProperty("_mainCanvas").objectReferenceValue = uiCanvas.GetComponent<Canvas>();
            so.FindProperty("_canvasRoot").objectReferenceValue = uiCanvas;
            so.FindProperty("_toolbar").objectReferenceValue = uiCanvas.transform.Find("Toolbar")?.GetComponent<DrawingToolbarUI>();
            so.FindProperty("_pageNavigator").objectReferenceValue = uiCanvas.transform.Find("PageNavigator")?.GetComponent<PageNavigatorUI>();
            so.FindProperty("_fileBrowser").objectReferenceValue = uiCanvas.transform.Find("FileBrowser")?.GetComponent<FileBrowserUI>();
            so.FindProperty("_messageText").objectReferenceValue = uiCanvas.transform.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();
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
