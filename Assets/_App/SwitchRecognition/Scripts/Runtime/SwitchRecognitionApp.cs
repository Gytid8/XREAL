using UnityEngine;
using Unity.XR.XREAL.App.Core;
using Unity.XR.XREAL.DrawingViewer;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// Switch recognition module orchestrator (phase 1: mock detector + UI shell).
    /// </summary>
    [DefaultExecutionOrder(-880)]
    public class SwitchRecognitionApp : MonoBehaviour, IAppModule
    {
        [Header("References")]
        [SerializeField] private SwitchRecognitionUI _ui;
        [SerializeField] private SwitchLabelManager _labelManager;
        [SerializeField] private SwitchClassDefinition _classDefinition;

        [Header("Mock Detection")]
        [SerializeField] private float _detectionIntervalSeconds = 0.35f;
        [SerializeField] private float _mockDistanceMeters = 1.2f;

        private ISwitchDetector _detector;
        private float _nextDetectionTime;
        private bool _moduleInitialized;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            if (_ui == null)
                _ui = GetComponentInChildren<SwitchRecognitionUI>(true);

            if (_labelManager == null)
                _labelManager = GetComponentInChildren<SwitchLabelManager>(true);

            if (_classDefinition == null)
                _classDefinition = Resources.Load<SwitchClassDefinition>("SwitchClassDefinition");

            _detector = new MockSwitchDetector(_classDefinition, _mockDistanceMeters);
            _labelManager?.SetClassDefinition(_classDefinition);
            _moduleInitialized = true;

            SetModuleVisible(false);
        }

        public void EnterMode()
        {
            if (IsActive)
                return;

            IsActive = true;
            SetModuleVisible(true);
            _ui?.SetStatusMessage(_detector?.StatusMessage ?? string.Empty);
            RunDetectionPass();
        }

        public void ExitMode()
        {
            if (!IsActive && _moduleInitialized)
            {
                SetModuleVisible(false);
                return;
            }

            IsActive = false;
            _labelManager?.ClearAll();
            SetModuleVisible(false);
        }

        private void Update()
        {
            if (!IsActive)
                return;

            if (Time.unscaledTime < _nextDetectionTime)
                return;

            _nextDetectionTime = Time.unscaledTime + _detectionIntervalSeconds;
            RunDetectionPass();
        }

        private void LateUpdate()
        {
            if (!IsActive || _ui == null)
                return;

            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            _ui.PositionRelativeToCamera(cam);
        }

        private void RunDetectionPass()
        {
            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (cam == null || _detector == null || _labelManager == null)
                return;

            var detections = _detector.Detect(null, cam);
            _labelManager.UpdateDetections(detections);
            _ui?.SetStatusMessage(_detector.StatusMessage);
        }

        private void SetModuleVisible(bool visible)
        {
            if (visible)
            {
                _ui?.Show();
                if (_labelManager != null)
                    _labelManager.gameObject.SetActive(true);
            }
            else
            {
                _ui?.Hide();
                if (_labelManager != null)
                    _labelManager.gameObject.SetActive(false);
            }
        }
    }
}
