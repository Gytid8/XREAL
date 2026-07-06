using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Maps XREAL physical controller inputs to drawing viewer actions.
    /// Touchpad, trigger, and buttons drive navigation, zoom, and panel manipulation.
    /// </summary>
    public class DrawingControllerInput : MonoBehaviour
    {
        [Header("Input Thresholds")]
        [SerializeField] private float _touchpadDeadzone = 0.15f;
        [SerializeField] private float _longPressDuration = 0.5f;

        [Header("Scroll Sensitivity")]
        [SerializeField] private float _zoomScrollSpeed = 0.5f;
        [SerializeField] private float _fixedZoomStep = 0.1f;
        [SerializeField] private float _zoomScrollThreshold = 0.35f;
        [SerializeField] private float _pageScrollSpeed = 0.5f;
        [SerializeField] private float _touchpadRotationSpeed = 1.5f;

        /// <summary>
        /// Reference to the XREAL input system.
        /// </summary>
        private XREALInput _xrealInput;

        /// <summary>
        /// Timestamp when APP button was pressed (for long press detection).
        /// </summary>
        private float _appPressTime;

        /// <summary>
        /// Whether the touchpad was touched in the previous frame.
        /// </summary>
        private bool _wasTouching;

        /// <summary>
        /// Accumulated horizontal scroll for page navigation threshold.
        /// </summary>
        private float _accumulatedHorizontalScroll;

        /// <summary>
        /// Accumulated vertical scroll for fixed-step zoom threshold.
        /// </summary>
        private float _accumulatedVerticalScroll;

        private const float PageScrollThreshold = 0.5f;

        private void Start()
        {
            _xrealInput = XREALInput.Singleton;

            if (_xrealInput == null)
            {
                _xrealInput = XREALInput.CreateSingleton();
            }

            var settings = DrawingViewerApp.Singleton?.Settings;
            if (settings != null)
            {
                _touchpadDeadzone = settings.TouchpadDeadzone;
                _touchpadRotationSpeed = settings.TouchpadRotationSpeed;
                _longPressDuration = 0.5f;
            }
        }

        private void Update()
        {
            if (_xrealInput == null) return;

            var interactionHandler = DrawingInteractionHandler.Singleton;
            if (interactionHandler == null) return;

            // Process touchpad input
            ProcessTouchpad(interactionHandler);

            // Process button input
            ProcessButtons(interactionHandler);

            // Process trigger
            ProcessTrigger(interactionHandler);

            // Track touch state
            _wasTouching = _xrealInput.IsTouching();
        }

        /// <summary>
        /// Processes touchpad scrolling and swiping for zoom and page navigation.
        /// </summary>
        private void ProcessTouchpad(DrawingInteractionHandler handler)
        {
            if (!handler.CanProcessController()) return;

            if (_xrealInput.IsTouchScrolling())
            {
                handler.NotifyControllerInput();

                Vector2 touchDelta = _xrealInput.GetDeltaTouch();

                // Apply deadzone
                if (Mathf.Abs(touchDelta.x) < _touchpadDeadzone) touchDelta.x = 0;
                if (Mathf.Abs(touchDelta.y) < _touchpadDeadzone) touchDelta.y = 0;

                if (_xrealInput.GetButton(ControllerButton.GRIP))
                {
                    if (Mathf.Abs(touchDelta.x) > 0f)
                        handler.HandleRotate(-touchDelta.x * _touchpadRotationSpeed * 90f);
                    return;
                }

                // Vertical scroll = fixed-step zoom
                if (Mathf.Abs(touchDelta.y) > 0)
                {
                    _accumulatedVerticalScroll += touchDelta.y * _zoomScrollSpeed;

                    if (Mathf.Abs(_accumulatedVerticalScroll) >= _zoomScrollThreshold)
                    {
                        int direction = _accumulatedVerticalScroll > 0 ? -1 : 1;
                        handler.HandleZoomStep(direction * _fixedZoomStep);
                        _accumulatedVerticalScroll = 0;
                    }
                }

                // Horizontal scroll = page navigation
                if (Mathf.Abs(touchDelta.x) > 0)
                {
                    _accumulatedHorizontalScroll += touchDelta.x * _pageScrollSpeed;

                    if (Mathf.Abs(_accumulatedHorizontalScroll) >= PageScrollThreshold)
                    {
                        int direction = _accumulatedHorizontalScroll > 0 ? -1 : 1;
                        handler.HandlePageNavigation(direction);
                        _accumulatedHorizontalScroll = 0;
                    }
                }
            }
            else if (!_xrealInput.IsTouching() && _wasTouching)
            {
                // Touch ended - reset scroll accumulators
                _accumulatedHorizontalScroll = 0;
                _accumulatedVerticalScroll = 0;
            }

            // Direct touch position for drag
            if (_xrealInput.IsTouching() && !_xrealInput.IsTouchScrollStart())
            {
                // Could implement drag-by-touchpad here
            }
        }

        /// <summary>
        /// Processes controller button presses.
        /// </summary>
        private void ProcessButtons(DrawingInteractionHandler handler)
        {
            if (!handler.CanProcessController()) return;

            // APP button: short press = toggle UI, long press = reset panel position
            if (_xrealInput.GetButtonDown(ControllerButton.APP))
            {
                handler.NotifyControllerInput();
                _appPressTime = Time.time;
            }

            if (_xrealInput.GetButtonUp(ControllerButton.APP))
            {
                handler.NotifyControllerInput();
                float holdDuration = Time.time - _appPressTime;
                if (holdDuration < _longPressDuration)
                {
                    // Short press: toggle toolbar UI
                    var uiManager = DrawingViewerApp.Singleton?.GetComponent<UIManager>();
                    if (uiManager != null)
                    {
                        uiManager.ToggleToolbar();
                    }
                }
                else
                {
                    // Long press: reset panel to default view
                    var canvasManager = DrawingViewerApp.Singleton?.GetComponent<DrawingCanvasManager>();
                    if (canvasManager?.ActivePanel != null)
                    {
                        Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
                        canvasManager.ActivePanel.ResetToDefaultView(cam);
                    }
                }
            }

            // HOME button: open file browser
            if (_xrealInput.GetButtonDown(ControllerButton.HOME))
            {
                handler.NotifyControllerInput();

                var app = DrawingViewerApp.Singleton;
                if (app != null)
                {
                    if (app.CurrentState == DrawingViewerApp.AppState.ViewingDrawing)
                    {
                        app.ShowFileBrowser();
                    }
                    else if (app.CurrentState == DrawingViewerApp.AppState.BrowsingFiles)
                    {
                        app.CloseDocument();
                    }
                }
            }
        }

        /// <summary>
        /// Processes trigger button. Panel/UI interaction is handled by <see cref="DrawingLaserController"/>.
        /// </summary>
        private void ProcessTrigger(DrawingInteractionHandler handler)
        {
            if (!handler.CanProcessController()) return;

            if (_xrealInput.GetButtonDown(ControllerButton.TRIGGER) ||
                _xrealInput.GetButton(ControllerButton.TRIGGER) ||
                _xrealInput.GetButtonUp(ControllerButton.TRIGGER))
            {
                handler.NotifyControllerInput();
            }
        }
    }
}
