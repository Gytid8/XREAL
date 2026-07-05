using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.XR.XREAL.App.Core;
#if UNITY_EDITOR
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Virtual laser pointer for XREAL Beam Pro controller.
    /// Raycasts UI and drawing panels, drives clicks and panel dragging.
    /// </summary>
    public class DrawingLaserController : MonoBehaviour
    {
        [Header("Ray")]
        [SerializeField] private Transform _rayAttachTransform;
        [SerializeField] private Transform _rayOriginTransform;
        [SerializeField] private LayerMask _physicsLayerMask = ~0;
        [SerializeField] private float _defaultRayDistance = 2.5f;

        [Header("Visual")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Transform _reticleTransform;
        [SerializeField] private float _lineWidth = 0.004f;
        [SerializeField] private Color _lineColor = new Color(0.2f, 0.75f, 1f, 0.85f);
        [SerializeField] private Color _hoverColor = new Color(0.35f, 0.95f, 1f, 1f);

        private XREALInput _xrealInput;
        private bool _isDraggingPanel;
        private bool _isDraggingUi;
        private bool _isPointerOnUi;
        private Vector3 _panelDragOffset;
        private DrawingPanelController _draggedPanel;
        
        private GameObject _currentHoverTarget;

        private RaycastHit _physicsHit;
        private RaycastResult _uiHit;
        private bool _hasPhysicsHit;
        private bool _hasUiHit;
        private bool _uiHitIsClosest;

        public Transform RayAttachTransform => _rayAttachTransform;
        public bool HasValidRay => _rayAttachTransform != null;

        private void Start()
        {
            _xrealInput = XREALInput.Singleton ?? XREALInput.CreateSingleton();
            EnsureVisuals();
            StartCoroutine(ResolveControllerRayRoutine());
        }

        private IEnumerator ResolveControllerRayRoutine()
        {
            const float timeout = 8f;
            float elapsed = 0f;

            while (_rayAttachTransform == null && elapsed < timeout)
            {
                TryResolveControllerRay();
                if (_rayAttachTransform != null)
                    break;

                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }

            if (_rayAttachTransform == null)
                Debug.LogWarning("[DrawingLaserController] Controller ray transform not found. Laser disabled until controller appears.");
        }

        private void TryResolveControllerRay()
        {
            if (_rayAttachTransform != null)
                return;

            var rightController = GameObject.Find("Right Controller");
            if (rightController != null)
            {
                _rayAttachTransform = rightController.transform.Find("Ray Interactor")
                    ?? rightController.transform.Find("Near-Far Interactor")
                    ?? rightController.transform.Find("Attach Transform")
                    ?? rightController.transform;
            }

            if (_rayOriginTransform == null)
                _rayOriginTransform = _rayAttachTransform;
        }

        private void EnsureVisuals()
        {
            if (_lineRenderer == null)
            {
                var lineGo = new GameObject("LaserLine");
                lineGo.transform.SetParent(transform, false);
                _lineRenderer = lineGo.AddComponent<LineRenderer>();
                _lineRenderer.useWorldSpace = true;
                _lineRenderer.positionCount = 2;
                _lineRenderer.startWidth = _lineWidth;
                _lineRenderer.endWidth = _lineWidth * 0.6f;
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _lineRenderer.startColor = _lineColor;
                _lineRenderer.endColor = _lineColor;
            }

            if (_reticleTransform == null)
            {
                var reticleGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reticleGo.name = "LaserReticle";
                reticleGo.transform.SetParent(transform, false);
                reticleGo.transform.localScale = Vector3.one * 0.02f;
                var collider = reticleGo.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                var renderer = reticleGo.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = _lineColor;
                _reticleTransform = reticleGo.transform;
            }
        }

        private void LateUpdate()
        {
            var handler = DrawingInteractionHandler.Singleton;
            if (handler == null || !handler.CanProcessController())
            {
                SetLaserVisible(false);
                return;
            }

            if (_rayAttachTransform == null)
            {
                TryResolveControllerRay();
                if (_rayAttachTransform == null)
                {
                    UpdateRayFromInputDevice();
                }
            }

            if (_rayAttachTransform == null)
            {
                SetLaserVisible(false);
                return;
            }

            UpdateRaycast();
            UpdateVisual();
            ProcessTrigger(handler);
        }

        private void UpdateRayFromInputDevice()
        {
            if (_xrealInput == null || !_xrealInput.TryGetPose(out Vector3 position, out Quaternion rotation))
                return;

            if (_rayAttachTransform == null)
            {
                var go = new GameObject("ControllerRayAttach");
                _rayAttachTransform = go.transform;
            }

            _rayAttachTransform.SetPositionAndRotation(position, rotation);
            if (_rayOriginTransform == null)
                _rayOriginTransform = _rayAttachTransform;
        }

        private void UpdateRaycast()
        {
            _hasPhysicsHit = false;
            _hasUiHit = false;
            _uiHitIsClosest = true;

            Vector3 origin = _rayOriginTransform != null ? _rayOriginTransform.position : _rayAttachTransform.position;
            Vector3 direction = _rayAttachTransform.forward;

            float maxDistance = DrawingViewerApp.Singleton?.Settings != null
                ? DrawingViewerApp.Singleton.Settings.LaserRayDistance
                : _defaultRayDistance;

            _hasPhysicsHit = Physics.Raycast(origin, direction, out _physicsHit, maxDistance, _physicsLayerMask);
            _hasUiHit = TryRaycastUi(origin, direction, maxDistance, out _uiHit);

            if (_hasPhysicsHit && _hasUiHit)
                _uiHitIsClosest = _uiHit.distance <= _physicsHit.distance;
            else if (_hasPhysicsHit)
                _uiHitIsClosest = false;
        }

        private static bool TryRaycastUi(Vector3 origin, Vector3 direction, float maxDistance, out RaycastResult bestResult)
        {
            return WorldSpaceUiRaycastUtility.TryRaycast(origin, direction, maxDistance, out bestResult);
        }

        private void UpdateVisual()
        {
            bool visible = _rayAttachTransform != null;
            SetLaserVisible(visible);
            if (!visible)
                return;

            Vector3 start = _rayOriginTransform != null ? _rayOriginTransform.position : _rayAttachTransform.position;
            Vector3 end = start + _rayAttachTransform.forward * _defaultRayDistance;
            bool hovering = false;

            if (_hasUiHit && (_uiHitIsClosest || !_hasPhysicsHit))
            {
                end = _uiHit.worldPosition;
                hovering = true;
                UpdateHoverTarget(_uiHit.gameObject);
            }
            else if (_hasPhysicsHit)
            {
                end = _physicsHit.point;
                hovering = _physicsHit.collider.GetComponentInParent<DrawingPanelController>() != null;
                UpdateHoverTarget(_physicsHit.collider.gameObject);
            }
            else
            {
                UpdateHoverTarget(null);
            }

            _lineRenderer.SetPosition(0, start);
            _lineRenderer.SetPosition(1, end);
            _lineRenderer.startColor = hovering ? _hoverColor : _lineColor;
            _lineRenderer.endColor = hovering ? _hoverColor : _lineColor;

            if (_reticleTransform != null)
            {
                _reticleTransform.position = end;
                _reticleTransform.rotation = Quaternion.LookRotation(-_rayAttachTransform.forward, Vector3.up);
                float scale = 0.015f + 0.01f * Vector3.Distance(start, end);
                _reticleTransform.localScale = Vector3.one * scale;
            }
        }

        private void UpdateHoverTarget(GameObject target)
        {
            if (_currentHoverTarget == target)
                return;

            _currentHoverTarget = target;
        }

private void ProcessTrigger(DrawingInteractionHandler handler)
        {
            if (_xrealInput == null)
                return;

            bool triggerDown = _xrealInput.GetButtonDown(ControllerButton.TRIGGER);
            bool triggerHeld = _xrealInput.GetButton(ControllerButton.TRIGGER);
            bool triggerUp = _xrealInput.GetButtonUp(ControllerButton.TRIGGER);

            if (triggerDown)
                handler.NotifyControllerInput();

            if (triggerDown)
                TryBeginInteraction(handler);

            if (triggerHeld && _isDraggingPanel)
                UpdatePanelDrag();

            if (triggerHeld && _isPointerOnUi)
                UpdateUiPointer();

            if (triggerUp)
                EndInteraction();
        }

private void TryBeginInteraction(DrawingInteractionHandler handler)
        {
            _isPointerOnUi = false;

            if (_hasUiHit && (_uiHitIsClosest || !_hasPhysicsHit))
            {
                WorldSpaceUiPointerInteraction.BeginPointer(_uiHit, useScreenThreshold: false);
                _isPointerOnUi = true;
                _isDraggingUi = WorldSpaceUiPointerInteraction.IsDragging;
                return;
            }

            var app = DrawingViewerApp.Singleton;
            if (app == null || !app.IsActive)
                return;

            if (!_hasPhysicsHit)
                return;

            var panel = _physicsHit.collider.GetComponentInParent<DrawingPanelController>();
            if (panel == null)
                return;

            _isDraggingPanel = true;
            _draggedPanel = panel;
            _panelDragOffset = panel.transform.position - _physicsHit.point;
            panel.IsBeingManipulated = true;
        }

        private void UpdatePanelDrag()
        {
            if (_draggedPanel == null || _rayAttachTransform == null)
                return;

            Vector3 origin = _rayOriginTransform != null ? _rayOriginTransform.position : _rayAttachTransform.position;
            var ray = new Ray(origin, _rayAttachTransform.forward);
            var plane = new Plane(-_draggedPanel.transform.forward, _draggedPanel.transform.position);

            if (!plane.Raycast(ray, out float enter))
                return;

            Vector3 hitPoint = ray.GetPoint(enter);
            _draggedPanel.SetTargetPosition(hitPoint + _panelDragOffset);
        }



private void UpdateUiPointer()
        {
            if (_rayAttachTransform == null)
                return;

            Vector3 origin = _rayOriginTransform != null ? _rayOriginTransform.position : _rayAttachTransform.position;
            var ray = new Ray(origin, _rayAttachTransform.forward);
            WorldSpaceUiPointerInteraction.UpdatePointer(ray, Vector2.zero);
            _isDraggingUi = WorldSpaceUiPointerInteraction.IsDragging;
        }

private void EndInteraction()
        {
            if (_draggedPanel != null)
                _draggedPanel.IsBeingManipulated = false;

            if (_isPointerOnUi)
                WorldSpaceUiPointerInteraction.EndPointer();

            _isDraggingPanel = false;
            _isDraggingUi = false;
            _isPointerOnUi = false;
            _draggedPanel = null;
        }

        private void SetLaserVisible(bool visible)
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = visible;
            if (_reticleTransform != null)
                _reticleTransform.gameObject.SetActive(visible);
        }
    }
}
