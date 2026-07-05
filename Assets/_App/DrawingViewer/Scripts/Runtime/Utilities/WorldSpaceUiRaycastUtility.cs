using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.XR.XREAL.DrawingViewer
{
    public static class WorldSpaceUiRaycastUtility
    {
        private static readonly List<RaycastResult> ResultsBuffer = new List<RaycastResult>(16);

        public static bool TryRaycast(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            out RaycastResult bestResult)
        {
            bestResult = default;

            if (EventSystem.current == null)
                return false;

            bool found = false;
            float bestDistance = maxDistance;

            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.WorldSpace || !canvas.isActiveAndEnabled)
                    continue;

                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                    continue;

                if (!TryRaycastCanvas(canvas, raycaster, origin, direction, maxDistance, out RaycastResult result))
                    continue;

                if (result.distance < bestDistance)
                {
                    bestDistance = result.distance;
                    bestResult = result;
                    found = true;
                }
            }

            return found;
        }

        public static void ExecuteClick(RaycastResult result)
        {
            if (result.gameObject == null || EventSystem.current == null)
                return;

            var button = result.gameObject.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                return;
            }

            GameObject target = result.gameObject;
            var pointerData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = result,
                pointerPressRaycast = result,
                pointerPress = target,
                rawPointerPress = target,
                pointerEnter = target
            };

            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
        }

        public static bool IsButtonHit(RaycastResult result)
        {
            return result.gameObject != null && result.gameObject.GetComponentInParent<Button>() != null;
        }

        private static bool TryRaycastCanvas(
            Canvas canvas,
            GraphicRaycaster raycaster,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            out RaycastResult bestResult)
        {
            bestResult = default;

            Camera eventCamera = canvas.worldCamera;
            if (eventCamera == null)
                eventCamera = DrawingViewerCamera.MainCamera;
            if (eventCamera == null)
                return false;

            var ray = new Ray(origin, direction);
            var plane = new Plane(-direction, origin + direction * 0.01f);
            if (!plane.Raycast(ray, out float enter) || enter > maxDistance)
                return false;

            Vector3 worldPoint = origin + direction * enter;
            Vector2 screenPoint = eventCamera.WorldToScreenPoint(worldPoint);

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPoint
            };

            ResultsBuffer.Clear();
            raycaster.Raycast(pointerData, ResultsBuffer);
            if (ResultsBuffer.Count == 0)
                return false;

            bestResult = ResultsBuffer[0];
            bestResult.distance = Vector3.Distance(origin, bestResult.worldPosition);
            return true;
        }
    }
}
