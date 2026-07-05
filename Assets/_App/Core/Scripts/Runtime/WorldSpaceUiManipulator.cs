using UnityEngine;
using UnityEngine.UI;

namespace Unity.XR.XREAL.App.Core
{
    /// <summary>
    /// Allows a world-space UI canvas to be repositioned in space via laser or hand gestures.
    /// </summary>
    public class WorldSpaceUiManipulator : MonoBehaviour
    {
        [SerializeField] private Transform _dragRoot;
        [SerializeField] private float _positionSmoothDamping = 10f;
        [SerializeField] private float _grabPlaneDistanceThreshold = 0.2f;
        [SerializeField] private Vector2 _grabHalfExtentsMeters = new Vector2(0.85f, 0.65f);

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _positionVelocity;
        private Vector3 _grabOffset;
        private bool _userPlaced;
        private Camera _followCamera;
        private float _followDistance = 0.85f;
        private float _followVerticalOffset = 0.05f;

        public Transform DragRoot => _dragRoot != null ? _dragRoot : transform;

        public bool IsBeingManipulated { get; set; }

        public bool ShouldFollowCamera => !_userPlaced && !IsBeingManipulated;

        private void Awake()
        {
            _targetPosition = DragRoot.position;
            _targetRotation = DragRoot.rotation;
        }

private void LateUpdate()
        {
#if UNITY_EDITOR
            if (ShouldFollowCamera)
                return;
#else
            if (ShouldFollowCamera)
            {
                ApplyHeadFollow();
                return;
            }
#endif

            if (IsBeingManipulated)
            {
                DragRoot.position = _targetPosition;
                DragRoot.rotation = _targetRotation;
                return;
            }

            float smoothTime = 1f / Mathf.Max(0.01f, _positionSmoothDamping);
            DragRoot.position = Vector3.SmoothDamp(
                DragRoot.position,
                _targetPosition,
                ref _positionVelocity,
                smoothTime);

            DragRoot.rotation = Quaternion.Slerp(
                DragRoot.rotation,
                _targetRotation,
                Time.deltaTime * _positionSmoothDamping);
        }

public void SnapToCamera(Camera camera, float distance, float verticalOffset)
        {
            if (camera == null)
                return;

            Transform camTransform = camera.transform;
            _followCamera = camera;
            _followDistance = distance;
            _followVerticalOffset = verticalOffset;
            _targetPosition = camTransform.position + camTransform.forward * distance + camTransform.up * verticalOffset;
            _targetRotation = Quaternion.LookRotation(_targetPosition - camTransform.position, Vector3.up);

            if (DragRoot.parent != null)
                DragRoot.SetParent(null, true);

            DragRoot.SetPositionAndRotation(_targetPosition, _targetRotation);
            _positionVelocity = Vector3.zero;
        }

/// <summary>
        /// Parents the UI to the camera for 3DOF head-follow behavior.
        /// </summary>
        public void FollowCamera(Camera camera, float distance, float verticalOffset)
        {
            _followCamera = camera;
            _followDistance = distance;
            _followVerticalOffset = verticalOffset;
            ApplyHeadFollow();
        }

        /// <summary>
        /// Re-enables head-follow after returning to the launcher.
        /// </summary>
public void ResetHeadFollow()
        {
            _userPlaced = false;
            _positionVelocity = Vector3.zero;
#if UNITY_EDITOR
            if (DragRoot.parent != null)
                DetachFromCamera();
#else
            ApplyHeadFollow();
#endif
        }

/// <summary>
        /// Detaches from the camera and keeps the menu at its current world pose.
        /// </summary>
        public void PinToWorldSpace()
        {
            if (_userPlaced)
                return;

            _userPlaced = true;
            IsBeingManipulated = false;
            DetachFromCamera();
            _targetPosition = DragRoot.position;
            _targetRotation = DragRoot.rotation;
            _positionVelocity = Vector3.zero;
        }


        private void ApplyHeadFollow()
        {
            if (_followCamera == null || _userPlaced || IsBeingManipulated)
                return;

            Transform root = DragRoot;
            Transform cameraTransform = _followCamera.transform;

            if (root.parent != cameraTransform)
                root.SetParent(cameraTransform, false);

            root.localPosition = new Vector3(0f, _followVerticalOffset, _followDistance);
            root.localRotation = Quaternion.identity;
            _targetPosition = root.position;
            _targetRotation = root.rotation;
            _positionVelocity = Vector3.zero;
        }

        private void DetachFromCamera()
        {
            if (DragRoot.parent == null)
                return;

            DragRoot.SetParent(null, true);
            _targetPosition = DragRoot.position;
            _targetRotation = DragRoot.rotation;
        }


        public void MarkUserPlaced()
        {
            _userPlaced = true;
        }

        public bool IsDraggableTarget(GameObject hitObject)
        {
            if (hitObject == null || !isActiveAndEnabled)
                return false;

            if (hitObject.GetComponentInParent<Button>() != null)
                return false;

            return hitObject.transform.IsChildOf(DragRoot);
        }

        public bool IsNearGrabSurface(Vector3 worldPoint)
        {
            if (!isActiveAndEnabled)
                return false;

            Transform root = DragRoot;
            var plane = new Plane(-root.forward, root.position);
            if (Mathf.Abs(plane.GetDistanceToPoint(worldPoint)) > _grabPlaneDistanceThreshold)
                return false;

            Vector3 local = root.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= _grabHalfExtentsMeters.x
                && Mathf.Abs(local.y) <= _grabHalfExtentsMeters.y;
        }

        public static bool TryFindGrabTarget(Vector3 worldPoint, out WorldSpaceUiManipulator manipulator)
        {
            manipulator = null;
            float bestDistance = float.MaxValue;

            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<WorldSpaceUiManipulator>(true))
            {
                if (!candidate.isActiveAndEnabled || !candidate.IsNearGrabSurface(worldPoint))
                    continue;

                Transform root = candidate.DragRoot;
                float distance = Vector3.Distance(worldPoint, root.position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                manipulator = candidate;
            }

            return manipulator != null;
        }

        public void BeginGrab(Vector3 grabWorldPoint)
        {
            IsBeingManipulated = true;
            _userPlaced = true;
            DetachFromCamera();
            _grabOffset = _targetPosition - grabWorldPoint;
        }

        public void DragToWorldPoint(Vector3 worldPoint)
        {
            SetTargetPosition(worldPoint + _grabOffset);
        }

        public void BeginGrabFromUiHit(Vector3 hitWorldPoint)
        {
            BeginGrab(hitWorldPoint);
        }

        public void SetTargetPosition(Vector3 position)
        {
            _targetPosition = position;
        }

        public void SetTargetRotation(Quaternion rotation)
        {
            _targetRotation = rotation;
        }

        public void EndGrab()
        {
            IsBeingManipulated = false;
        }

        public void MoveAlongRay(Ray ray)
        {
            var plane = new Plane(-DragRoot.forward, DragRoot.position);
            if (!plane.Raycast(ray, out float enter))
                return;

            SetTargetPosition(ray.GetPoint(enter) + _grabOffset);
        }
    }
}
