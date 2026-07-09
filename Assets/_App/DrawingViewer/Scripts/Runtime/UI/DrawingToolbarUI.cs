using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Unity.XR.XREAL.App.Core;

namespace Unity.XR.XREAL.DrawingViewer
{
    public class DrawingToolbarUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _zoomInButton;
        [SerializeField] private Button _zoomOutButton;
        [SerializeField] private Button _rotateLeftButton;
        [SerializeField] private Button _rotateRightButton;
        [SerializeField] private Button _resetViewButton;
        [SerializeField] private Button _cycleLayoutButton;
        [SerializeField] private Button _openFileButton;
        [SerializeField] private Button _returnToMenuButton;

        [Header("Settings")]
        [SerializeField] private float _zoomStep = 0.1f;

        private float _lastZoomClickTime;
        private float _lastRotateClickTime;
        private float _lastLayoutClickTime;
        private int _lastZoomFrame = -1;
        private const float ZoomClickCooldown = 0.35f;
        private const float RotateClickCooldown = 0.15f;
        private const float LayoutClickCooldown = 0.35f;

        private void Awake()
        {
            DrawingViewerUiFactory.EnsureToolbarButtons(transform);
        }

        private void Start()
        {
            Debug.Log($"[ToolbarDebug] DrawingToolbarUI instances = {FindObjectsOfType<DrawingToolbarUI>(true).Length}, object = {name}");
            RefreshButtonBindings();
            SyncLayoutButtonLabel();
        }

        public void RefreshButtonBindings()
        {
            DrawingViewerUiFactory.EnsureToolbarButtons(transform);
            ResolveButtonReferences();
            WireButtonListeners();
            DrawingViewerUiFactory.RepairBrokenButtonSizes(transform);
            AppUiTheme.ApplyToolbar(transform);
            DrawingViewerFontProvider.ApplyAllInChildren(transform);
            SyncLayoutButtonLabel();
        }

        private void ResolveButtonReferences()
        {
            _zoomInButton = FindToolbarButton("Btn_ZoomIn");
            _zoomOutButton = FindToolbarButton("Btn_ZoomOut");
            _rotateLeftButton = FindToolbarButton("Btn_RotateLeft");
            _rotateRightButton = FindToolbarButton("Btn_RotateRight");
            _resetViewButton = FindToolbarButton("Btn_ResetView");
            _cycleLayoutButton = FindToolbarButton("Btn_CycleLayout");
            _openFileButton = FindToolbarButton("Btn_OpenFile");
            _returnToMenuButton = FindToolbarButton("Btn_ReturnToMenu");

            var legacyTitle = transform.Find("DrawingNameText");
            if (legacyTitle != null)
                legacyTitle.gameObject.SetActive(false);
        }

