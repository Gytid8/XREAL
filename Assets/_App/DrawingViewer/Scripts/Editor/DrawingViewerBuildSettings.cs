#if UNITY_EDITOR
using Unity.XR.XREAL;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Unity.XR.XREAL.DrawingViewer.Editor
{
    /// <summary>
    /// Validates build settings and generates necessary assets before building the APK.
    /// </summary>
    public class DrawingViewerBuildSettings : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            Debug.Log("[DrawingViewerBuildSettings] Preprocessing Android build...");

            // Validate that the main scene is in the build settings
            ValidateSceneSetup();

            // Generate StreamingAssets manifest for Android
            SyncDrawingAssetsToUnityStreamingAssets();
            GenerateManifest();

            // Validate XREAL settings
            ValidateXREALSettings();

            // Ensure permissions
            ValidatePermissions();

            Debug.Log("[DrawingViewerBuildSettings] Build preprocessing complete.");
        }

        private void ValidateSceneSetup()
        {
            var scenes = EditorBuildSettings.scenes;
            bool hasMainScene = false;

            foreach (var scene in scenes)
            {
                if (scene.path.Contains("DrawingViewer/Scenes/MainScene"))
                {
                    hasMainScene = true;
                    if (!scene.enabled)
                    {
                        Debug.LogWarning("[DrawingViewerBuildSettings] MainScene is in build settings but not enabled!");
                    }
                    break;
                }
            }

            if (!hasMainScene)
            {
                Debug.LogWarning("[DrawingViewerBuildSettings] MainScene not found in build settings.");
                Debug.LogWarning("   Add: Assets/_App/DrawingViewer/Scenes/MainScene.unity");
            }
        }

        private void GenerateManifest()
        {
            // Call FileScanner's manifest generator
            FileScanner.GenerateStreamingAssetsManifest();
        }

        private static void SyncDrawingAssetsToUnityStreamingAssets()
        {
            string sourceRoot = "Assets/_App/DrawingViewer/StreamingAssets/Drawings";
            string targetRoot = "Assets/StreamingAssets/Drawings";

            if (!System.IO.Directory.Exists(sourceRoot))
                return;

            if (!System.IO.Directory.Exists(targetRoot))
                System.IO.Directory.CreateDirectory(targetRoot);

            foreach (string sourcePath in System.IO.Directory.GetFiles(sourceRoot, "*", System.IO.SearchOption.AllDirectories))
            {
                string fileName = System.IO.Path.GetFileName(sourcePath);
                if (fileName.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = sourcePath.Substring(sourceRoot.Length).TrimStart('\\', '/');
                string targetPath = System.IO.Path.Combine(targetRoot, relative);
                string targetDirectory = System.IO.Path.GetDirectoryName(targetPath);

                if (!System.IO.Directory.Exists(targetDirectory))
                    System.IO.Directory.CreateDirectory(targetDirectory);

                System.IO.File.Copy(sourcePath, targetPath, overwrite: true);
            }

            AssetDatabase.Refresh();
            Debug.Log("[DrawingViewerBuildSettings] Synced DrawingViewer sample drawings into Assets/StreamingAssets/Drawings for Android packaging.");
        }

        private void ValidateXREALSettings()
        {
            var settings = XREALSettings.GetSettings();
            if (settings == null)
            {
                Debug.LogError("[DrawingViewerBuildSettings] XREALSettings not found!");
                return;
            }

            Debug.Log($"[DrawingViewerBuildSettings] XREAL Settings OK. StereoRendering={settings.StereoRendering}");
        }

        private void ValidatePermissions()
        {
            // Ensure CAMERA permission is present
            if (!PlayerSettings.Android.forceSDCardPermission)
            {
                Debug.Log("[DrawingViewerBuildSettings] Note: Storage permission may be needed for external files.");
            }
        }

        /// <summary>
        /// Menu item to quickly validate the project setup.
        /// </summary>
        [MenuItem("Drawing Viewer/Validate Project Setup")]
        public static void ValidateProject()
        {
            Debug.Log("=== Drawing Viewer Project Validation ===");

            // Check XREAL package
            var xrealLoader = AssetDatabase.LoadAssetAtPath<XRLoader>(
                "Assets/XR/Loaders/XREALXRLoader.asset");
            Debug.Log(xrealLoader != null
                ? "[OK] XREAL Loader found."
                : "[WARN] XREAL Loader not found.");

            // Check required scenes
            var mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/_App/DrawingViewer/Scenes/MainScene.unity");
            Debug.Log(mainScene != null
                ? "[OK] MainScene found."
                : "[MISSING] MainScene not found - create it.");

            // Check Resources
            var settings = Resources.Load<DrawingViewerSettings>("DrawingViewerSettings");
            Debug.Log(settings != null
                ? "[OK] DrawingViewerSettings found."
                : "[WARN] DrawingViewerSettings not found in Resources.");

            // Check StreamingAssets
            bool drawingsExist = System.IO.Directory.Exists(
                System.IO.Path.Combine(Application.streamingAssetsPath, "Drawings"));
            Debug.Log(drawingsExist
                ? "[OK] StreamingAssets/Drawings/ exists."
                : "[INFO] StreamingAssets/Drawings/ not found (will be created).");

            Debug.Log("=== Validation Complete ===");
        }

        /// <summary>
        /// Menu item to add the main scene to build settings.
        /// </summary>
        [MenuItem("Drawing Viewer/Add Scenes to Build")]
        public static void AddScenesToBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            var newScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);

            string mainScenePath = "Assets/_App/DrawingViewer/Scenes/MainScene.unity";

            if (!System.IO.File.Exists(mainScenePath))
            {
                Debug.LogError($"Main scene not found at: {mainScenePath}");
                return;
            }

            bool found = false;
            foreach (var scene in scenes)
            {
                if (scene.path == mainScenePath)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                newScenes.Add(new EditorBuildSettingsScene(mainScenePath, true));
                EditorBuildSettings.scenes = newScenes.ToArray();
                Debug.Log($"Added {mainScenePath} to build settings.");
            }
            else
            {
                Debug.Log("Main scene already in build settings.");
            }
        }
    }
}
#endif
