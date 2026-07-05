using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Controls a single drawing panel's position, scale, and rotation.
    /// Provides smooth interpolation for all transformations.
    /// </summary>
    [RequireComponent(typeof(DrawingPageDisplay))]
    public class DrawingPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DrawingPageDisplay _pageDisplay;

        [Header("Transform Targets")]
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _targetScale = 1f;

        [Header("State")]
        private bool _isBeingManipulated;
        private float _currentScaleVelocity;
        private Vector3 _positionVelocity = Vector3.zero;
        private float _rotationVelocity;

        /// <summary>
        /// The page display component.
        /// </summary>
        public DrawingPageDisplay PageDisplay
        {
            get
            {
                if (_pageDisplay == null)
                    _pageDisplay = GetComponent<DrawingPageDisplay>();
                return _pageDisplay;
            }
        }

        /// <summary>
        /// Whether this panel is currently being manipulated (grab, pinch).
        /// When true, smooth interpolation is reduced for responsiveness.
        /// </summary>
        public bool IsBeingManipulated
        {
            get => _isBeingManipulated;
            set => _isBeingManipulated = value;
        }

        /// <summary>
        /// Current zoom scale (1.0 = default size).
        /// </summary>
        public float CurrentScale => _targetScale;

        /// <summary>
        /// Actual zoom multiplier derived from the current transform.
        /// </summary>
        public float GetVisualScaleMultiplier()
        {
            if (PageDisplay == null || PageDisplay.PanelWidth <= 0f)
                return _targetScale;

            return transform.localScale.x / PageDisplay.PanelWidth;
        }

        private bool _startInitialized;

        private void Awake()
        {
            if (_pageDisplay == null)
                _pageDisplay = GetComponent<DrawingPageDisplay>();
        }

        private void Start()
        {
            if (_startInitialized)
                return;

            _startInitialized = true;
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;

            if (PageDisplay != null && PageDisplay.PanelWidth > 0f)
                _targetScale = GetVisualScaleMultiplier();
            else
                _targetScale = 1f;
        }

        private void LateUpdate()
        {
            if (PageDisplay == null || PageDisplay.PanelWidth <= 0f)
                return;

            // Apply smooth interpolation when not being manipulated
            if (!_isBeingManipulated)
            {
                var settings = DrawingViewerApp.Singleton?.Settings;
                float damping = settings != null ? settings.PanelSmoothDamping : 8f;

                transform.position = Vector3.SmoothDamp(
                    transform.position, _targetPosition,
                    ref _positionVelocity, 1f / damping);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation, _targetRotation,
                    Time.deltaTime * damping);

                // Smooth scale
                float currentScale = transform.localScale.x / PageDisplay.PanelWidth;
                float newScale = Mathf.SmoothDamp(
                    currentScale, _targetScale,
                    ref _currentScaleVelocity, 1f / damping);

                PageDisplay.SetScale(PageDisplay.PanelWidth * newScale);
            }
            else
            {
                // Direct update during manipulation for responsiveness
                transform.position = _targetPosition;
                transform.rotation = _targetRotation;
            }
        }

        /// <summary>
        /// Resets the panel to its default position in front of the camera.
        /// The panel is placed in world space and does not follow head movement afterward.
        /// </summary>
        public void ResetToDefaultView(Camera relativeToCamera)
        {
            if (relativeToCamera == null)
                relativeToCamera = DrawingViewerCamera.MainCamera;

            if (relativeToCamera == null) return;

            var settings = DrawingViewerApp.Singleton?.Settings;
            float distance = settings != null ? settings.PanelDefaultDistance : 2f;
            float verticalOffset = settings != null ? settings.PanelVerticalOffset : -0.2f;

            Transform camTransform = relativeToCamera.transform;
            Vector3 cameraPos = camTransform.position;
            Vector3 forward = camTransform.forward.normalized;
            Vector3 up = camTransform.up;

#if UNITY_EDITOR
            // Editor XR rig often starts at floor height before the user moves the camera.
            if (cameraPos.y < 0.5f)
                cameraPos += Vector3.up * 1.6f;
#endif

            // Place relative to the camera view axis so the panel stays in frame even when
            // the camera is tilted or at different heights between play sessions.
            _targetPosition = cameraPos + forward * distance + up * verticalOffset;
            _targetRotation = Quaternion.LookRotation(-forward, Vector3.up);

            _targetScale = 1f;
            ApplyTargetTransformImmediate();
        }

        /// <summary>
        /// Snapshot of panel transform used to preserve user adjustments across page changes.
        /// </summary>
        public struct ViewState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public bool IsValid;

            public static ViewState Invalid => new ViewState { IsValid = false };
        }

        /// <summary>
        /// Captures the current target transform for later reuse.
        /// </summary>
        public ViewState CaptureViewState()
        {
            return new ViewState
            {
                Position = _targetPosition,
                Rotation = _targetRotation,
                Scale = GetVisualScaleMultiplier(),
                IsValid = true
            };
        }

        /// <summary>
        /// Restores a previously captured view state.
        /// </summary>
        public void ApplyViewState(ViewState state)
        {
            if (!state.IsValid)
                return;

            _targetPosition = state.Position;
            _targetRotation = state.Rotation;
            _targetScale = state.Scale;
            ApplyTargetTransformImmediate();
        }

        /// <summary>
        /// Copies another panel's world transform so page switches keep the same placement.
        /// </summary>
        public void CopyWorldTransformFrom(DrawingPanelController source)
        {
            if (source == null) return;

            _targetPosition = source.transform.position;
            _targetRotation = source.transform.rotation;
            _targetScale = source.GetVisualScaleMultiplier();
            ApplyTargetTransformImmediate();
        }

        /// <summary>
        /// Snaps the panel to its target transform immediately.
        /// </summary>
        public void SnapToTargets()
        {
            ApplyTargetTransformImmediate();
        }

        private void ApplyTargetTransformImmediate()
        {
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
            _positionVelocity = Vector3.zero;
            _currentScaleVelocity = 0f;
            PageDisplay.SetScale(PageDisplay.PanelWidth * _targetScale);
        }

        /// <summary>
        /// Sets the target position directly.
        /// </summary>
        public void SetTargetPosition(Vector3 position)
        {
            _targetPosition = position;
        }

        /// <summary>
        /// Sets the target rotation directly.
        /// </summary>
        public void SetTargetRotation(Quaternion rotation)
        {
            _targetRotation = rotation;
        }

        /// <summary>
        /// Sets the zoom factor (clamped to settings range).
        /// </summary>
        public void SetZoom(float scale)
        {
            var settings = DrawingViewerApp.Singleton?.Settings;
            float min = settings != null ? settings.MinZoomScale : 0.2f;
            float max = settings != null ? settings.MaxZoomScale : 5f;
            _targetScale = Mathf.Clamp(scale, min, max);
        }

        /// <summary>
        /// Applies a discrete zoom step (e.g. toolbar +/- buttons). One click = one step.
        /// </summary>
        public void ApplyZoomStep(float step)
        {
            SetZoom(_targetScale + step);
        }

        /// <summary>
        /// Applies a pinch zoom delta.
        /// </summary>
        public void ApplyPinchZoom(float delta)
        {
            var settings = DrawingViewerApp.Singleton?.Settings;
            float speed = settings != null ? settings.PinchZoomSpeed : 0.005f;
            SetZoom(_targetScale + delta * speed);
        }

        /// <summary>
        /// Applies a drag delta to the target position.
        /// </summary>
        public void ApplyDrag(Vector2 delta)
        {
            var settings = DrawingViewerApp.Singleton?.Settings;
            float speed = settings != null ? settings.DragSpeed : 1f;

            Vector3 worldDelta = transform.right * delta.x + transform.up * delta.y;
            _targetPosition += worldDelta * speed * Time.deltaTime;
        }

        /// <summary>
        /// Applies a two-hand rotation angle.
        /// </summary>
        public void ApplyTwoHandRotation(float deltaAngle)
        {
            var settings = DrawingViewerApp.Singleton?.Settings;
            float speed = settings != null ? settings.TwoHandRotationSpeed : 1f;
            ApplyRotationDegrees(deltaAngle * speed);
        }

        /// <summary>
        /// Rotates the panel in-plane by an exact number of degrees.
        /// </summary>
        public void ApplyRotationDegrees(float degrees)
        {
            _targetRotation *= Quaternion.Euler(0f, 0f, degrees);
        }

        /// <summary>
        /// Moves the panel by a touchpad scroll amount.
        /// </summary>
        public void ApplyTouchpadScroll(Vector2 scrollDelta)
        {
            var settings = DrawingViewerApp.Singleton?.Settings;
            float speed = settings != null ? settings.TouchpadScrollSpeed : 0.01f;

            // Vertical scroll = zoom
            ApplyPinchZoom(scrollDelta.y * speed);

            // Horizontal scroll = pan X
            Vector3 horizontalDelta = transform.right * scrollDelta.x * speed;
            _targetPosition += horizontalDelta;
        }

        /// <summary>
        /// Positions the panel at a specific world position.
        /// </summary>
        public void PlaceAt(Vector3 worldPosition)
        {
            _targetPosition = worldPosition;
        }

        /// <summary>
        /// Makes the panel face a specific world position.
        /// </summary>
        public void LookAt(Vector3 worldPosition, bool keepUpright = true)
        {
            Vector3 direction = worldPosition - transform.position;

            if (keepUpright)
            {
                direction = Vector3.ProjectOnPlane(direction, Vector3.up);
                if (direction == Vector3.zero) return;
            }

            _targetRotation = Quaternion.LookRotation(-direction, Vector3.up);
        }

        /// <summary>
        /// Gets the position in front of this panel (for stacking).
        /// </summary>
        public Vector3 GetFrontPosition(float offset = 0.1f)
        {
            return _targetPosition + transform.forward * offset;
        }
    }
}
