using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using Unity.XR.XREAL;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Initializes the XREAL platform on app start.
    /// Must execute before all other app scripts.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AppBootstrapper : SingletonMonoBehaviour<AppBootstrapper>
    {
        [Header("XR Components")]
        [SerializeField] private XROrigin _xrOrigin;
        [SerializeField] private ARSession _arSession;
        [SerializeField] private ARInputManager _arInputManager;

        [Header("Tracking Settings")]
        [SerializeField] private TrackingType _defaultTrackingType = TrackingType.MODE_6DOF;
        [SerializeField] private InputSource _defaultInputSource = InputSource.ControllerAndHands;
        [SerializeField] private int _targetFrameRate = 60;

        /// <summary>
        /// Gets the XR Origin component for coordinate space transformations.
        /// </summary>
        public XROrigin XROrigin => _xrOrigin;

        /// <summary>
        /// Gets the AR Session component.
        /// </summary>
        public ARSession ARSession => _arSession;

        /// <summary>
        /// Gets the main camera via XREALUtility (never use Camera.main in XR).
        /// </summary>
        public Camera MainCamera => DrawingViewerCamera.MainCamera;

        /// <summary>
        /// Gets the current tracking type.
        /// </summary>
        public TrackingType CurrentTrackingType { get; private set; }

        /// <summary>
        /// Gets the current input source.
        /// </summary>
        public InputSource CurrentInputSource { get; private set; }

        /// <summary>
        /// Event raised when the tracking type changes.
        /// </summary>
        public event Action<TrackingType> OnTrackingTypeChanged;

        /// <summary>
        /// Event raised when the input source changes.
        /// </summary>
        public event Action<InputSource> OnInputSourceChanged;

        /// <summary>
        /// Event raised when bootstrapper initialization is complete.
        /// </summary>
        public event Action OnInitialized;

        /// <summary>
        /// Whether the bootstrapper has completed initialization.
        /// </summary>
        public bool IsInitialized { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            // Auto-find components if not assigned
            if (_xrOrigin == null)
                _xrOrigin = GetComponentInChildren<XROrigin>(true);

            if (_arSession == null)
                _arSession = GetComponent<ARSession>();

            if (_arInputManager == null)
                _arInputManager = GetComponentInChildren<ARInputManager>(true) ?? GetComponent<ARInputManager>();

#if UNITY_EDITOR
            DisableEditorOnlyARComponents();
            EnsureEditorUIInput();
#endif

            // Set initial input source
            XREALPlugin.SetInputSource(_defaultInputSource);
            CurrentInputSource = _defaultInputSource;

            // Subscribe to XREAL plugin events
            XREALPlugin.OnTrackingTypeChanged += OnXREALTrackingTypeChanged;

            Debug.Log($"[AppBootstrapper] Awake complete. InputSource={_defaultInputSource}");
        }

        private async void Start()
        {
            Application.targetFrameRate = _targetFrameRate;

#if UNITY_EDITOR
            // In Editor: skip AR/tracking init, immediately signal ready
            Debug.Log("[AppBootstrapper] Editor mode — skipping AR initialization.");

            // Attach Editor camera controller for mouse/keyboard navigation
            var mainCam = MainCamera;
            if (mainCam != null && mainCam.GetComponent<EditorCameraController>() == null)
            {
                mainCam.transform.position = new Vector3(0f, 1.6f, 0f);
                mainCam.transform.rotation = Quaternion.identity;
                mainCam.gameObject.AddComponent<EditorCameraController>();
                Debug.Log("[AppBootstrapper] EditorCameraController attached.");
            }

            // Disable AR camera components that interfere with Editor control
            var arBg = mainCam?.GetComponent<ARCameraBackground>();
            if (arBg != null) arBg.enabled = false;

            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            }

            // TrackedPoseDriver fights EditorCameraController every frame
            var trackedPose = mainCam?.GetComponent("TrackedPoseDriver") as MonoBehaviour;
            if (trackedPose != null) trackedPose.enabled = false;

            IsInitialized = true;
            OnInitialized?.Invoke();
            CurrentTrackingType = TrackingType.MODE_3DOF;
#else
            await InitializeTrackingAsync();
            await InitializeARSessionAsync();

            IsInitialized = true;
            OnInitialized?.Invoke();
#endif

            Debug.Log($"[AppBootstrapper] Initialization complete. TrackingType={CurrentTrackingType}");

            EnsureXRUIInput();
#if UNITY_EDITOR
            EnsureEditorUIInput();
#endif
        }

        private void EnsureXRUIInput()
        {
#if UNITY_EDITOR
            return;
#else
            var eventSystem = EventSystem.current ?? FindObjectOfType<EventSystem>();
            if (eventSystem == null)
                return;

            var xrUiModule = eventSystem.GetComponent<XRUIInputModule>();
            if (xrUiModule == null)
                xrUiModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
            xrUiModule.enabled = true;

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
                standalone.enabled = false;

            WireAllWorldSpaceCanvases(MainCamera);
#endif
        }

        private static void WireAllWorldSpaceCanvases(Camera camera)
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

        private async Task InitializeTrackingAsync()
        {
            // Check if 6DOF is supported before switching
            if (_defaultTrackingType == TrackingType.MODE_6DOF)
            {
                bool supports6DOF = XREALPlugin.IsHMDFeatureSupported(
                    XREALSupportedFeature.XREAL_FEATURE_PERCEPTION_HEAD_TRACKING_POSITION);

                if (!supports6DOF)
                {
                    Debug.LogWarning("[AppBootstrapper] 6DOF not supported on this device, falling back to 3DOF.");
                    _defaultTrackingType = TrackingType.MODE_3DOF;
                }
            }

            var tcs = new TaskCompletionSource<bool>();

            await XREALPlugin.SwitchTrackingTypeAsync(_defaultTrackingType, (result, targetType) =>
            {
                if (result)
                {
                    CurrentTrackingType = targetType;
                    Debug.Log($"[AppBootstrapper] Tracking switched to {targetType}");
                }
                else
                {
                    Debug.LogError($"[AppBootstrapper] Failed to switch tracking to {targetType}");
                }
                tcs.TrySetResult(result);
            });

            await tcs.Task;
        }

        private async Task InitializeARSessionAsync()
        {
            if (_arSession == null)
                return;

            // Wait a frame for AR Session to start
            await Task.Yield();

            // AR Session should auto-start via ARSession component
            // Just verify it's in a valid state
            Debug.Log($"[AppBootstrapper] AR Session state: {ARSession.state}");
        }

        private void OnXREALTrackingTypeChanged(bool result, TrackingType targetTrackingType)
        {
            if (result)
            {
                CurrentTrackingType = targetTrackingType;
                OnTrackingTypeChanged?.Invoke(targetTrackingType);
            }
        }

        /// <summary>
        /// Switches the input source (Controller, Hands, or Both).
        /// </summary>
        public void SwitchInputSource(InputSource inputSource)
        {
            XREALPlugin.SetInputSource(inputSource);
            CurrentInputSource = inputSource;
            OnInputSourceChanged?.Invoke(inputSource);
            Debug.Log($"[AppBootstrapper] Input source switched to {inputSource}");
        }

        /// <summary>
        /// Switches the tracking type asynchronously.
        /// </summary>
        public async Task<bool> SwitchTrackingType(TrackingType trackingType)
        {
            var tcs = new TaskCompletionSource<bool>();

            await XREALPlugin.SwitchTrackingTypeAsync(trackingType, (result, targetType) =>
            {
                if (result)
                {
                    CurrentTrackingType = targetType;
                    OnTrackingTypeChanged?.Invoke(targetType);
                }
                tcs.TrySetResult(result);
            });

            return await tcs.Task;
        }

        /// <summary>
        /// Recenters the controller orientation.
        /// </summary>
        public void RecenterController()
        {
            XREALPlugin.RecenterController();
        }

        protected override void OnDestroy()
        {
            XREALPlugin.OnTrackingTypeChanged -= OnXREALTrackingTypeChanged;
            base.OnDestroy();
        }

