using UnityEngine;
using Unity.XR.XREAL.App.Core;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Transforms the drawing panel based on input from controller or hand gestures.
    /// Handles grab-drag, pinch-zoom, and two-hand rotation operations.
    /// </summary>
    public class PanelManipulator : MonoBehaviour
    {
        [Header("Manipulation Settings")]
        [SerializeField] private float _minScale = 0.2f;
        [SerializeField] private float _maxScale = 5f;
        [SerializeField] private float _zoomSpeed = 0.005f;
        [SerializeField] private float _rotationSpeed = 1f;

        /// <summary>
        /// The panel currently being manipulated.
        /// </summary>
        private DrawingPanelController _activePanel;

        /// <summary>
        /// Offset for drag (position at grab start relative to panel center).
        /// </summary>
        private Vector3 _grabOffset;

        /// <summary>
        /// Scale at the start of a pinch operation.
        /// </summary>
        private float _initialPinchScale;

        /// <summary>
        /// Distance between hands at the start of a two-hand pinch.
        /// </summary>
        private float _initialPinchDistance;

        /// <summary>
        /// Rotation at the start of a two-hand rotation.
        /// </summary>
        private float _initialRotationAngle;

        private WorldSpaceUiManipulator _activeUiManipulator;

        private void Start()
        {
            var handler = DrawingInteractionHandler.Singleton;
            if (handler == null) return;

            var gestureController = handler.GetComponent<DrawingGestureController>();
            if (gestureController != null)
            {
                gestureController.OnPinchDragStarted += OnGrabStart;
                gestureController.OnPinchDragUpdated += OnGrabDrag;
                gestureController.OnPinchDragEnded += OnGrabEnd;
                gestureController.OnTwoHandPinchZoom += OnTwoHandPinchZoom;
                gestureController.OnTwoHandRotation += OnTwoHandRotate;
            }

            var settings = DrawingViewerApp.Singleton?.Settings;
            if (settings != null)
            {
                _minScale = settings.MinZoomScale;
                _maxScale = settings.MaxZoomScale;
                _zoomSpeed = settings.PinchZoomSpeed;
            }
        }

        private void Update()
        {
            // Get the currently active panel
            var canvasManager = DrawingViewerApp.Singleton?.GetComponent<DrawingCanvasManager>();
            _activePanel = canvasManager?.ActivePanel;
        }

        /// <summary>
        /// Called when a grab/pinch-drag starts.
        /// </summary>
        public void OnGrabStart(Vector3 handPosition)
        {
            if (WorldSpaceUiManipulator.TryFindGrabTarget(handPosition, out WorldSpaceUiManipulator uiManipulator))
            {
                _activeUiManipulator = uiManipulator;
                uiManipulator.BeginGrab(handPosition);
                return;
            }

            if (_activePanel == null)
                return;

            _activePanel.IsBeingManipulated = true;
            _grabOffset = _activePanel.transform.position - handPosition;
        }

        /// <summary>
        /// Called during a grab/pinch-drag movement.
        /// </summary>
        public void OnGrabDrag(Vector3 handPosition)
        {
            if (_activeUiManipulator != null)
            {
                _activeUiManipulator.DragToWorldPoint(handPosition);
                return;
            }

            if (_activePanel == null)
                return;

            Vector3 targetPosition = handPosition + _grabOffset;
            _activePanel.SetTargetPosition(targetPosition);
        }

        /// <summary>
        /// Called when a grab/pinch-drag ends.
        /// </summary>
        public void OnGrabEnd()
        {
            if (_activeUiManipulator != null)
            {
                _activeUiManipulator.EndGrab();
                _activeUiManipulator = null;
                return;
            }

            if (_activePanel != null)
            {
                _activePanel.IsBeingManipulated = false;
            }
        }

        /// <summary>
        /// Called when a two-hand pinch-to-zoom gesture is detected.
        /// </summary>
        public void OnTwoHandPinchZoom(float scaleDelta)
        {
            if (_activePanel != null)
            {
                _activePanel.ApplyPinchZoom(scaleDelta);
            }
        }

        /// <summary>
        /// Called when a two-hand rotation gesture is detected.
        /// </summary>
        public void OnTwoHandRotate(float deltaAngle)
        {
            if (_activePanel != null)
            {
                _activePanel.ApplyTwoHandRotation(deltaAngle);
            }
        }

        /// <summary>
        /// Starts a two-hand pinch operation (stores initial state).
        /// </summary>
        public void OnTwoHandPinchStart(float distance)
        {
            _initialPinchDistance = distance;
            if (_activePanel != null)
            {
                _initialPinchScale = _activePanel.CurrentScale;
            }
        }

        /// <summary>
        /// Updates a two-hand pinch operation.
        /// </summary>
        public void OnTwoHandPinchUpdate(float distance)
        {
            if (_activePanel == null || _initialPinchDistance <= 0) return;

            float scaleDelta = distance / _initialPinchDistance;
            float newScale = _initialPinchScale * scaleDelta;
            newScale = Mathf.Clamp(newScale, _minScale, _maxScale);
            _activePanel.SetZoom(newScale);
        }

        /// <summary>
        /// Starts a two-hand rotation operation.
        /// </summary>
        public void OnTwoHandRotateStart(float angle)
        {
            _initialRotationAngle = angle;
        }

        /// <summary>
        /// Updates a two-hand rotation operation.
        /// </summary>
        public void OnTwoHandRotateUpdate(float angle)
        {
            if (_activePanel == null) return;

            float deltaAngle = angle - _initialRotationAngle;
            _activePanel.ApplyTwoHandRotation(deltaAngle * _rotationSpeed);
        }

        /// <summary>
        /// Resets panel to default position in front of the camera.
        /// </summary>
        public void ResetPanelToDefault()
        {
            if (_activePanel == null) return;

            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            _activePanel.ResetToDefaultView(cam);
        }

        private void OnDestroy()
        {
            var handler = DrawingInteractionHandler.Singleton;
            if (handler != null)
            {
                var gestureController = handler.GetComponent<DrawingGestureController>();
                if (gestureController != null)
                {
                    gestureController.OnPinchDragStarted -= OnGrabStart;
                    gestureController.OnPinchDragUpdated -= OnGrabDrag;
                    gestureController.OnPinchDragEnded -= OnGrabEnd;
                    gestureController.OnTwoHandPinchZoom -= OnTwoHandPinchZoom;
                    gestureController.OnTwoHandRotation -= OnTwoHandRotate;
                }
            }
        }
    }
}