        private Button FindToolbarButton(string name)
        {
            var t = transform.Find(name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private void WireButtonListeners()
        {
            BindButton(_zoomInButton, OnZoomIn);
            BindButton(_zoomOutButton, OnZoomOut);
            BindButton(_rotateLeftButton, OnRotateLeft);
            BindButton(_rotateRightButton, OnRotateRight);
            BindButton(_resetViewButton, OnResetView);
            BindButton(_cycleLayoutButton, OnCycleLayout);
            BindButton(_openFileButton, OnOpenFile);
            BindButton(_returnToMenuButton, OnReturnToMenu);

            Debug.Log($"[ToolbarDebug] zoomIn={_zoomInButton?.name}, zoomOut={_zoomOutButton?.name}");
        }

        private static void BindButton(Button button, UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public void OnZoomIn()
        {
            Debug.Log($"[ToolbarDebug] OnZoomIn frame={Time.frameCount}, time={Time.unscaledTime}, object={name}");
            if (!CanZoomNow())
                return;

            var handler = DrawingInteractionHandler.Singleton;
            if (handler != null)
                handler.HandleZoomStep(_zoomStep);
        }

        public void OnZoomOut()
        {
            Debug.Log($"[ToolbarDebug] OnZoomOut frame={Time.frameCount}, time={Time.unscaledTime}, object={name}");
            if (!CanZoomNow())
                return;

            var handler = DrawingInteractionHandler.Singleton;
            if (handler != null)
                handler.HandleZoomStep(-_zoomStep);
        }

        private bool CanZoomNow()
        {
            if (_lastZoomFrame == Time.frameCount)
                return false;

            if (Time.unscaledTime - _lastZoomClickTime < ZoomClickCooldown)
                return false;

            _lastZoomFrame = Time.frameCount;
            _lastZoomClickTime = Time.unscaledTime;
            return true;
        }

        private bool CanCycleLayoutNow()
        {
            if (Time.unscaledTime - _lastLayoutClickTime < LayoutClickCooldown)
                return false;

            _lastLayoutClickTime = Time.unscaledTime;
            return true;
        }

        public void OnRotateLeft()
        {
            if (!CanRotateNow())
                return;

            DrawingInteractionHandler.Singleton?.HandleRotateLeftStep();
        }

        public void OnRotateRight()
        {
            if (!CanRotateNow())
                return;

            DrawingInteractionHandler.Singleton?.HandleRotateRightStep();
        }

        private bool CanRotateNow()
        {
            if (Time.unscaledTime - _lastRotateClickTime < RotateClickCooldown)
                return false;

            _lastRotateClickTime = Time.unscaledTime;
            return true;
        }

        public void OnResetView()
        {
            var app = DrawingViewerApp.Singleton;
            if (app == null) return;

            var canvasManager = app.GetComponent<DrawingCanvasManager>();
            if (canvasManager != null && canvasManager.ActivePanel != null)
            {
                Camera cam = AppBootstrapper.Singleton != null
                    ? AppBootstrapper.Singleton.MainCamera
                    : DrawingViewerCamera.MainCamera;
                canvasManager.ActivePanel.ResetToDefaultView(cam);
                canvasManager.RefreshLayout();
            }
        }

        public void OnCycleLayout()
        {
            if (!CanCycleLayoutNow())
                return;

            var app = DrawingViewerApp.Singleton;
            if (app == null)
                return;

            var canvasManager = app.GetComponent<DrawingCanvasManager>();
            if (canvasManager == null)
                return;

            canvasManager.CycleLayout();
            SyncLayoutButtonLabel();

            var ui = app.GetComponent<UIManager>();
            if (ui != null)
            {
                string modeName = canvasManager.GetCurrentLayoutDisplayName();
                var doc = app.CurrentDocument;
                if (doc != null && doc.PageCount <= 1
                    && canvasManager.CurrentLayout != MultiPageLayoutStrategy.LayoutMode.Single)
                {
                    modeName += "\uff08\u4ec5\u5355\u9875\u56fe\u7eb8\uff09";
                }

                ui.ShowMessage("\u6392\u7248\uff1a" + modeName, 2f);
            }
        }

        private void SyncLayoutButtonLabel()
        {
            if (_cycleLayoutButton == null)
                return;

            var app = DrawingViewerApp.Singleton;
            var canvasManager = app != null ? app.GetComponent<DrawingCanvasManager>() : null;
            string modeName = canvasManager != null
                ? canvasManager.GetCurrentLayoutDisplayName()
                : "\u5355\u9875";

            var text = _cycleLayoutButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null)
                return;

            text.text = "\u6392\u7248\n" + modeName;
            DrawingViewerFontProvider.Apply(text);
            DrawingViewerUiFactory.ApplyCycleLayoutButtonLabelStyle(text);
        }

        public void OnOpenFile()
        {
            var app = DrawingViewerApp.Singleton;
            if (app == null)
                return;

            if (app.CurrentState == DrawingViewerApp.AppState.BrowsingFiles)
                app.HideFileBrowser();
            else
                app.ShowFileBrowser();
        }

        public void OnReturnToMenu()
        {
            if (AppShell.Singleton != null)
                AppShell.Singleton.ReturnToLauncher();
        }

        private void OnDestroy()
        {
            if (_zoomInButton != null) _zoomInButton.onClick.RemoveListener(OnZoomIn);
            if (_zoomOutButton != null) _zoomOutButton.onClick.RemoveListener(OnZoomOut);
            if (_rotateLeftButton != null) _rotateLeftButton.onClick.RemoveListener(OnRotateLeft);
            if (_rotateRightButton != null) _rotateRightButton.onClick.RemoveListener(OnRotateRight);
            if (_resetViewButton != null) _resetViewButton.onClick.RemoveListener(OnResetView);
            if (_cycleLayoutButton != null) _cycleLayoutButton.onClick.RemoveListener(OnCycleLayout);
            if (_openFileButton != null) _openFileButton.onClick.RemoveListener(OnOpenFile);
            if (_returnToMenuButton != null) _returnToMenuButton.onClick.RemoveListener(OnReturnToMenu);
        }
    }
}
