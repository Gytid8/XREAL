using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Manages all Canvas-based UI panels for the drawing viewer.
    /// Controls visibility states, transitions, and message display.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private GameObject _canvasRoot;

        [Header("UI Panels")]
        [SerializeField] private DrawingToolbarUI _toolbar;
        [SerializeField] private PageNavigatorUI _pageNavigator;
        [SerializeField] private FileBrowserUI _fileBrowser;

        [Header("Messages")]
        [SerializeField] private TMPro.TextMeshProUGUI _messageText;

        private UIState _stateBeforeHide = UIState.ToolbarOnly;

        /// <summary>
        /// Current UI visibility state.
        /// </summary>
        public UIState CurrentState { get; private set; } = UIState.Hidden;

        /// <summary>
        /// UI visibility states.
        /// </summary>
        public enum UIState
        {
            Hidden,
            ToolbarOnly,
            FileBrowser,
            FullUI
        }

        private void Awake()
        {
            // Auto-find references
            if (_mainCanvas == null && _canvasRoot != null)
                _mainCanvas = _canvasRoot.GetComponent<Canvas>();

            ResolveUiReferencesFromScene();
        }

        private void ResolveUiReferencesFromScene()
        {
            var uiCanvas = GameObject.Find("UI_Canvas");
            if (uiCanvas == null) return;

            if (_canvasRoot == null)
                _canvasRoot = uiCanvas;

            if (_mainCanvas == null)
                _mainCanvas = uiCanvas.GetComponent<Canvas>();

            if (_toolbar == null)
                _toolbar = uiCanvas.GetComponentInChildren<DrawingToolbarUI>(true);

            if (_pageNavigator == null)
                _pageNavigator = uiCanvas.GetComponentInChildren<PageNavigatorUI>(true);

            if (_fileBrowser == null)
                _fileBrowser = uiCanvas.GetComponentInChildren<FileBrowserUI>(true);

            if (_messageText == null)
                _messageText = uiCanvas.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        }

private void Start()
        {
            ConfigureWorldSpaceCanvas();
            ApplyPanelLayouts();
            if (_canvasRoot != null)
                DrawingViewerUiFactory.EnsureDrawingViewerUi(_canvasRoot.transform);
            _toolbar?.RefreshButtonBindings();
            _pageNavigator?.RefreshButtonBindings();

            
ApplyChineseFont();
            ApplyTechTheme();

            var app = DrawingViewerApp.Singleton;
            if (app != null)
            {
                app.OnStateChanged += OnAppStateChanged;
                app.OnDocumentOpened += OnDocumentOpened;
                app.OnPageChanged += OnPageChanged;
                app.OnError += OnError;
            }

            SetUIState(UIState.Hidden);
        }

        /// <summary>
        /// Sizes the world-space canvas for comfortable XR viewing.
        /// </summary>
        private void ConfigureWorldSpaceCanvas()
        {
            if (_canvasRoot == null) return;

            var settings = DrawingViewerApp.Singleton?.Settings;
            float canvasWidthMeters = settings != null ? settings.UICanvasWidth : DrawingViewerUILayout.DefaultCanvasWidthMeters;
            DrawingViewerUILayout.ConfigureCanvasRoot(_canvasRoot.transform, canvasWidthMeters);

            if (_mainCanvas != null)
            {
                _mainCanvas.renderMode = RenderMode.WorldSpace;
                _mainCanvas.worldCamera = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
                _mainCanvas.sortingOrder = 100;
            }
        }

        /// <summary>
        /// Ensures toolbar, page navigator, and file browser use correct anchors at runtime.
        /// </summary>
        private void ApplyPanelLayouts()
        {
            if (_canvasRoot == null) return;

            var settings = DrawingViewerApp.Singleton?.Settings;
            float bottomLift = settings != null ? settings.UIBottomBarLiftPixels : DrawingViewerUILayout.DefaultBottomBarLiftPixels;
            DrawingViewerUILayout.ApplyAll(_canvasRoot.transform, bottomLift);
        }

        private void ApplyChineseFont()
        {
            if (_canvasRoot != null)
                DrawingViewerFontProvider.ApplyAllInChildren(_canvasRoot.transform);
        }

