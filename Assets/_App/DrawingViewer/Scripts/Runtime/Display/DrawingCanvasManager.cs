using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    public class DrawingCanvasManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DrawingPagePool _pagePool;
        [SerializeField] private MultiPageLayoutStrategy _layoutStrategy;

        [Header("Canvas Root")]
        [SerializeField] private Transform _canvasRoot;
        [SerializeField] private Transform _worldAnchor;

        public DrawingPanelController ActivePanel { get; private set; }

        private bool _panelPlacedInWorld;
        private bool _isLoadingNeighbors;
        private DrawingPanelController.ViewState _preservedViewState = DrawingPanelController.ViewState.Invalid;
        private bool _hasPreservedViewState;
        private float _preservedZoomScale = 1f;
        private bool _hasPreservedZoomScale;
        private Coroutine _repositionCoroutine;

        private Dictionary<int, DrawingPanelController> _panelsByPage = new Dictionary<int, DrawingPanelController>();
        private List<DrawingPanelController> _allPanels = new List<DrawingPanelController>();

        public MultiPageLayoutStrategy.LayoutMode CurrentLayout { get; private set; } = MultiPageLayoutStrategy.LayoutMode.Single;

        public System.Action<DrawingPanelController> OnPanelSelected;

        private void Awake()
        {
            if (_pagePool == null)
                _pagePool = GetComponent<DrawingPagePool>();

            if (_layoutStrategy == null)
                _layoutStrategy = GetComponent<MultiPageLayoutStrategy>();

            EnsureWorldAnchor();

            if (_canvasRoot == null)
            {
                var go = new GameObject("DrawingCanvasRoot");
                _canvasRoot = go.transform;
            }

            _canvasRoot.SetParent(_worldAnchor, false);
        }

        private void EnsureWorldAnchor()
        {
            if (_worldAnchor == null)
            {
                var anchorGo = new GameObject("DrawingWorldAnchor");
                _worldAnchor = anchorGo.transform;
            }

            if (_worldAnchor.parent != null)
                _worldAnchor.SetParent(null, true);
        }

        private void Start()
        {
            EnsureWorldAnchor();
            SetLayout(MultiPageLayoutStrategy.LayoutMode.Single);
        }

        public void DisplayPage(
            Texture2D texture,
            int pageIndex,
            bool repositionPanel = false,
            bool setAsActive = true,
            bool applyLayout = true)
        {
            if (texture == null) return;

            EnsureWorldAnchor();

            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;

            if (_panelsByPage.TryGetValue(pageIndex, out var existingController))
            {
                existingController.PageDisplay.SetTexture(texture);
                existingController.PageDisplay.PageIndex = pageIndex;

                if (setAsActive)
                    ActivePanel = existingController;

                if (applyLayout && _layoutStrategy != null)
                    _layoutStrategy.ApplyLayout(_allPanels, GetActivePageIndex(), cam);

                if (!repositionPanel)
                    RestorePreservedViewState(existingController);

                if (setAsActive)
                    TryLoadLayoutNeighbors();

                return;
            }

            var display = _pagePool.GetPageDisplay();
            display.SetTexture(texture);
            display.PageIndex = pageIndex;

            var controller = display.GetComponent<DrawingPanelController>();
            if (controller == null)
                controller = display.gameObject.AddComponent<DrawingPanelController>();

            display.transform.SetParent(_canvasRoot, false);

            bool shouldReposition = repositionPanel || !_panelPlacedInWorld;
            if (shouldReposition)
            {
                controller.ResetToDefaultView(cam);
                _panelPlacedInWorld = true;
                _hasPreservedViewState = false;
            }
            else if (ActivePanel != null && ActivePanel != controller)
            {
                controller.CopyWorldTransformFrom(ActivePanel);
            }
            else if (!_hasPreservedViewState)
            {
                if (ActivePanel == controller)
                    controller.SnapToTargets();
                else
                {
                    controller.ResetToDefaultView(cam);
                    _panelPlacedInWorld = true;
                }
            }

            _panelsByPage[pageIndex] = controller;
            if (!_allPanels.Contains(controller))
                _allPanels.Add(controller);

            if (setAsActive)
                ActivePanel = controller;

            if (applyLayout && _layoutStrategy != null)
                _layoutStrategy.ApplyLayout(_allPanels, GetActivePageIndex(), cam);

            if (!shouldReposition)
                RestorePreservedViewState(controller);

            if (setAsActive)
                TryLoadLayoutNeighbors();

            if (shouldReposition && setAsActive)
                StartRepositionWhenCameraReady(controller);

            Debug.Log($"[DrawingCanvasManager] Displayed page {pageIndex} (active={setAsActive}) at {controller.transform.position}");
        }

        private int GetActivePageIndex()
        {
            var app = DrawingViewerApp.Singleton;
            if (app != null && app.CurrentDocument != null)
                return app.CurrentPageIndex;

            if (ActivePanel != null)
                return ActivePanel.PageDisplay.PageIndex;

            return 0;
        }

        private int GetLayoutKeepRadius()
        {
            switch (CurrentLayout)
            {
                case MultiPageLayoutStrategy.LayoutMode.Spread:
                    return 2;
                case MultiPageLayoutStrategy.LayoutMode.SideBySide:
                    return 1;
                default:
                    return 0;
            }
        }

        private System.Collections.IEnumerator RepositionPanelWhenCameraReady(DrawingPanelController controller)
        {
            const int totalFrames = 45;
            for (int i = 0; i < totalFrames; i++)
            {
                yield return null;

                if (controller == null || controller != ActivePanel)
                    yield break;

                if (Mathf.Abs(controller.GetVisualScaleMultiplier() - 1f) > 0.01f)
                    yield break;

                if (i == 0 || i == 10 || i == 25 || i == 44)
                {
                    Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
                    if (cam != null)
                    {
                        controller.ResetToDefaultView(cam);
                        RefreshLayout();
                    }
                }
            }

            _repositionCoroutine = null;
        }

        private void StartRepositionWhenCameraReady(DrawingPanelController controller)
        {
            StopRepositionCoroutine();
            _repositionCoroutine = StartCoroutine(RepositionPanelWhenCameraReady(controller));
        }

        private void StopRepositionCoroutine()
        {
            if (_repositionCoroutine == null)
                return;

            StopCoroutine(_repositionCoroutine);
            _repositionCoroutine = null;
        }

        private void RestorePreservedViewState(DrawingPanelController controller)
        {
            if (controller == null || !_hasPreservedViewState)
                return;

            Debug.Log($"[ViewStateDebug] Restore to page={controller.PageDisplay.PageIndex}, preservedScale={_preservedZoomScale}, beforeScale={controller.CurrentScale}, beforeVisual={controller.GetVisualScaleMultiplier()}");

            controller.ApplyViewState(_preservedViewState);

            if (_hasPreservedZoomScale)
            {
                controller.SetZoom(_preservedZoomScale);
                controller.SnapToTargets();
            }

            Debug.Log($"[ViewStateDebug] After restore page={controller.PageDisplay.PageIndex}, afterScale={controller.CurrentScale}, afterVisual={controller.GetVisualScaleMultiplier()}");
            StartCoroutine(LogViewStateNextFrame(controller));
        }

        private System.Collections.IEnumerator LogViewStateNextFrame(DrawingPanelController controller)
        {
            yield return null;
            if (controller == null)
                yield break;

            Debug.Log($"[ViewStateDebug] Next frame page={controller.PageDisplay.PageIndex}, afterScale={controller.CurrentScale}, afterVisual={controller.GetVisualScaleMultiplier()}");
        }

        public void RepositionActivePanel()
        {
            if (ActivePanel == null)
                return;

            StopRepositionCoroutine();

            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (cam != null)
            {
                ActivePanel.ResetToDefaultView(cam);
                RefreshLayout();
                _hasPreservedViewState = false;
                _hasPreservedZoomScale = false;
                _preservedZoomScale = 1f;
            }
        }

        public void RefreshLayout()
        {
            ApplyCurrentLayout();
        }

        public void UpdateActivePanelTitle(string title)
        {
            if (ActivePanel == null) return;

            DrawingViewerFontProvider.PreloadText(title);
            var label = DrawingPanelTitleLabel.EnsureOnPanel(ActivePanel);
            label?.SetTitle(title);
        }

        public void ClearActivePanelTitle()
        {
            if (ActivePanel == null) return;

            var label = ActivePanel.GetComponentInChildren<DrawingPanelTitleLabel>(true);
            if (label != null)
                label.SetTitle(null);
        }

        public void PreloadPage(int pageIndex, DrawingDocument document)
        {
            if (document == null) return;

            var loader = DrawingViewerApp.Singleton?.GetComponent<DrawingLoader>();
            if (loader != null)
            {
                string path = document.GetPagePath(pageIndex);
                if (!string.IsNullOrEmpty(path))
                    _ = loader.LoadDrawingAsync(path);
            }
        }

        public void NavigateToPage(int newPageIndex, DrawingDocument document)
        {
            if (document == null) return;

            StopRepositionCoroutine();
            CaptureActiveViewState();

            int keepRadius = GetLayoutKeepRadius();

            var pagesToRemove = new List<int>();
            foreach (var kvp in _panelsByPage)
            {
                if (Mathf.Abs(kvp.Key - newPageIndex) > keepRadius)
                {
                    _pagePool.ReturnPageDisplay(kvp.Value.PageDisplay);
                    pagesToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in pagesToRemove)
            {
                _allPanels.Remove(_panelsByPage[key]);
                _panelsByPage.Remove(key);
            }

            var loader = DrawingViewerApp.Singleton?.GetComponent<DrawingLoader>();
            loader?.UnloadDistantTextures(document, newPageIndex);

            for (int i = newPageIndex - keepRadius; i <= newPageIndex + keepRadius; i++)
            {
                if (i >= 0 && i < document.PageCount && i != newPageIndex)
                    PreloadPage(i, document);
            }
        }

        public void ClearAllPanels()
        {
            StopRepositionCoroutine();

            foreach (var panel in _allPanels)
            {
                if (panel != null && panel.PageDisplay != null)
                    _pagePool.ReturnPageDisplay(panel.PageDisplay);
            }

            _allPanels.Clear();
            _panelsByPage.Clear();
            ActivePanel = null;
            _panelPlacedInWorld = false;
            _hasPreservedViewState = false;
            _preservedViewState = DrawingPanelController.ViewState.Invalid;
            _hasPreservedZoomScale = false;
            _preservedZoomScale = 1f;
        }

        private void CaptureActiveViewState()
        {
            if (ActivePanel == null || !ActivePanel.PageDisplay.IsLoaded)
                return;

            _preservedViewState = ActivePanel.CaptureViewState();
            _hasPreservedViewState = _preservedViewState.IsValid;
            _preservedZoomScale = ActivePanel.CurrentScale;
            _hasPreservedZoomScale = true;

            Debug.Log($"[ViewStateDebug] Capture page={ActivePanel.PageDisplay.PageIndex}, currentScale={ActivePanel.CurrentScale}, visualScale={ActivePanel.GetVisualScaleMultiplier()}");
        }

        public void SetLayout(MultiPageLayoutStrategy.LayoutMode mode)
        {
            CurrentLayout = mode;

            if (_layoutStrategy != null)
                _layoutStrategy.CurrentMode = mode;

            if (mode == MultiPageLayoutStrategy.LayoutMode.Single)
                ApplyCurrentLayout();

            LoadNeighborsAndApplyLayout();
            Debug.Log($"[DrawingCanvasManager] Layout set to: {GetCurrentLayoutDisplayName()}");
        }

        public void CycleLayout()
        {
            int nextMode = ((int)CurrentLayout + 1) % 3;
            SetLayout((MultiPageLayoutStrategy.LayoutMode)nextMode);
        }

        public string GetCurrentLayoutDisplayName()
        {
            return MultiPageLayoutStrategy.GetDisplayName(CurrentLayout);
        }

        private void ApplyCurrentLayout()
        {
            if (_layoutStrategy == null || _allPanels.Count == 0)
                return;

            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            _layoutStrategy.ApplyLayout(_allPanels, GetActivePageIndex(), cam);
        }

        private async void LoadNeighborsAndApplyLayout()
        {
            await EnsureNeighborPagesLoadedAsync();
            ApplyCurrentLayout();
        }

        private async System.Threading.Tasks.Task EnsureNeighborPagesLoadedAsync()
        {
            if (_layoutStrategy == null || ActivePanel == null)
                return;

            if (_layoutStrategy.CurrentMode == MultiPageLayoutStrategy.LayoutMode.Single)
                return;

            var app = DrawingViewerApp.Singleton;
            var doc = app?.CurrentDocument;
            var loader = app?.GetComponent<DrawingLoader>();
            if (doc == null || loader == null || doc.PageCount <= 1)
                return;

            int activeIndex = GetActivePageIndex();
            _isLoadingNeighbors = true;

            try
            {
                foreach (int pageIndex in MultiPageLayoutStrategy.GetRequiredNeighborIndices(
                             _layoutStrategy.CurrentMode, activeIndex, doc.PageCount))
                {
                    if (_panelsByPage.ContainsKey(pageIndex))
                        continue;

                    string path = doc.GetPagePath(pageIndex);
                    var texture = await loader.LoadDrawingAsync(path);
                    if (texture == null)
                        continue;

                    DisplayPage(texture, pageIndex, repositionPanel: false, setAsActive: false, applyLayout: false);
                }
            }
            finally
            {
                _isLoadingNeighbors = false;
            }
        }

        private void TryLoadLayoutNeighbors()
        {
            if (_isLoadingNeighbors || _layoutStrategy == null)
                return;

            if (_layoutStrategy.CurrentMode == MultiPageLayoutStrategy.LayoutMode.Single)
                return;

            LoadNeighborsAndApplyLayout();
        }

        public DrawingPanelController GetPanel(int pageIndex)
        {
            _panelsByPage.TryGetValue(pageIndex, out var panel);
            return panel;
        }

        private void OnDestroy()
        {
            ClearAllPanels();
        }
    }
}
