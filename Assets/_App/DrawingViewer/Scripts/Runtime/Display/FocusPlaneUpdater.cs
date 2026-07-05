using UnityEngine;
using UnityEngine.XR;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Updates the XR display focus plane to match the active drawing panel position.
    /// This reduces eye strain by helping the display optimize for the correct focal distance.
    /// </summary>
    [DefaultExecutionOrder(100)] // Run after panel updates
    public class FocusPlaneUpdater : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DrawingCanvasManager _canvasManager;

        [Header("Settings")]
        [SerializeField] private bool _enableAutoFocus = true;
        [SerializeField] private float _updateInterval = 0.1f; // seconds

        private XRDisplaySubsystem _displaySubsystem;
        private float _lastUpdateTime;

        /// <summary>
        /// Whether auto focus plane updating is enabled.
        /// </summary>
        public bool EnableAutoFocus
        {
            get => _enableAutoFocus;
            set => _enableAutoFocus = value;
        }

        private void Start()
        {
            _displaySubsystem = XREALUtility.GetLoadedSubsystem<XRDisplaySubsystem>();

            if (_displaySubsystem == null)
            {
                Debug.LogWarning("[FocusPlaneUpdater] No XRDisplaySubsystem found. Focus plane updates disabled.");
            }

            if (_canvasManager == null)
                _canvasManager = FindAnyObjectByType<DrawingCanvasManager>();
        }

        private void Update()
        {
            if (!_enableAutoFocus || _displaySubsystem == null)
                return;

            if (Time.time - _lastUpdateTime < _updateInterval)
                return;

            _lastUpdateTime = Time.time;
            UpdateFocusPlane();
        }

        /// <summary>
        /// Sets the focus plane to the active drawing panel position.
        /// </summary>
        private void UpdateFocusPlane()
        {
            var activePanel = _canvasManager?.ActivePanel;
            if (activePanel == null) return;

            Camera mainCamera = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (mainCamera == null) return;

            // Convert panel world position to camera-local space
            Vector3 panelWorldPos = activePanel.transform.position;
            Vector3 panelLocalPos = mainCamera.transform.InverseTransformPoint(panelWorldPos);

            // The focus plane should be at the depth of the panel (Z in camera space)
            // Normal points toward the camera from the panel
            Vector3 focusPoint = new Vector3(0, 0, panelLocalPos.z);

            // Set focus plane: point (in camera space), normal (toward camera), velocity (zero)
            _displaySubsystem.SetFocusPlane(
                focusPoint,
                Vector3.back,  // panel facing toward camera
                Vector3.zero   // no velocity
            );
        }

        /// <summary>
        /// Manually sets the focus plane to a specific world-space point.
        /// </summary>
        public void SetManualFocusPoint(Vector3 worldPoint, Vector3 worldNormal)
        {
            if (_displaySubsystem == null) return;

            Camera mainCamera = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (mainCamera == null) return;

            Vector3 localPoint = mainCamera.transform.InverseTransformPoint(worldPoint);
            Vector3 localNormal = mainCamera.transform.InverseTransformDirection(worldNormal);

            _displaySubsystem.SetFocusPlane(localPoint, localNormal, Vector3.zero);
        }

        private void OnDestroy()
        {
            _displaySubsystem = null;
        }
    }
}
