using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Hands;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Detects and processes hand gestures for drawing interaction.
    /// Uses XR Hand subsystem for pinch, poke, and two-hand gestures.
    /// </summary>
    public class DrawingGestureController : MonoBehaviour
    {
        [Header("Gesture Settings")]
        [SerializeField] private float _pinchThresholdMeters = 0.035f;
        [SerializeField] private float _pinchZoomSensitivity = 0.01f;
        [SerializeField] private float _rotationSensitivity = 1f;
        [SerializeField] private float _pokeMaxDistanceMeters = 0.12f;
        [SerializeField] private float _pokeUiMaxDistanceMeters = 1.5f;

        [Header("Two-Hand Gesture")]
        [SerializeField] private float _minTwoHandDistance = 0.1f;

        public bool IsHandTrackingActive { get; private set; }
        public Vector3? RightPinchPosition { get; private set; }
        public Vector3? LeftPinchPosition { get; private set; }
        public bool IsRightPinching { get; private set; }
        public bool IsLeftPinching { get; private set; }

        public System.Action<Vector3> OnPinchDragStarted;
        public System.Action<Vector3> OnPinchDragUpdated;
        public System.Action OnPinchDragEnded;
        public System.Action<float> OnTwoHandPinchZoom;
        public System.Action<float> OnTwoHandRotation;
        public System.Action<GameObject> OnPokeDetected;
        public System.Action OnMenuGesture;

        private Vector3? _previousRightPinchPos;
        private Vector3? _previousLeftPinchPos;
        private float _previousTwoHandDistance;
        private bool _wasTwoHandPinching;
        private bool _wasRightPinching;
        private bool _wasPoking;
        private Vector3 _smoothedRightPinch;
        private Vector3 _smoothedLeftPinch;
        private float _handSmoothing = 0.35f;

        private void Start()
        {
            var bootstrapper = AppBootstrapper.Singleton;
            if (bootstrapper != null)
            {
                bootstrapper.OnInputSourceChanged += OnInputSourceChanged;
                IsHandTrackingActive = bootstrapper.CurrentInputSource == InputSource.Hands ||
                                       bootstrapper.CurrentInputSource == InputSource.ControllerAndHands;
            }

            var settings = DrawingViewerApp.Singleton?.Settings;
            if (settings != null)
                _handSmoothing = settings.HandJitterSmoothing;
        }

        private void Update()
        {
            var handler = DrawingInteractionHandler.Singleton;
            if (handler == null || !handler.CanProcessGesture())
                return;

            if (!IsHandTrackingActive)
                return;

            UpdateHandStates();
            ProcessPinchDrag(handler);
            ProcessTwoHandGestures(handler);
            ProcessPokeGesture();
        }

        private void UpdateHandStates()
        {
            IsRightPinching = HandTrackingUtility.TryGetPinchMidpoint(
                Handedness.Right, _pinchThresholdMeters, out Vector3 rightMid, out _);
            IsLeftPinching = HandTrackingUtility.TryGetPinchMidpoint(
                Handedness.Left, _pinchThresholdMeters, out Vector3 leftMid, out _);

            if (IsRightPinching)
            {
                _smoothedRightPinch = HandTrackingUtility.SmoothPosition(_smoothedRightPinch, rightMid, _handSmoothing);
                RightPinchPosition = _smoothedRightPinch;
            }
            else
            {
                RightPinchPosition = null;
            }

            if (IsLeftPinching)
            {
                _smoothedLeftPinch = HandTrackingUtility.SmoothPosition(_smoothedLeftPinch, leftMid, _handSmoothing);
                LeftPinchPosition = _smoothedLeftPinch;
            }
            else
            {
                LeftPinchPosition = null;
            }
        }

        private void ProcessPinchDrag(DrawingInteractionHandler handler)
        {
            if (IsRightPinching && RightPinchPosition.HasValue)
            {
                if (!_wasRightPinching)
                {
                    OnPinchDragStarted?.Invoke(RightPinchPosition.Value);
                }
                else if (_previousRightPinchPos.HasValue)
                {
                    handler.NotifyControllerInput();
                    OnPinchDragUpdated?.Invoke(RightPinchPosition.Value);
                }
            }
            else if (_wasRightPinching)
            {
                OnPinchDragEnded?.Invoke();
            }

            _wasRightPinching = IsRightPinching;
            _previousRightPinchPos = RightPinchPosition;
        }

        private void ProcessTwoHandGestures(DrawingInteractionHandler handler)
        {
            bool isTwoHandPinching = IsRightPinching && IsLeftPinching &&
                                     RightPinchPosition.HasValue && LeftPinchPosition.HasValue;

            if (isTwoHandPinching)
            {
                handler.NotifyControllerInput();

                float distance = Vector3.Distance(RightPinchPosition.Value, LeftPinchPosition.Value);

                if (_wasTwoHandPinching && distance > _minTwoHandDistance)
                {
                    float distanceDelta = distance - _previousTwoHandDistance;
                    float zoomFactor = distanceDelta * _pinchZoomSensitivity;
                    handler.HandleZoom(zoomFactor);
                    OnTwoHandPinchZoom?.Invoke(zoomFactor);

                    if (_previousRightPinchPos.HasValue && _previousLeftPinchPos.HasValue)
                    {
                        Vector3 currentDirection = (RightPinchPosition.Value - LeftPinchPosition.Value).normalized;
                        Vector3 previousDirection = (_previousRightPinchPos.Value - _previousLeftPinchPos.Value).normalized;
                        float angleDelta = Vector3.SignedAngle(previousDirection, currentDirection, Vector3.forward);
                        handler.HandleRotate(angleDelta * _rotationSensitivity);
                        OnTwoHandRotation?.Invoke(angleDelta);
                    }
                }

                _previousTwoHandDistance = distance;
            }

            _wasTwoHandPinching = isTwoHandPinching;
            _previousLeftPinchPos = LeftPinchPosition;
        }

        private void ProcessPokeGesture()
        {
            if (IsRightPinching)
            {
                _wasPoking = false;
                return;
            }

            if (!HandTrackingUtility.TryGetPokePose(Handedness.Right, out Pose pokePose))
            {
                _wasPoking = false;
                return;
            }

            Vector3 tip = pokePose.position;
            Vector3 pokeDirection = pokePose.forward;
            Camera cam = DrawingViewerCamera.MainCamera;

            if (pokeDirection.sqrMagnitude < 0.0001f)
                pokeDirection = cam != null ? (cam.transform.position - tip).normalized : Vector3.forward;

            if (TryPokeUi(tip, pokeDirection))
                return;

            if (cam != null)
            {
                Vector3 towardCamera = (cam.transform.position - tip).normalized;
                if (TryPokeUi(tip, towardCamera))
                    return;
            }

            var app = DrawingViewerApp.Singleton;
            if (app == null || !app.IsActive)
            {
                _wasPoking = false;
                return;
            }

            if (!Physics.Raycast(tip, pokeDirection, out RaycastHit hit, _pokeMaxDistanceMeters))
            {
                _wasPoking = false;
                return;
            }

            if (!_wasPoking && hit.collider != null)
                OnPokeDetected?.Invoke(hit.collider.gameObject);

            _wasPoking = true;
        }

        private bool TryPokeUi(Vector3 tip, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            if (!WorldSpaceUiRaycastUtility.TryRaycast(tip, direction, _pokeUiMaxDistanceMeters, out RaycastResult uiHit))
                return false;

            if (!_wasPoking)
                WorldSpaceUiRaycastUtility.ExecuteClick(uiHit);

            _wasPoking = true;
            return true;
        }

        private void OnInputSourceChanged(InputSource inputSource)
        {
            IsHandTrackingActive = inputSource == InputSource.Hands ||
                                   inputSource == InputSource.ControllerAndHands;
        }

        private void OnDestroy()
        {
            var bootstrapper = AppBootstrapper.Singleton;
            if (bootstrapper != null)
                bootstrapper.OnInputSourceChanged -= OnInputSourceChanged;
        }
    }
}