#if UNITY_EDITOR
        private void DisableEditorOnlyARComponents()
        {
            if (_arSession != null) _arSession.enabled = false;
            if (_arInputManager != null) _arInputManager.enabled = false;

            var arPlaneManager = GetComponent<ARPlaneManager>();
            if (arPlaneManager != null) arPlaneManager.enabled = false;

            var arCameraManager = MainCamera?.GetComponent<ARCameraManager>();
            if (arCameraManager != null) arCameraManager.enabled = false;
        }

        private void EnsureEditorUIInput()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
                eventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null) return;

            var standalone = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone == null)
                standalone = eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            standalone.enabled = true;
            standalone.forceModuleActive = true;

            var xrUiModule = eventSystem.GetComponent<XRUIInputModule>();
            if (xrUiModule != null)
                xrUiModule.enabled = false;

            WireAllWorldSpaceCanvases(MainCamera);
        }
#endif
    }

    /// <summary>
    /// Resolves the active app camera. XREAL is preferred on device; Unity camera lookup
    /// keeps Editor play mode usable when the XREAL runtime has not initialized a camera.
    /// </summary>
    public static class DrawingViewerCamera
    {
        public static Camera MainCamera
        {
            get
            {
                Camera camera = XREALUtility.MainCamera;
                if (camera != null) return camera;

                camera = Camera.main;
                if (camera != null) return camera;

                return UnityEngine.Object.FindObjectOfType<Camera>();
            }
        }
    }
}
