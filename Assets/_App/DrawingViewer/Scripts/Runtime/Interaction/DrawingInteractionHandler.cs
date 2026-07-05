using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Routes input from all sources (controller, hands, UI) to the appropriate handler.
    /// Arbitrates conflicts between simultaneous input modalities.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DrawingInteractionHandler : SingletonMonoBehaviour<DrawingInteractionHandler>
    {
        [Header("Input Modules")]
        [SerializeField] private DrawingGestureController _gestureController;
        [SerializeField] private DrawingControllerInput _controllerInput;
        [SerializeField] private DrawingLaserController _laserController;
        [SerializeField] private PanelManipulator _panelManipulator;

        [Header("Interaction Settings")]
        [SerializeField] private float _gestureSuppressTime = 0.3f; // seconds to suppress gestures after controller input

        /// <summary>
        /// The current active input mode.
        /// </summary>
        public InputMode ActiveInputMode { get; private set; } = InputMode.Both;

        /// <summary>
        /// Timestamp of last controller input (used for gesture suppression).
        /// </summary>
        private float _lastControllerInputTime;

        /// <summary>
        /// Whether hand gestures are currently suppressed due to controller activity.
        /// </summary>
        private bool _gesturesSuppressed;

        /// <summary>
        /// Currently active drawing panel.
        /// </summary>
        private DrawingPanelController _activePanel;

        /// <summary>
        /// Event when the active input mode changes.
        /// </summary>
        public System.Action<InputMode> OnInputModeChanged;

        /// <summary>
        /// Available input modes.
        /// </summary>
        public enum InputMode
        {
            HandsOnly,
            ControllerOnly,
            Both,
            None
        }

        protected override void Awake()
        {
            base.Awake();

            // Auto-find components
            if (_gestureController == null)
                _gestureController = GetComponent<DrawingGestureController>();

            if (_controllerInput == null)
                _controllerInput = GetComponent<DrawingControllerInput>();

            if (_laserController == null)
                _laserController = GetComponent<DrawingLaserController>();

            if (_panelManipulator == null)
                _panelManipulator = GetComponent<PanelManipulator>();
        }

        private void Start()
        {
            var bootstrapper = AppBootstrapper.Singleton;
            if (bootstrapper != null)
            {
                bootstrapper.OnInputSourceChanged += OnInputSourceChanged;
            }
        }

        private void Update()
        {
            // Check if we should suppress gestures (after recent controller input)
            if (Time.time - _lastControllerInputTime > _gestureSuppressTime)
            {
                _gesturesSuppressed = false;
            }

            // Track the active panel for manipulation
            var canvasManager = DrawingViewerApp.Singleton?.GetComponent<DrawingCanvasManager>();
            if (canvasManager != null)
            {
                _activePanel = canvasManager.ActivePanel;
            }
        }

        private void OnInputSourceChanged(InputSource inputSource)
        {
            switch (inputSource)
            {
                case InputSource.Controller:
                    ActiveInputMode = InputMode.ControllerOnly;
                    break;
                case InputSource.Hands:
                    ActiveInputMode = InputMode.HandsOnly;
                    break;
                case InputSource.ControllerAndHands:
                    ActiveInputMode = InputMode.Both;
                    break;
                default:
                    ActiveInputMode = InputMode.Both;
                    break;
            }

            OnInputModeChanged?.Invoke(ActiveInputMode);
            Debug.Log($"[DrawingInteractionHandler] Input mode: {ActiveInputMode}");
        }

        /// <summary>
        /// Called when controller input is detected (button press, touchpad).
        /// Suppresses hand gestures briefly to prevent conflicts.
        /// </summary>
        public void NotifyControllerInput()
        {
            _lastControllerInputTime = Time.time;
            _gesturesSuppressed = true;
        }

        /// <summary>
        /// Called when a hand gesture is detected.
        /// Returns true if the gesture should be processed (not suppressed).
        /// </summary>
        public bool CanProcessGesture()
        {
            if (ActiveInputMode == InputMode.ControllerOnly)
                return false;

            if (ActiveInputMode == InputMode.HandsOnly)
                return true;

            // Both: only process if gestures aren't suppressed by recent controller input
            return !_gesturesSuppressed;
        }

        /// <summary>
        /// Called when a controller action is detected.
        /// Returns true if the controller action should be processed.
        /// </summary>
        public bool CanProcessController()
        {
            if (ActiveInputMode == InputMode.HandsOnly)
                return false;

            return true;
        }

        /// <summary>
        /// Handles a zoom action from any input source.
        /// </summary>
        public void HandleZoom(float zoomDelta)
        {
            if (_activePanel != null)
            {
                _activePanel.ApplyPinchZoom(zoomDelta);
            }
        }

/// <summary>
        /// Applies a discrete zoom step from toolbar buttons (one click = one step).
        /// </summary>
        public void HandleZoomStep(float step)
        {
            if (_activePanel != null)
                _activePanel.ApplyZoomStep(step);
        }


        /// <summary>
        /// Handles a drag/pan action from any input source.
        /// </summary>
        public void HandleDrag(Vector2 delta)
        {
            if (_activePanel != null)
            {
                _activePanel.ApplyDrag(delta);
            }
        }

        /// <summary>
        /// Handles a rotation action from any input source.
        /// </summary>
        public void HandleRotate(float deltaAngle)
        {
            if (_activePanel != null)
            {
                _activePanel.ApplyTwoHandRotation(deltaAngle);
            }
        }

        /// <summary>
        /// Rotates the active panel by a fixed step (toolbar buttons / keyboard).
        /// </summary>
        public void HandleRotateStep(float degrees)
        {
            if (_activePanel != null)
                _activePanel.ApplyRotationDegrees(degrees);
        }

        /// <summary>
        /// Rotates the active panel left by the configured step size.
        /// </summary>
        public void HandleRotateLeftStep()
        {
            float step = DrawingViewerApp.Singleton?.Settings?.PanelRotationStepDegrees ?? 15f;
            HandleRotateStep(-step);
        }

        /// <summary>
        /// Rotates the active panel right by the configured step size.
        /// </summary>
        public void HandleRotateRightStep()
        {
            float step = DrawingViewerApp.Singleton?.Settings?.PanelRotationStepDegrees ?? 15f;
            HandleRotateStep(step);
        }

        /// <summary>
        /// Handles a page navigation request.
        /// </summary>
        public void HandlePageNavigation(int direction)
        {
            var app = DrawingViewerApp.Singleton;
            if (app == null) return;

            if (direction > 0)
                app.NextPage();
            else if (direction < 0)
                app.PreviousPage();
        }

        private new void OnDestroy()
        {
            var bootstrapper = AppBootstrapper.Singleton;
            if (bootstrapper != null)
            {
                bootstrapper.OnInputSourceChanged -= OnInputSourceChanged;
            }
            base.OnDestroy();
        }
    }
}
