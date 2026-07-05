using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.XREAL;

namespace Unity.XR.XREAL.App.Core
{
    /// <summary>
    /// Top-level orchestrator that routes between launcher menu and feature modules.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public class AppShell : SingletonMonoBehaviour<AppShell>
    {
        private const string DrawingViewerObjectName = "DrawingViewerApp";
        private const string SwitchRecognitionObjectName = "SwitchRecognitionApp";

        [Header("Module Roots")]
        [SerializeField] private GameObject _drawingViewerRoot;
        [SerializeField] private GameObject _switchRecognitionRoot;

        [Header("Launcher")]
        [SerializeField] private ModeLauncherUI _launcherUI;

        private IAppModule _drawingModule;
        private IAppModule _switchRecognitionModule;

        public AppMode CurrentMode { get; private set; } = AppMode.Launcher;

        protected override void Awake()
        {
            base.Awake();
            CacheModules();
        }

private async void Start()
        {
            await WaitForMainCameraAsync();
            CacheModules();
            WireLauncherEvents();
            await ApplyTrackingForModeAsync(AppMode.Launcher);
            ReturnToLauncher();

#if UNITY_EDITOR
            // Async continuations can run after child Start(); refresh once the launcher is configured.
            await Task.Yield();
            RefreshLauncherPlacement();
#endif
        }

private void LateUpdate()
        {
            if (CurrentMode != AppMode.Launcher || _launcherUI == null)
                return;

            var manipulator = _launcherUI.GetComponent<WorldSpaceUiManipulator>();
            if (manipulator == null || !manipulator.ShouldFollowCamera)
                return;

            _launcherUI.PositionRelativeToCamera(ResolveMainCamera());
        }

public void RequestMode(AppMode mode)
        {
            if (mode == AppMode.Launcher)
            {
                ReturnToLauncher();
                return;
            }

            RequestModeAsync(mode);
        }

        private async void RequestModeAsync(AppMode mode)
        {
            _drawingModule?.ExitMode();
            _switchRecognitionModule?.ExitMode();
            _launcherUI?.Hide();

            await ApplyTrackingForModeAsync(mode);

            switch (mode)
            {
                case AppMode.DrawingViewer:
                    _drawingModule?.EnterMode();
                    break;
                case AppMode.SwitchRecognition:
                    _switchRecognitionModule?.EnterMode();
                    break;
            }

            CurrentMode = mode;
            Debug.Log($"[AppShell] Mode: {mode}");
        }

public void ReturnToLauncher()
        {
            ReturnToLauncherAsync();
        }

private async void ReturnToLauncherAsync()
        {
            _drawingModule?.ExitMode();
            _switchRecognitionModule?.ExitMode();

            CurrentMode = AppMode.Launcher;

            if (_launcherUI == null)
            {
                Debug.LogError("[AppShell] ModeLauncherUI not found. Run Drawing Viewer > Setup App Shell, or ensure AppShellRuntimeBootstrap ran.");
                return;
            }

            await ApplyTrackingForModeAsync(AppMode.Launcher);

            var manipulator = _launcherUI.GetComponent<WorldSpaceUiManipulator>();
            manipulator?.ResetHeadFollow();

            RefreshLauncherPlacement();
        }

private void RefreshLauncherPlacement()
        {
            if (_launcherUI == null || CurrentMode != AppMode.Launcher)
                return;

            var camera = ResolveMainCamera();
            WireWorldSpaceCanvases(camera);
            _launcherUI.Show();
            _launcherUI.PositionRelativeToCamera(camera);
        }


        private void WireLauncherEvents()
        {
            if (_launcherUI == null)
                return;

            _launcherUI.OnDrawingViewerSelected -= HandleDrawingViewerSelected;
            _launcherUI.OnSwitchRecognitionSelected -= HandleSwitchRecognitionSelected;
            _launcherUI.OnDrawingViewerSelected += HandleDrawingViewerSelected;
            _launcherUI.OnSwitchRecognitionSelected += HandleSwitchRecognitionSelected;
        }

        private void HandleDrawingViewerSelected()
        {
            RequestMode(AppMode.DrawingViewer);
        }

        private void HandleSwitchRecognitionSelected()
        {
            RequestMode(AppMode.SwitchRecognition);
        }

        private void CacheModules()
        {
            if (_drawingViewerRoot == null)
                _drawingViewerRoot = GameObject.Find(DrawingViewerObjectName);

            if (_switchRecognitionRoot == null)
                _switchRecognitionRoot = GameObject.Find(SwitchRecognitionObjectName);

            _drawingModule = ResolveModule(_drawingViewerRoot);
            _switchRecognitionModule = ResolveModule(_switchRecognitionRoot);

            if (_launcherUI == null)
                _launcherUI = GetComponentInChildren<ModeLauncherUI>(true);
        }

        private static IAppModule ResolveModule(GameObject root)
        {
            if (root == null)
                return null;

            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IAppModule module)
                    return module;
            }

            return null;
        }

        private static Camera ResolveMainCamera()
        {
            var tagged = GameObject.FindGameObjectWithTag("MainCamera");
            if (tagged != null && tagged.TryGetComponent(out Camera taggedCamera))
                return taggedCamera;

            return Camera.main;
        }

        private static async Task WaitForMainCameraAsync()
        {
            const int maxFrames = 120;
            for (int i = 0; i < maxFrames; i++)
            {
                if (ResolveMainCamera() != null)
                    return;

                await Task.Yield();
            }
        }

        private static void WireWorldSpaceCanvases(Camera camera)
        {
            if (camera == null)
                return;

            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.WorldSpace)
                    continue;

                canvas.worldCamera = camera;

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void OnDestroy()
        {
            if (_launcherUI == null)
                return;

            _launcherUI.OnDrawingViewerSelected -= HandleDrawingViewerSelected;
            _launcherUI.OnSwitchRecognitionSelected -= HandleSwitchRecognitionSelected;
        }
    

private static async Task ApplyTrackingForModeAsync(AppMode mode)
        {
#if UNITY_EDITOR
            await Task.CompletedTask;
#else
            TrackingType targetTracking = mode switch
            {
                AppMode.DrawingViewer => TrackingType.MODE_6DOF,
                AppMode.Launcher => TrackingType.MODE_3DOF,
                AppMode.SwitchRecognition => TrackingType.MODE_3DOF,
                _ => TrackingType.MODE_3DOF
            };

            var tcs = new TaskCompletionSource<bool>();
            await XREALPlugin.SwitchTrackingTypeAsync(targetTracking, (result, _) => tcs.TrySetResult(result));
            await tcs.Task;
#endif
        }
    }
}

