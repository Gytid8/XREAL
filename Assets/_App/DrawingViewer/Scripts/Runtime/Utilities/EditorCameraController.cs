#if UNITY_EDITOR
using UnityEngine;
using Unity.XR.XREAL.App.Core;
using UnityEngine.EventSystems;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Editor-only camera and drawing viewer shortcuts when AR hardware is unavailable.
    /// Right-click drag: look. WASD: move. F: file browser. +/-: zoom. Z/X: rotate. R: reset view.
    /// </summary>
    public class EditorCameraController : MonoBehaviour
    {
        [SerializeField] private float _lookSpeed = 2f;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _scrollSpeed = 2f;
        [SerializeField] private float _zoomStep = 0.2f;

        private float _yaw;
        private float _pitch;
        private bool _isLooking;
        private bool _isDraggingPanel;
        private Vector3 _lastMouseWorldPoint;

        private void Start()
        {
            ResetEditorViewPose();

            if (Camera.main == transform.GetComponent<Camera>())
            {
                _yaw = transform.eulerAngles.y;
                _pitch = transform.eulerAngles.x;
            }
        }

        private void ResetEditorViewPose()
        {
            transform.position = new Vector3(0f, 1.6f, 0f);
            transform.rotation = Quaternion.identity;
            _yaw = 0f;
            _pitch = 0f;
        }

        private void Update()
        {
            HandleCameraControls();
            HandleWorldSpaceUiMouseInput();
            HandleDrawingViewerShortcuts();
        }

        private void HandleCameraControls()
        {
            if (Input.GetMouseButtonDown(1))
            {
                PinFollowingWorldSpaceMenus();
                _isLooking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _isLooking = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (_isLooking)
            {
                float mouseX = Input.GetAxis("Mouse X") * _lookSpeed;
                float mouseY = Input.GetAxis("Mouse Y") * _lookSpeed;

                _yaw += mouseX;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);

                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);
            }

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;

            float speed = _moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= 2f;

            transform.position += move.normalized * speed * Time.deltaTime;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                transform.position += transform.forward * scroll * _scrollSpeed;
        }

        private void HandleWorldSpaceUiMouseInput()
        {
            if (_isLooking)
            {
                if (Input.GetMouseButtonUp(0))
                    WorldSpaceUiPointerInteraction.CancelPointer();
                return;
            }

            // StandaloneInputModule already handles world-space button clicks in Editor.
            if (IsEditorStandaloneInputActive())
                return;

            Camera cam = DrawingViewerCamera.MainCamera;
            if (cam == null)
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            const float maxDistance = 5f;
            Vector2 screenPos = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
                WorldSpaceUiPointerInteraction.BeginPointer(ray.origin, ray.direction, screenPos, maxDistance);

            if (Input.GetMouseButton(0))
                WorldSpaceUiPointerInteraction.UpdatePointer(ray, screenPos);

            if (Input.GetMouseButtonUp(0))
                WorldSpaceUiPointerInteraction.EndPointer();
        }

        private static bool IsEditorStandaloneInputActive()
        {
            return EventSystem.current != null
                && EventSystem.current.currentInputModule is StandaloneInputModule standalone
                && standalone.enabled;
        }

        private static void PinFollowingWorldSpaceMenus()
        {
            foreach (var manipulator in Object.FindObjectsOfType<WorldSpaceUiManipulator>(true))
            {
                if (manipulator.isActiveAndEnabled && manipulator.ShouldFollowCamera)
                    manipulator.PinToWorldSpace();
            }
        }

        private void HandleDrawingViewerShortcuts()
        {
            if (DrawingViewerApp.Singleton == null)
                return;

            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.PageUp))
                DrawingViewerApp.Singleton.PreviousPage();

            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.PageDown))
                DrawingViewerApp.Singleton.NextPage();

            var handler = DrawingInteractionHandler.Singleton;
            if (handler != null)
            {
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                    handler.HandleZoom(_zoomStep);

                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                    handler.HandleZoom(-_zoomStep);

                if (Input.GetKeyDown(KeyCode.Z))
                    handler.HandleRotateLeftStep();

                if (Input.GetKeyDown(KeyCode.X))
                    handler.HandleRotateRightStep();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                var canvasManager = DrawingViewerApp.Singleton.GetComponent<DrawingCanvasManager>();
                canvasManager?.RepositionActivePanel();
            }

            if (Input.GetKeyDown(KeyCode.L))
                DrawingViewerApp.Singleton.GetComponent<DrawingCanvasManager>()?.CycleLayout();

            if (Input.GetKeyDown(KeyCode.Tab))
                DrawingViewerApp.Singleton.GetComponent<UIManager>()?.ToggleToolbar();

            if (Input.GetKeyDown(KeyCode.F))
            {
                var app = DrawingViewerApp.Singleton;
                if (app.CurrentState == DrawingViewerApp.AppState.BrowsingFiles)
                    app.HideFileBrowser();
                else
                    app.ShowFileBrowser();
            }

            HandlePanelDrag();
        }

        private void HandlePanelDrag()
        {
            if (WorldSpaceUiPointerInteraction.IsDragging)
                return;

            var panel = DrawingViewerApp.Singleton.GetComponent<DrawingCanvasManager>()?.ActivePanel;
            if (panel == null)
                return;

            Camera cam = DrawingViewerCamera.MainCamera;
            if (cam == null)
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Input.GetMouseButtonDown(0)
                && WorldSpaceUiPointerInteraction.TryRaycastUi(ray.origin, ray.direction, 5f, out _))
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _isDraggingPanel = true;
                panel.IsBeingManipulated = true;
                _lastMouseWorldPoint = GetMouseWorldPointOnPanelPlane(panel);
            }

            if (Input.GetMouseButton(0) && _isDraggingPanel)
            {
                Vector3 currentPoint = GetMouseWorldPointOnPanelPlane(panel);
                panel.SetTargetPosition(panel.transform.position + (currentPoint - _lastMouseWorldPoint));
                _lastMouseWorldPoint = currentPoint;
            }

            if (Input.GetMouseButtonUp(0) && _isDraggingPanel)
            {
                _isDraggingPanel = false;
                panel.IsBeingManipulated = false;
            }
        }

        private static Vector3 GetMouseWorldPointOnPanelPlane(DrawingPanelController panel)
        {
            Camera cam = DrawingViewerCamera.MainCamera;
            if (cam == null) return panel.transform.position;

            Plane plane = new Plane(-panel.transform.forward, panel.transform.position);
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : panel.transform.position;
        }
    }
}
#endif
