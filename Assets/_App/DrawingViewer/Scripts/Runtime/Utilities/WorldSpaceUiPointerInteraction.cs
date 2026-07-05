using UnityEngine;
using UnityEngine.EventSystems;

using Unity.XR.XREAL.App.Core;
using UnityEngine.UI;

namespace Unity.XR.XREAL.DrawingViewer
{
    public static class WorldSpaceUiPointerInteraction
    {
        private const float ScreenDragThresholdPixels = 8f;
        private const float WorldDragThresholdMeters = 0.02f;

        private static WorldSpaceUiManipulator _activeManipulator;
        private static WorldSpaceUiManipulator _pendingManipulator;
        private static RaycastResult _pendingHit;
        private static bool _hasPendingPointer;
        private static bool _isDragging;
        private static Vector2 _pointerDownScreenPos;
        private static Vector3 _pointerDownWorldPos;
        private static bool _useScreenThreshold;

        public static bool IsDragging => _isDragging;

        public static bool TryRaycastUi(Vector3 origin, Vector3 direction, float maxDistance, out RaycastResult result)
        {
            return WorldSpaceUiRaycastUtility.TryRaycast(origin, direction, maxDistance, out result);
        }

        public static bool BeginPointer(Vector3 origin, Vector3 direction, Vector2 screenPosition, float maxDistance = 5f)
        {
            CancelPointer();

            if (!WorldSpaceUiRaycastUtility.TryRaycast(origin, direction, maxDistance, out RaycastResult uiHit))
                return false;

            return BeginPointer(uiHit, useScreenThreshold: true, screenPosition);
        }

        public static bool BeginPointer(RaycastResult uiHit, bool useScreenThreshold, Vector2 screenPosition = default)
        {
            CancelPointer();

#if UNITY_EDITOR
            if (ShouldUseStandaloneInputModule() && WorldSpaceUiRaycastUtility.IsButtonHit(uiHit))
                return false;
#endif

            _hasPendingPointer = true;
            _pendingHit = uiHit;
            _pointerDownScreenPos = screenPosition;
            _pointerDownWorldPos = uiHit.worldPosition;
            _useScreenThreshold = useScreenThreshold;

            var manipulator = uiHit.gameObject.GetComponentInParent<WorldSpaceUiManipulator>();
            _pendingManipulator = manipulator != null && manipulator.IsDraggableTarget(uiHit.gameObject)
                ? manipulator
                : null;

            return true;
        }

        public static void UpdatePointer(Ray ray, Vector2 screenPosition)
        {
            if (!_hasPendingPointer)
                return;

            if (!_isDragging && _pendingManipulator != null)
            {
                bool passedThreshold = _useScreenThreshold
                    ? Vector2.Distance(screenPosition, _pointerDownScreenPos) >= ScreenDragThresholdPixels
                    : HasWorldDragThreshold(ray);

                if (passedThreshold)
                    StartDrag();
            }

            if (_isDragging)
                HandlePointerDrag(ray);
        }

        public static void EndPointer()
        {
            if (_hasPendingPointer && !_isDragging && IsClickableTarget(_pendingHit.gameObject))
            {
#if UNITY_EDITOR
                if (!ShouldUseStandaloneInputModule())
#endif
                    WorldSpaceUiRaycastUtility.ExecuteClick(_pendingHit);
            }

            if (_isDragging)
                HandlePointerUp();
            else
                CancelPointer();
        }

        public static void HandlePointerDrag(Ray ray)
        {
            if (!_isDragging || _activeManipulator == null)
                return;

            _activeManipulator.MoveAlongRay(ray);
        }

        public static void HandlePointerUp()
        {
            if (_activeManipulator != null)
                _activeManipulator.EndGrab();

            _isDragging = false;
            _activeManipulator = null;
            _hasPendingPointer = false;
            _pendingManipulator = null;
        }

        public static void CancelPointer()
        {
            if (_isDragging)
                HandlePointerUp();

            _hasPendingPointer = false;
            _pendingManipulator = null;
        }

        private static void StartDrag()
        {
            _isDragging = true;
            _activeManipulator = _pendingManipulator;
            _activeManipulator.BeginGrabFromUiHit(_pointerDownWorldPos);
        }

        private static bool HasWorldDragThreshold(Ray ray)
        {
            var plane = new Plane(-_pendingManipulator.DragRoot.forward, _pointerDownWorldPos);
            if (!plane.Raycast(ray, out float enter))
                return false;

            Vector3 currentPoint = ray.GetPoint(enter);
            return Vector3.Distance(currentPoint, _pointerDownWorldPos) >= WorldDragThresholdMeters;
        }

        private static bool IsClickableTarget(GameObject hitObject)
        {
            return hitObject != null && hitObject.GetComponentInParent<Button>() != null;
        }

#if UNITY_EDITOR
        private static bool ShouldUseStandaloneInputModule()
        {
            return EventSystem.current != null
                && EventSystem.current.currentInputModule is StandaloneInputModule standalone
                && standalone.enabled;
        }
#endif
    }
}
