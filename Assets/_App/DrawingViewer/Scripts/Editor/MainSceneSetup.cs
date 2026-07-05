#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.XREAL;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// Creates and configures the MainScene for the Drawing Viewer application.
    /// Run from: Drawing Viewer > Setup Main Scene
    /// </summary>
    public class MainSceneSetup : EditorWindow
    {
        [MenuItem("Drawing Viewer/Setup Main Scene")]
        public static void SetupMainScene()
        {
            // Create or load the scene
            string scenePath = "Assets/_App/DrawingViewer/Scenes/MainScene.unity";
            Scene scene;

            bool sceneExists = System.IO.File.Exists(scenePath);

            if (sceneExists)
            {
                if (!EditorUtility.DisplayDialog("Overwrite Scene?",
                    "MainScene.unity already exists. Overwrite?", "Yes", "Cancel"))
                    return;

                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            EditorSceneManager.MarkSceneDirty(scene);

            // ========================================
            // 1. Create AppBootstrapper (XR Origin + AR)
            // ========================================
            GameObject bootstrapper = new GameObject("AppBootstrapper");
            Undo.RegisterCreatedObjectUndo(bootstrapper, "Create AppBootstrapper");

            // Load and instantiate XR Origin (AR Rig) prefab
            string xrOriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.6.5/AR Starter Assets/Prefabs/XR Origin (AR Rig).prefab";
            GameObject xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(xrOriginPrefabPath);

            GameObject xrOriginInstance;
            if (xrOriginPrefab != null)
            {
                xrOriginInstance = (GameObject)PrefabUtility.InstantiatePrefab(xrOriginPrefab);
                xrOriginInstance.name = "XR Origin (AR Rig)";
                xrOriginInstance.transform.SetParent(bootstrapper.transform);

                Debug.Log($"[MainSceneSetup] Instantiated XR Origin from prefab.");
            }
            else
            {
                // Fallback: create basic XR Origin manually
                xrOriginInstance = new GameObject("XR Origin (AR Rig)");
                xrOriginInstance.transform.SetParent(bootstrapper.transform);

                var xrOrigin = xrOriginInstance.AddComponent<XROrigin>();

                var cameraOffset = new GameObject("Camera Offset");
                cameraOffset.transform.SetParent(xrOriginInstance.transform);

                var mainCamera = new GameObject("Main Camera");
                mainCamera.transform.SetParent(cameraOffset.transform);
                mainCamera.tag = "MainCamera";
                var camera = mainCamera.AddComponent<Camera>();
                mainCamera.AddComponent<ARCameraManager>();
                mainCamera.AddComponent<ARCameraBackground>();
                mainCamera.AddComponent<TrackedPoseDriver>();

                xrOrigin.CameraFloorOffsetObject = cameraOffset;
                xrOrigin.Camera = camera;

                var audioListener = mainCamera.AddComponent<AudioListener>();
                audioListener.enabled = false;

                Debug.LogWarning("[MainSceneSetup] XR Origin prefab not found. Created basic setup.");
            }

            // Add AppBootstrapper script
            var bootstrapperScript = bootstrapper.AddComponent<AppBootstrapper>();

            // Add AR Foundation managers
            var arSession = bootstrapper.AddComponent<ARSession>();
            var arInputManager = bootstrapper.AddComponent<ARInputManager>();

            // Try to add AR Plane Manager (optional)
            var arPlaneManager = bootstrapper.AddComponent<ARPlaneManager>();
            string planePrefabPath = "Assets/Samples/XR Interaction Toolkit/2.6.5/AR Starter Assets/Prefabs/AR Feathered Plane.prefab";
            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(planePrefabPath);
            if (planePrefab != null)
            {
                arPlaneManager.planePrefab = planePrefab;
            }

            // Try to add XREALInput prefab
            string xrealInputPrefabPath = "Assets/Samples/XREAL XR Plugin/3.1.0/Interaction Basics/LegacyTools/XREALInput.prefab";
            GameObject xrealInputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(xrealInputPrefabPath);
            if (xrealInputPrefab != null)
            {
                var xrealInputInstance = (GameObject)PrefabUtility.InstantiatePrefab(xrealInputPrefab);
                xrealInputInstance.transform.SetParent(bootstrapper.transform);
            }
            else
            {
                bootstrapper.AddComponent<XREALInput>();
            }

            Debug.Log("[MainSceneSetup] AppBootstrapper created.");

            // ========================================
            // 2. Create DrawingViewerApp
            // ========================================
            GameObject viewerApp = new GameObject("DrawingViewerApp");
            Undo.RegisterCreatedObjectUndo(viewerApp, "Create DrawingViewerApp");

            var appScript = viewerApp.AddComponent<DrawingViewerApp>();
            viewerApp.AddComponent<DrawingLoader>();
            viewerApp.AddComponent<DrawingPagePool>();
            viewerApp.AddComponent<DrawingCanvasManager>();
            viewerApp.AddComponent<MultiPageLayoutStrategy>();
            viewerApp.AddComponent<FocusPlaneUpdater>();
            viewerApp.AddComponent<DrawingInteractionHandler>();
            viewerApp.AddComponent<DrawingControllerInput>();
            viewerApp.AddComponent<DrawingGestureController>();
            viewerApp.AddComponent<DrawingLaserController>();
            viewerApp.AddComponent<PanelManipulator>();
            viewerApp.AddComponent<UIManager>();
            viewerApp.AddComponent<FileScanner>();
            viewerApp.AddComponent<DrawingUploadReceiver>();
            viewerApp.AddComponent<PerformanceMonitor>();
            viewerApp.AddComponent<DrawingFilePicker>();

            Debug.Log("[MainSceneSetup] DrawingViewerApp created.");

            // ========================================
            // 3. Create EventSystem
            // ========================================
            GameObject eventSystem = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Debug.Log("[MainSceneSetup] EventSystem created.");

            // ========================================
            // 4. Create UI Canvas (World Space)
            // ========================================
            GameObject uiCanvasRoot = new GameObject("UI_Canvas");
            Undo.RegisterCreatedObjectUndo(uiCanvasRoot, "Create UI Canvas");

            uiCanvasRoot.transform.position = new Vector3(0, 0, 1.5f);

            var canvas = uiCanvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasScaler = uiCanvasRoot.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 10;

            var raycaster = uiCanvasRoot.AddComponent<GraphicRaycaster>();

            // Create toolbar panel child
            GameObject toolbar = new GameObject("Toolbar");
            toolbar.transform.SetParent(uiCanvasRoot.transform);
            var toolbarRect = toolbar.AddComponent<RectTransform>();
            toolbarRect.anchoredPosition = new Vector2(0, -200);
            toolbarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 800);
            toolbarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 120);

            // Add background to toolbar
            var toolbarBg = toolbar.AddComponent<UnityEngine.UI.Image>();
            toolbarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            var toolbarScript = toolbar.AddComponent<DrawingToolbarUI>();

            // Create page navigator child
            GameObject pageNav = new GameObject("PageNavigator");
            pageNav.transform.SetParent(uiCanvasRoot.transform);
            var pageNavRect = pageNav.AddComponent<RectTransform>();
            pageNavRect.anchoredPosition = new Vector2(0, -350);
            pageNavRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 400);
            pageNavRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);

            // Add background
            var pageNavBg = pageNav.AddComponent<UnityEngine.UI.Image>();
            pageNavBg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            var pageNavScript = pageNav.AddComponent<PageNavigatorUI>();

            // Create page counter text
            GameObject pageCounter = new GameObject("PageCounter");
            pageCounter.transform.SetParent(pageNav.transform);
            var counterText = pageCounter.AddComponent<TMPro.TextMeshProUGUI>();
            counterText.text = "0 / 0";
            counterText.alignment = TMPro.TextAlignmentOptions.Center;
            counterText.fontSize = 36;
            var counterRect = pageCounter.GetComponent<RectTransform>();
            counterRect.anchoredPosition = Vector2.zero;
            counterRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
            counterRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);

            // Create message text
            GameObject messageText = new GameObject("MessageText");
            messageText.transform.SetParent(uiCanvasRoot.transform);
            var msgText = messageText.AddComponent<TMPro.TextMeshProUGUI>();
            msgText.text = "";
            msgText.alignment = TMPro.TextAlignmentOptions.Center;
            msgText.fontSize = 28;
            msgText.color = Color.yellow;
            var msgRect = messageText.GetComponent<RectTransform>();
            msgRect.anchoredPosition = new Vector2(0, 100);
            msgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 600);
            msgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);

            // Create file browser panel (initially hidden)
            GameObject fileBrowser = new GameObject("FileBrowser");
            fileBrowser.transform.SetParent(uiCanvasRoot.transform);
            fileBrowser.SetActive(false);
            var fbRect = fileBrowser.AddComponent<RectTransform>();
            fbRect.anchoredPosition = Vector2.zero;
            fbRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 600);
            fbRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);

            var fbBg = fileBrowser.AddComponent<UnityEngine.UI.Image>();
            fbBg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            // Header
            GameObject header = new GameObject("Header");
            header.transform.SetParent(fileBrowser.transform);
            var headerRect = header.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0, 80);
            header.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            CreateSimpleButton(header.transform, "Btn_Close", "X", new Vector2(-260, 0), 60);
            CreateSimpleButton(header.transform, "Btn_Import", "+", new Vector2(-180, 0), 60);
            CreateSimpleButton(header.transform, "Btn_Refresh", "↻", new Vector2(260, 0), 60);

            GameObject titleText = new GameObject("TitleText");
            titleText.transform.SetParent(header.transform);
            var titleRect = titleText.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(80, 0);
            titleRect.offsetMax = new Vector2(-80, 0);
            var titleTmp = titleText.AddComponent<TMPro.TextMeshProUGUI>();
            titleTmp.text = "选择图纸";
            titleTmp.fontSize = 32;
            titleTmp.alignment = TMPro.TextAlignmentOptions.Center;

            // Scroll view
            GameObject scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(fileBrowser.transform);
            var scrollRectTransform = scrollView.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(10, 10);
            scrollRectTransform.offsetMax = new Vector2(-10, -90);
            var scrollRect = scrollView.AddComponent<UnityEngine.UI.ScrollRect>();

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 400);

            var layout = content.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            content.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var fbScript = fileBrowser.AddComponent<FileBrowserUI>();

            Debug.Log("[MainSceneSetup] UI Canvas created.");

            // ========================================
            // 5. Create Placeholder Texture
            // ========================================
            CreatePlaceholderTexture();

            // ========================================
            // 6. Wire up serialized field references
            // ========================================
            WireUpReferences(bootstrapperScript, appScript,
                toolbarScript, pageNavScript, fbScript,
                uiCanvasRoot, fileBrowser, toolbar, pageNav,
                content, messageText);

            // ========================================
            // 7. Add scene to build settings
            // ========================================
            DrawingViewerBuildSettings.AddScenesToBuild();

            // ========================================
            // 8. Save scene
            // ========================================
            EditorSceneManager.SaveScene(scene, scenePath);

            AppShellSceneSetup.SetupAppShell();
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("====================================");
            Debug.Log("[MainSceneSetup] MainScene setup complete!");
            Debug.Log("  Next steps:");
            Debug.Log("  1. Review scene in Hierarchy window");
            Debug.Log("  2. Add sample PNG files to StreamingAssets/Drawings/");
            Debug.Log("  3. Build and deploy APK to XREAL One Pro");
            Debug.Log("====================================");

            EditorUtility.DisplayDialog("MainScene Created",
                "MainScene.unity has been created successfully!\n\n" +
                "Next: Add PNG drawing files to:\n" +
                "  Assets/_App/DrawingViewer/StreamingAssets/Drawings/\n\n" +
                "Then build for Android to create the APK.",
                "OK");
        }

        private static void WireUpReferences(
            AppBootstrapper bootstrapper,
            DrawingViewerApp app,
            DrawingToolbarUI toolbar,
            PageNavigatorUI pageNav,
            FileBrowserUI fileBrowser,
            GameObject uiCanvasRoot,
            GameObject fileBrowserGo,
            GameObject toolbarGo,
            GameObject pageNavGo,
            GameObject content,
            GameObject messageText)
        {
            // Use SerializedObject to set private fields
            var bootSo = new SerializedObject(bootstrapper);
            var appSo = new SerializedObject(app);
            var toolbarSo = new SerializedObject(toolbar);
            var pageNavSo = new SerializedObject(pageNav);
            var fbSo = new SerializedObject(fileBrowser);
            var uiManagerSo = new SerializedObject(app.GetComponent<UIManager>());

            // Wire UI Manager
            uiManagerSo.FindProperty("_mainCanvas").objectReferenceValue = uiCanvasRoot.GetComponent<Canvas>();
            uiManagerSo.FindProperty("_canvasRoot").objectReferenceValue = uiCanvasRoot;
            uiManagerSo.FindProperty("_toolbar").objectReferenceValue = toolbar;
            uiManagerSo.FindProperty("_pageNavigator").objectReferenceValue = pageNav;
            uiManagerSo.FindProperty("_fileBrowser").objectReferenceValue = fileBrowser;
            uiManagerSo.FindProperty("_messageText").objectReferenceValue = messageText.GetComponent<TMPro.TextMeshProUGUI>();
            uiManagerSo.ApplyModifiedProperties();

            // Wire FileBrowser
            fbSo.FindProperty("_listContentParent").objectReferenceValue =
                fileBrowserGo.transform.Find("ScrollView/Viewport/Content")?.transform
                ?? content.transform;
            fbSo.FindProperty("_scrollRect").objectReferenceValue =
                fileBrowserGo.transform.Find("ScrollView")?.GetComponent<UnityEngine.UI.ScrollRect>()
                ?? content.GetComponent<UnityEngine.UI.ScrollRect>();
            fbSo.FindProperty("_closeButton").objectReferenceValue =
                fileBrowserGo.transform.Find("Header/Btn_Close")?.GetComponent<UnityEngine.UI.Button>();
            fbSo.FindProperty("_refreshButton").objectReferenceValue =
                fileBrowserGo.transform.Find("Header/Btn_Refresh")?.GetComponent<UnityEngine.UI.Button>();
            fbSo.FindProperty("_importButton").objectReferenceValue =
                fileBrowserGo.transform.Find("Header/Btn_Import")?.GetComponent<UnityEngine.UI.Button>();
            fbSo.FindProperty("_titleText").objectReferenceValue =
                fileBrowserGo.transform.Find("Header/TitleText")?.GetComponent<TMPro.TextMeshProUGUI>();
            fbSo.FindProperty("_emptyHintText").objectReferenceValue =
                fileBrowserGo.transform.Find("EmptyHintText")?.GetComponent<TMPro.TextMeshProUGUI>();
            fbSo.ApplyModifiedProperties();

            // Wire PageNavigator
            pageNavSo.FindProperty("_pageCounterText").objectReferenceValue =
                pageNavGo.transform.Find("PageCounter")?.GetComponent<TMPro.TextMeshProUGUI>();
            pageNavSo.ApplyModifiedProperties();

            // Wire DrawingCanvasManager
            var canvasManager = app.GetComponent<DrawingCanvasManager>();
            var cmSo = new SerializedObject(canvasManager);
            cmSo.FindProperty("_pagePool").objectReferenceValue = app.GetComponent<DrawingPagePool>();
            cmSo.FindProperty("_layoutStrategy").objectReferenceValue = app.GetComponent<MultiPageLayoutStrategy>();
            cmSo.ApplyModifiedProperties();

            // Wire DrawingInteractionHandler
            var handler = app.GetComponent<DrawingInteractionHandler>();
            var handlerSo = new SerializedObject(handler);
            handlerSo.FindProperty("_gestureController").objectReferenceValue = app.GetComponent<DrawingGestureController>();
            handlerSo.FindProperty("_controllerInput").objectReferenceValue = app.GetComponent<DrawingControllerInput>();
            handlerSo.FindProperty("_laserController").objectReferenceValue = app.GetComponent<DrawingLaserController>();
            handlerSo.FindProperty("_panelManipulator").objectReferenceValue = app.GetComponent<PanelManipulator>();
            handlerSo.ApplyModifiedProperties();

            // Wire FocusPlaneUpdater
            var focusUpdater = app.GetComponent<FocusPlaneUpdater>();
            var fuSo = new SerializedObject(focusUpdater);
            fuSo.FindProperty("_canvasManager").objectReferenceValue = canvasManager;
            fuSo.ApplyModifiedProperties();

            appSo.FindProperty("_settings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<DrawingViewerSettings>(
                    "Assets/_App/DrawingViewer/Resources/DrawingViewerSettings.asset");
            appSo.ApplyModifiedProperties();
        }

        private static void CreateSimpleButton(Transform parent, string name, string label, Vector2 position, float size)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent);
            var rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            buttonGo.AddComponent<UnityEngine.UI.Image>().color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            buttonGo.AddComponent<UnityEngine.UI.Button>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 28;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
        }

        private static void CreatePlaceholderTexture()
        {
            string texturesPath = "Assets/_App/DrawingViewer/Textures/";
            string placeholderPath = texturesPath + "PlaceholderPage.png";

            if (System.IO.File.Exists(placeholderPath))
                return;

            if (!System.IO.Directory.Exists(texturesPath))
                System.IO.Directory.CreateDirectory(texturesPath);

            // Create a checkerboard placeholder texture
            int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isWhite = ((x / 32) + (y / 32)) % 2 == 0;
                    texture.SetPixel(x, y, isWhite
                        ? new Color(0.7f, 0.7f, 0.7f)
                        : new Color(0.5f, 0.5f, 0.5f));
                }
            }
            texture.Apply();

            byte[] pngData = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(placeholderPath, pngData);

            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(placeholderPath);

            // Set compression for Android
            var importer = AssetImporter.GetAtPath(placeholderPath) as TextureImporter;
            if (importer != null)
            {
                var androidSettings = importer.GetPlatformTextureSettings("Android");
                androidSettings.overridden = true;
                androidSettings.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(androidSettings);
                importer.SaveAndReimport();
            }

            Debug.Log("[MainSceneSetup] Placeholder texture created.");
        }
    }
}
#endif