private void ApplyTechTheme()
        {
            if (_canvasRoot != null)
                Unity.XR.XREAL.App.Core.AppUiTheme.ApplyDrawingViewerCanvas(_canvasRoot.transform);

            if (_messageText != null)
                Unity.XR.XREAL.App.Core.AppUiTheme.ApplyMessage(_messageText.transform);
        }


        /// <summary>
        /// Sets the UI visibility state.
        /// </summary>
        public void SetUIState(UIState state)
        {
            CurrentState = state;

            bool showToolbar = state != UIState.Hidden;
            bool showPageNav = state != UIState.Hidden;
            bool showBrowser = state == UIState.FileBrowser || state == UIState.FullUI;

            if (_toolbar != null)
                _toolbar.gameObject.SetActive(showToolbar);

            if (_pageNavigator != null)
                _pageNavigator.gameObject.SetActive(showPageNav);

            if (_fileBrowser != null)
            {
                _fileBrowser.gameObject.SetActive(showBrowser);
                if (showBrowser)
                {
                    _fileBrowser.transform.SetAsLastSibling();
                    _fileBrowser.RefreshFromAppPublic();
                }
            }

            if (_mainCanvas != null)
                _mainCanvas.enabled = state != UIState.Hidden;

            Debug.Log($"[UIManager] State: {state}");
        }

        /// <summary>
        /// Toggles the toolbar visibility.
        /// </summary>
        public void ToggleToolbar()
        {
            if (CurrentState == UIState.Hidden)
            {
                SetUIState(_stateBeforeHide);
                return;
            }

            _stateBeforeHide = CurrentState;
            SetUIState(UIState.Hidden);
        }

        /// <summary>
        /// Updates the drawing name shown below the active panel.
        /// </summary>
        public void UpdateDrawingTitle(string drawingName)
        {
            var canvasManager = DrawingViewerApp.Singleton?.GetComponent<DrawingCanvasManager>();
            if (canvasManager != null)
            {
                if (string.IsNullOrEmpty(drawingName))
                    canvasManager.ClearActivePanelTitle();
                else
                    canvasManager.UpdateActivePanelTitle(drawingName);
            }
        }

        /// <summary>
        /// Shows a message to the user.
        /// </summary>
        public void ShowMessage(string message, float duration = 3f)
        {
            if (_messageText != null)
            {
                _messageText.text = message;
                DrawingViewerFontProvider.Apply(_messageText);
                _messageText.gameObject.SetActive(true);
                CancelInvoke(nameof(HideMessage));
                Invoke(nameof(HideMessage), duration);
            }

            Debug.Log($"[UIManager] Message: {message}");
        }

        /// <summary>
        /// Hides the current message.
        /// </summary>
        public void HideMessage()
        {
            if (_messageText != null)
            {
                _messageText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Refreshes the file list in the file browser.
        /// </summary>
        public void RefreshFileList(List<DrawingDocument> documents)
        {
            if (_fileBrowser != null)
            {
                _fileBrowser.SetDocuments(documents);
            }
        }

        /// <summary>
        /// Updates the page indicator.
        /// </summary>
        public void UpdatePageIndicator(int currentPage, int totalPages)
        {
            if (_pageNavigator != null)
            {
                _pageNavigator.UpdatePageDisplay(currentPage, totalPages);
            }
        }

        /// <summary>
        /// Positions the UI canvas relative to the camera.
        /// </summary>
        public void PositionCanvasRelativeToCamera()
        {
            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (cam == null || _canvasRoot == null) return;

            var settings = DrawingViewerApp.Singleton?.Settings;
            float distance = settings != null ? settings.UICanvasDistance : 0.8f;
            float verticalOffset = settings != null ? settings.UICanvasVerticalOffset : -0.02f;

            Transform camTransform = cam.transform;
            if (_mainCanvas != null && _mainCanvas.worldCamera == null)
                _mainCanvas.worldCamera = cam;

            _canvasRoot.transform.position = camTransform.position + camTransform.forward * distance;
            _canvasRoot.transform.position += camTransform.up * verticalOffset;
            _canvasRoot.transform.rotation = Quaternion.LookRotation(
                _canvasRoot.transform.position - camTransform.position, Vector3.up);
        }

        private void LateUpdate()
        {
            // Keep UI canvas facing the camera smoothly
            if (_canvasRoot != null && CurrentState != UIState.Hidden)
            {
                PositionCanvasRelativeToCamera();
            }
        }

        #region Event Handlers

        private void OnAppStateChanged(DrawingViewerApp.AppState oldState, DrawingViewerApp.AppState newState)
        {
            switch (newState)
            {
                case DrawingViewerApp.AppState.ViewingDrawing:
                    SetUIState(UIState.ToolbarOnly);
                    break;
                case DrawingViewerApp.AppState.BrowsingFiles:
                    SetUIState(UIState.FileBrowser);
                    RefreshFileList(DrawingViewerApp.Singleton?.AvailableDocuments);
                    break;
                case DrawingViewerApp.AppState.Error:
                    SetUIState(UIState.ToolbarOnly);
                    break;
            }
        }

        private void OnDocumentOpened(DrawingDocument document)
        {
            UpdatePageIndicator(0, document.PageCount);
            UpdateDrawingTitle(document?.Name);
        }

        private void OnPageChanged(int pageIndex, int totalPages)
        {
            UpdatePageIndicator(pageIndex, totalPages);
        }

        private void OnError(string errorMessage)
        {
            ShowMessage(errorMessage, 5f);
        }

        #endregion

        private void OnDestroy()
        {
            var app = DrawingViewerApp.Singleton;
            if (app != null)
            {
                app.OnStateChanged -= OnAppStateChanged;
                app.OnDocumentOpened -= OnDocumentOpened;
                app.OnPageChanged -= OnPageChanged;
                app.OnError -= OnError;
            }
        }
    }
}
