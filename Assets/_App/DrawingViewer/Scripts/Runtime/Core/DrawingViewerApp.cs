using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.XREAL.App.Core;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Central orchestrator for the Drawing Viewer application.
    /// Coordinates all subsystems: loading, display, interaction, and UI.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class DrawingViewerApp : SingletonMonoBehaviour<DrawingViewerApp>, IAppModule
    {
        [Header("Subsystem References")]
        [SerializeField] private DrawingLoader _drawingLoader;
        [SerializeField] private DrawingCanvasManager _canvasManager;
        [SerializeField] private DrawingInteractionHandler _interactionHandler;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private FileScanner _fileScanner;
        [SerializeField] private DrawingUploadReceiver _uploadReceiver;

        [Header("App Settings")]
        [SerializeField] private DrawingViewerSettings _settings;

        private DrawingControllerInput _controllerInput;
        private DrawingGestureController _gestureController;
        private DrawingLaserController _laserController;
        private bool _moduleInitialized;
        private bool _isNavigatingPage;

        /// <summary>
        /// Whether the drawing viewer module is currently active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// The loaded runtime settings.
        /// </summary>
        public DrawingViewerSettings Settings => _settings;

        /// <summary>
        /// Current app state.
        /// </summary>
        public AppState CurrentState { get; private set; } = AppState.Loading;

        /// <summary>
        /// Currently open document (null if none).
        /// </summary>
        public DrawingDocument CurrentDocument { get; private set; }

        /// <summary>
        /// Index of the currently visible page.
        /// </summary>
        public int CurrentPageIndex { get; private set; }

        /// <summary>
        /// List of all available documents found by the file scanner.
        /// </summary>
        public List<DrawingDocument> AvailableDocuments { get; private set; } = new List<DrawingDocument>();

        /// <summary>
        /// Event raised when the app state changes.
        /// </summary>
        public event Action<AppState, AppState> OnStateChanged;

        /// <summary>
        /// Event raised when a document is opened.
        /// </summary>
        public event Action<DrawingDocument> OnDocumentOpened;

        /// <summary>
        /// Event raised when the page changes.
        /// </summary>
        public event Action<int, int> OnPageChanged; // (newIndex, totalPages)

        /// <summary>
        /// Event raised when an error occurs.
        /// </summary>
        public event Action<string> OnError;

        /// <summary>
        /// Event raised when the available document list changes.
        /// </summary>
        public event Action OnDocumentsChanged;

        protected override void Awake()
        {
            base.Awake();

            // Auto-find subsystems if not assigned
            if (_drawingLoader == null)
                _drawingLoader = GetComponent<DrawingLoader>();

            if (_canvasManager == null)
                _canvasManager = GetComponent<DrawingCanvasManager>();

            if (_interactionHandler == null)
                _interactionHandler = GetComponent<DrawingInteractionHandler>();

            if (_uiManager == null)
                _uiManager = GetComponent<UIManager>();

            if (_fileScanner == null)
                _fileScanner = GetComponent<FileScanner>();

            if (_uploadReceiver == null)
                _uploadReceiver = GetComponent<DrawingUploadReceiver>();

            if (_controllerInput == null)
                _controllerInput = GetComponent<DrawingControllerInput>();

            if (_gestureController == null)
                _gestureController = GetComponent<DrawingGestureController>();

            if (_laserController == null)
                _laserController = GetComponent<DrawingLaserController>();

            // Load settings from Resources
            if (_settings == null)
                _settings = Resources.Load<DrawingViewerSettings>("DrawingViewerSettings");

            if (_settings == null)
            {
                Debug.LogWarning("[DrawingViewerApp] No DrawingViewerSettings found in Resources. Using defaults.");
                _settings = ScriptableObject.CreateInstance<DrawingViewerSettings>();
                _settings.ApplyDefaults();
            }

            EnsureUploadReceiver();
        }

        private void EnsureUploadReceiver()
        {
            if (_uploadReceiver == null)
            {
                _uploadReceiver = GetComponent<DrawingUploadReceiver>();
                if (_uploadReceiver == null)
                    _uploadReceiver = gameObject.AddComponent<DrawingUploadReceiver>();
            }

            _uploadReceiver.OnDrawingReceived -= HandleNetworkDrawingReceived;
            _uploadReceiver.OnDrawingReceived += HandleNetworkDrawingReceived;
            _uploadReceiver.ApplySettings(_settings);
        }

        private async void Start()
        {
            await EnsureBootstrapperReadyAsync();
            EnsureUploadReceiver();
            SetState(AppState.Ready);
            SetInteractionEnabled(false);
            _uiManager?.SetUIState(UIManager.UIState.Hidden);
            _moduleInitialized = true;
        }

        /// <summary>
        /// Activates the drawing viewer module and opens the first available document.
        /// </summary>
        public void EnterMode()
        {
            if (IsActive)
                return;

            IsActive = true;
            EnterModeAsync();
        }

        /// <summary>
        /// Deactivates the drawing viewer module and hides all viewer UI.
        /// </summary>
        public void ExitMode()
        {
            if (!IsActive && _moduleInitialized)
            {
                _uiManager?.SetUIState(UIManager.UIState.Hidden);
                SetInteractionEnabled(false);
                return;
            }

            IsActive = false;

            if (CurrentDocument != null)
            {
                _canvasManager?.ClearAllPanels();
                CurrentDocument = null;
                CurrentPageIndex = 0;
            }

            SetState(AppState.Ready);
            _uiManager?.HideMessage();
            _uiManager?.SetUIState(UIManager.UIState.Hidden);
            SetInteractionEnabled(false);
        }

        private async void EnterModeAsync()
        {
            if (!_moduleInitialized)
                await EnsureBootstrapperReadyAsync();

            SetState(AppState.Loading);
            SetInteractionEnabled(true);

            await ScanForDrawingsAsync();
            SetState(AppState.Ready);

            if (AvailableDocuments.Count > 0)
            {
                await OpenDocumentAsync(AvailableDocuments[0]);
                _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
            }
            else
            {
                _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
                _uiManager?.ShowMessage("暂无图纸，请点击「文件」上传 PNG/JPG/PDF，或使用 PC 端上传工具");
            }
        }

        private async System.Threading.Tasks.Task EnsureDocumentPagesReadyAsync(DrawingDocument document)
        {
            if (document == null || !document.IsPdf || document.PdfPageCount > 0)
                return;

            document.PdfPageCount = await PdfPageRenderer.GetPageCountAsync(document.RelativePath, document.Source);
            if (document.PdfPageCount <= 0)
                throw new Exception($"无法读取 PDF 页数：{document.Name}");
        }

        private async void HandleNetworkDrawingReceived(string importedPath, string originalFileName)
        {
            await ScanForDrawingsAsync();
            _uiManager?.RefreshFileList(AvailableDocuments);
            OnDocumentsChanged?.Invoke();

            var importedDoc = FindDocumentByImportedPath(importedPath);
            if (importedDoc == null)
            {
                _uiManager?.ShowMessage($"已接收图纸：{originalFileName}", 3f);
                return;
            }

            if (IsActive)
            {
                await OpenDocumentAsync(importedDoc);
                _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
                _uiManager?.ShowMessage($"已从 PC 接收：{importedDoc.Name}", 3f);
            }
            else
            {
                _uiManager?.ShowMessage($"图纸已接收：{importedDoc.Name}", 3f);
            }
        }

        protected override void OnDestroy()
        {
            if (_uploadReceiver != null)
                _uploadReceiver.OnDrawingReceived -= HandleNetworkDrawingReceived;

            base.OnDestroy();
        }

        private static async System.Threading.Tasks.Task EnsureBootstrapperReadyAsync()
        {
            if (AppBootstrapper.Singleton == null || AppBootstrapper.Singleton.IsInitialized)
                return;

            var initTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            AppBootstrapper.Singleton.OnInitialized += () => initTcs.TrySetResult(true);
            await initTcs.Task;
        }

        private void SetInteractionEnabled(bool drawingControlsEnabled)
        {
            if (_controllerInput != null)
                _controllerInput.enabled = drawingControlsEnabled;

            // Keep laser and hand gestures active so launcher / recognition UI remain operable.
            if (_interactionHandler != null)
                _interactionHandler.enabled = true;

            if (_gestureController != null)
                _gestureController.enabled = true;

            if (_laserController != null)
                _laserController.enabled = true;
        }

        /// <summary>
        /// Opens a document and displays its first page (awaitable).
        /// </summary>
        public async System.Threading.Tasks.Task<bool> OpenDocumentAsync(DrawingDocument document, int pageIndex = 0)
        {
            if (document == null)
            {
                OnError?.Invoke("Cannot open null document.");
                return false;
            }

            bool switchingDocument = CurrentDocument != null && !IsSameDocument(CurrentDocument, document);
            if (switchingDocument)
            {
                for (int i = 0; i < CurrentDocument.PageCount; i++)
                {
                    string oldPagePath = CurrentDocument.GetPagePath(i);
                    _drawingLoader?.UnloadTexture(oldPagePath);
                }

                _canvasManager?.ClearAllPanels();
                _canvasManager?.SetLayout(MultiPageLayoutStrategy.LayoutMode.Single);
            }

            SetState(AppState.Loading);

            try
            {
                await EnsureDocumentPagesReadyAsync(document);

                CurrentDocument = document;
                CurrentPageIndex = Mathf.Clamp(pageIndex, 0, document.PageCount - 1);
                DrawingViewerFontProvider.PreloadText(document.Name);

                string pagePath = document.GetPagePath(CurrentPageIndex);
                var texture = await _drawingLoader.LoadDrawingAsync(pagePath);

                if (texture != null)
                {
                    bool repositionPanel = switchingDocument || _canvasManager?.ActivePanel == null;
                    _canvasManager.DisplayPage(texture, CurrentPageIndex, repositionPanel: repositionPanel);
                    OnDocumentOpened?.Invoke(document);
                    OnPageChanged?.Invoke(CurrentPageIndex, document.PageCount);
                    UpdateDrawingTitle(document.Name);
                    SetState(AppState.ViewingDrawing);
                    return true;
                }

                throw new Exception($"Failed to load page: {pagePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DrawingViewerApp] Error opening document: {ex.Message}");
                OnError?.Invoke($"Failed to open document: {ex.Message}");
                SetState(AppState.Error);
                return false;
            }
        }

        private async System.Threading.Tasks.Task ScanForDrawingsAsync()
        {
            if (_fileScanner != null)
            {
                AvailableDocuments = await _fileScanner.ScanStreamingAssetsAsync();
                // Also scan persistent data for user-imported files
                var persistentDocs = await _fileScanner.ScanPersistentDataAsync();
                AvailableDocuments.AddRange(persistentDocs);
            }

            Debug.Log($"[DrawingViewerApp] Found {AvailableDocuments.Count} documents.");
            DrawingViewerFontProvider.PreloadDocumentNames(AvailableDocuments.ConvertAll(d => d.Name));
            _uiManager?.RefreshFileList(AvailableDocuments);
        }

        /// <summary>
        /// Opens a document and displays its first page.
        /// </summary>
        public async void OpenDocument(DrawingDocument document, int pageIndex = 0)
        {
            if (await OpenDocumentAsync(document, pageIndex))
                _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
        }

        /// <summary>
        /// Navigates to a specific page in the current document.
        /// </summary>
        public async void GoToPage(int pageIndex)
        {
            if (CurrentDocument == null)
                return;

            int clampedIndex = Mathf.Clamp(pageIndex, 0, CurrentDocument.PageCount - 1);

            if (clampedIndex == CurrentPageIndex || _isNavigatingPage)
                return;

            _isNavigatingPage = true;
            SetState(AppState.Loading);

            try
            {
                string pagePath = CurrentDocument.GetPagePath(clampedIndex);

                CurrentPageIndex = clampedIndex;
                _canvasManager.NavigateToPage(clampedIndex, CurrentDocument);

                var texture = await _drawingLoader.LoadDrawingAsync(pagePath);

                if (texture != null)
                {
                    _canvasManager.DisplayPage(texture, CurrentPageIndex, repositionPanel: false);
                    OnPageChanged?.Invoke(CurrentPageIndex, CurrentDocument.PageCount);
                    SetState(AppState.ViewingDrawing);
                }
                else
                {
                    throw new Exception($"Failed to load page: {pagePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DrawingViewerApp] Error navigating to page: {ex.Message}");
                OnError?.Invoke($"Failed to load page: {ex.Message}");
                SetState(AppState.Error);
            }
            finally
            {
                _isNavigatingPage = false;
            }
        }

        /// <summary>
        /// Navigates to the next page.
        /// </summary>
        public void NextPage()
        {
            if (CurrentDocument != null && CurrentPageIndex < CurrentDocument.PageCount - 1)
            {
                GoToPage(CurrentPageIndex + 1);
            }
        }

        /// <summary>
        /// Navigates to the previous page.
        /// </summary>
        public void PreviousPage()
        {
            if (CurrentDocument != null && CurrentPageIndex > 0)
            {
                GoToPage(CurrentPageIndex - 1);
            }
        }

        /// <summary>
        /// Closes the current document and returns to the file browser.
        /// </summary>
        public void CloseDocument()
        {
            _canvasManager?.ClearAllPanels();
            CurrentDocument = null;
            CurrentPageIndex = 0;

            if (AvailableDocuments.Count > 0)
            {
                SetState(AppState.BrowsingFiles);
                _uiManager?.SetUIState(UIManager.UIState.FileBrowser);
            }
            else
            {
                SetState(AppState.Ready);
                _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
            }
        }

        /// <summary>
        /// Opens the file browser to select a document.
        /// </summary>
        public void ShowFileBrowser()
        {
            if (CurrentState == AppState.BrowsingFiles)
            {
                HideFileBrowser();
                return;
            }

            _uiManager?.RefreshFileList(AvailableDocuments);
            SetState(AppState.BrowsingFiles);
            _uiManager?.SetUIState(UIManager.UIState.FileBrowser);
        }

        /// <summary>
        /// Hides the file browser and returns to the drawing view.
        /// </summary>
        public void HideFileBrowser()
        {
            if (CurrentDocument != null)
                SetState(AppState.ViewingDrawing);
            else
                SetState(AppState.Ready);

            _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
            _canvasManager?.RefreshLayout();
        }

        /// <summary>
        /// Refreshes the list of available documents.
        /// </summary>
        public async void RefreshDocuments()
        {
            await ScanForDrawingsAsync();
            _uiManager?.RefreshFileList(AvailableDocuments);
            OnDocumentsChanged?.Invoke();
        }

        /// <summary>
        /// Opens the platform file picker and imports a drawing into persistent storage.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> ImportDrawingAsync()
        {
            var picker = DrawingFilePicker.Instance;
            if (picker == null)
            {
                OnError?.Invoke("文件选择器未初始化。");
                return false;
            }

            string importedPath = await picker.PickAndImportImageAsync();
            if (string.IsNullOrEmpty(importedPath))
                return false;

            await ScanForDrawingsAsync();
            _uiManager?.RefreshFileList(AvailableDocuments);
            OnDocumentsChanged?.Invoke();

            var importedDoc = FindDocumentByImportedPath(importedPath);
            if (importedDoc != null)
            {
                await OpenDocumentAsync(importedDoc);
                _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
                _uiManager?.ShowMessage($"已导入：{importedDoc.Name}", 3f);
                return true;
            }

            _uiManager?.ShowMessage("图纸已导入", 3f);
            return true;
        }

        /// <summary>
        /// Deletes a user-imported drawing from persistent storage.
        /// Built-in APK drawings cannot be deleted.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> DeleteDocumentAsync(DrawingDocument document)
        {
            if (document == null)
                return false;

            if (!FileImportHelper.CanDelete(document))
            {
                _uiManager?.ShowMessage("内置图纸无法删除", 3f);
                return false;
            }

            bool deletingCurrent = CurrentDocument != null &&
                string.Equals(CurrentDocument.Name, document.Name, StringComparison.Ordinal) &&
                CurrentDocument.Source == document.Source;

            if (deletingCurrent)
            {
                for (int i = 0; i < CurrentDocument.PageCount; i++)
                {
                    string pagePath = CurrentDocument.GetPagePath(i);
                    _drawingLoader?.UnloadTexture(pagePath);
                }

                _canvasManager?.ClearAllPanels();
                CurrentDocument = null;
                CurrentPageIndex = 0;
            }

            if (!FileImportHelper.DeleteDocument(document))
            {
                OnError?.Invoke($"删除失败：{document.Name}");
                return false;
            }

            await ScanForDrawingsAsync();
            _uiManager?.RefreshFileList(AvailableDocuments);
            OnDocumentsChanged?.Invoke();

            if (deletingCurrent)
            {
                if (AvailableDocuments.Count > 0)
                {
                    await OpenDocumentAsync(AvailableDocuments[0]);
                    _uiManager?.SetUIState(UIManager.UIState.ToolbarOnly);
                }
                else
                {
                    SetState(AppState.Ready);
                    _uiManager?.SetUIState(UIManager.UIState.FileBrowser);
                }
            }

            _uiManager?.ShowMessage($"已删除：{document.Name}", 3f);
            return true;
        }

        private DrawingDocument FindDocumentByImportedPath(string importedPath)
        {
            if (string.IsNullOrEmpty(importedPath))
                return null;

            string importedName = System.IO.Path.GetFileNameWithoutExtension(importedPath);
            foreach (var doc in AvailableDocuments)
            {
                if (doc.Source != DrawingDocument.StorageSource.PersistentData)
                    continue;

                if (string.Equals(doc.Name, importedName, StringComparison.OrdinalIgnoreCase))
                    return doc;
            }

            return null;
        }

        private static bool IsSameDocument(DrawingDocument a, DrawingDocument b)
        {
            if (a == null || b == null)
                return false;

            return string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && a.Source == b.Source;
        }

        private void UpdateDrawingTitle(string drawingName)
        {
            var uiManager = _uiManager != null ? _uiManager : GetComponent<UIManager>();
            DrawingViewerFontProvider.PreloadText(drawingName);
            uiManager?.UpdateDrawingTitle(drawingName);
        }
        private void SetState(AppState newState)
        {
            if (CurrentState == newState)
                return;

            var oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
            Debug.Log($"[DrawingViewerApp] State: {oldState} -> {newState}");
        }

        /// <summary>
        /// Handles Android application pause/resume lifecycle.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // App going to background - AR session will auto-pause
                Debug.Log("[DrawingViewerApp] Application paused.");
            }
            else
            {
                // App resuming
                Debug.Log("[DrawingViewerApp] Application resumed.");
            }
        }

        /// <summary>
        /// Application states.
        /// </summary>
        public enum AppState
        {
            /// <summary>Initial loading / startup.</summary>
            Loading,
            /// <summary>Ready but no document open.</summary>
            Ready,
            /// <summary>Browsing files to open.</summary>
            BrowsingFiles,
            /// <summary>Viewing a drawing document.</summary>
            ViewingDrawing,
            /// <summary>An error has occurred.</summary>
            Error
        }
    }
}
