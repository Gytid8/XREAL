using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.XR.XREAL.DrawingViewer
{
    public class FileBrowserUI : MonoBehaviour
    {
        [Header("List Display")]
        [SerializeField] private Transform _listContentParent;
        [SerializeField] private GameObject _listItemPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Controls")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _importButton;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _emptyHintText;

        [Header("Settings")]
        [SerializeField] private float _itemSpacing = 8f;
        [SerializeField] private float _itemHeightPixels = 56f;

        private List<DrawingDocument> _documents = new List<DrawingDocument>();
        private List<GameObject> _listItemInstances = new List<GameObject>();
        private bool _importInProgress;

        private void Awake()
        {
            ResolveReferences();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClose);

            if (_refreshButton != null)
                _refreshButton.onClick.AddListener(OnRefresh);

            if (_importButton != null)
                _importButton.onClick.AddListener(OnImport);

            var rootImage = GetComponent<Image>();
            if (rootImage != null)
                rootImage.raycastTarget = true;
        }

        private void ResolveReferences()
        {
            if (_listContentParent == null)
            {
                var content = transform.Find("ScrollView/Viewport/Content");
                if (content != null)
                    _listContentParent = content;
            }

            if (_scrollRect == null)
                _scrollRect = transform.Find("ScrollView")?.GetComponent<ScrollRect>();

            if (_closeButton == null)
                _closeButton = transform.Find("Header/Btn_Close")?.GetComponent<Button>();

            if (_refreshButton == null)
                _refreshButton = transform.Find("Header/Btn_Refresh")?.GetComponent<Button>();

            if (_importButton == null)
                _importButton = transform.Find("Header/Btn_Import")?.GetComponent<Button>();

            if (_titleText == null)
                _titleText = transform.Find("Header/TitleText")?.GetComponent<TextMeshProUGUI>();

            if (_emptyHintText == null)
                _emptyHintText = transform.Find("EmptyHintText")?.GetComponent<TextMeshProUGUI>();
        }

        private void ApplyTechTheme()
        {
            Unity.XR.XREAL.App.Core.AppUiTheme.ApplyFileBrowser(transform);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyTechTheme();
            RefreshFromApp();
        }

        private void Start()
        {
            ApplyTechTheme();
            RefreshFromApp();
        }

        private void RefreshFromApp()
        {
            if (_listContentParent == null)
            {
                Debug.LogError("[FileBrowserUI] List content parent is not assigned.");
                return;
            }

            var app = DrawingViewerApp.Singleton;
            if (app != null)
                SetDocuments(app.AvailableDocuments);
        }

        public void RefreshFromAppPublic() => RefreshFromApp();

        public void SetDocuments(List<DrawingDocument> documents)
        {
            ClearList();
            _documents = documents ?? new List<DrawingDocument>();

            var names = new List<string>(_documents.Count);
            foreach (var doc in _documents)
            {
                if (!string.IsNullOrEmpty(doc?.Name))
                    names.Add(doc.Name);
                CreateListItem(doc);
            }

            DrawingViewerFontProvider.PreloadDocumentNames(names);

            if (_titleText != null)
            {
                _titleText.text = _documents.Count > 0
                    ? $"\u9009\u62e9\u56fe\u7eb8 ({_documents.Count})"
                    : "\u9009\u62e9\u56fe\u7eb8";
                DrawingViewerFontProvider.Apply(_titleText);
            }

            if (_emptyHintText != null)
            {
                bool showHint = _documents.Count == 0;
                _emptyHintText.gameObject.SetActive(showHint);
                if (showHint)
                {
                    _emptyHintText.text = "\u6682\u65e0\u56fe\u7eb8\n\u70b9\u51fb\u53f3\u4e0a\u89d2 + \u4e0a\u4f20\u56fe\u7eb8";
                    DrawingViewerFontProvider.Apply(_emptyHintText);
                }
            }

            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;

            Debug.Log($"[FileBrowserUI] Displaying {_documents.Count} documents");
        }

        private void CreateListItem(DrawingDocument document)
        {
            GameObject itemGo = _listItemPrefab != null
                ? Instantiate(_listItemPrefab, _listContentParent)
                : CreateDefaultListItem(document);

            itemGo.name = $"FileItem_{document.Name}";

            var openButton = itemGo.transform.Find("OpenArea")?.GetComponent<Button>();
            if (openButton == null)
                openButton = itemGo.GetComponent<Button>();

            if (openButton != null)
            {
                EnsureButtonRaycastTarget(openButton);
                openButton.onClick.AddListener(() => OnDocumentSelected(document));
            }

            var label = itemGo.transform.Find("OpenArea/LabelText")?.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = itemGo.transform.Find("LabelText")?.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = itemGo.GetComponentInChildren<TextMeshProUGUI>();

            string displayText = null;
            if (label != null)
            {
                string sourceTag = FileImportHelper.CanDelete(document) ? "\u672c\u5730" : "\u5185\u7f6e";
                displayText = $"{document.Name}  ({document.PageCount}\u9875 \u00b7 {sourceTag})";
                label.text = displayText;
            }

            var deleteButton = itemGo.transform.Find("Btn_Delete")?.GetComponent<Button>();
            if (deleteButton != null)
            {
                bool canDelete = FileImportHelper.CanDelete(document);
                deleteButton.gameObject.SetActive(canDelete);
                if (canDelete)
                    deleteButton.onClick.AddListener(() => OnDeleteRequested(document));
            }

            var layoutElement = itemGo.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = itemGo.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = _itemHeightPixels;

            if (_listContentParent.GetComponent<VerticalLayoutGroup>() == null)
            {
                var rectTransform = itemGo.GetComponent<RectTransform>();
                int index = _listItemInstances.Count;
                float rowHeight = _itemHeightPixels + _itemSpacing;
                rectTransform.anchoredPosition = new Vector2(0, -index * rowHeight);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _itemHeightPixels);
            }

            Unity.XR.XREAL.App.Core.AppUiTheme.ApplyFileListItem(itemGo.transform);

            if (label != null)
                DrawingViewerFontProvider.Apply(label);

            if (displayText != null)
                DrawingViewerFontProvider.PreloadText(displayText);

            _listItemInstances.Add(itemGo);
        }

        private static void EnsureButtonRaycastTarget(Button button)
        {
            if (button == null)
                return;

            if (button.targetGraphic != null)
                return;

            var image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.01f);
                image.raycastTarget = true;
            }

            button.targetGraphic = image;
        }

        private GameObject CreateDefaultListItem(DrawingDocument document)
        {
            var go = new GameObject($"FileItem_{document.Name}");
            go.transform.SetParent(_listContentParent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);

            var image = go.AddComponent<Image>();
            image.color = Unity.XR.XREAL.App.Core.AppUiTheme.ListItemFill;

            var openArea = new GameObject("OpenArea");
            openArea.transform.SetParent(go.transform, false);
            var openRect = openArea.AddComponent<RectTransform>();
            openRect.anchorMin = Vector2.zero;
            openRect.anchorMax = Vector2.one;
            openRect.offsetMin = new Vector2(0, 0);
            openRect.offsetMax = new Vector2(-72, 0);

            var openImage = openArea.AddComponent<Image>();
            openImage.color = new Color(1f, 1f, 1f, 0.01f);
            openImage.raycastTarget = true;

            var openButton = openArea.AddComponent<Button>();
            openButton.targetGraphic = openImage;
            var openColors = openButton.colors;
            openColors.highlightedColor = new Color(0.5f, 0.55f, 0.65f, 1f);
            openColors.pressedColor = new Color(0.25f, 0.45f, 0.75f, 1f);
            openButton.colors = openColors;

            var textGo = new GameObject("LabelText");
            textGo.transform.SetParent(openArea.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 5);
            textRect.offsetMax = new Vector2(-8, -5);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;

            if (FileImportHelper.CanDelete(document))
            {
                var deleteGo = new GameObject("Btn_Delete");
                deleteGo.transform.SetParent(go.transform, false);
                var deleteRect = deleteGo.AddComponent<RectTransform>();
                deleteRect.anchorMin = new Vector2(1, 0.5f);
                deleteRect.anchorMax = new Vector2(1, 0.5f);
                deleteRect.pivot = new Vector2(1, 0.5f);
                deleteRect.anchoredPosition = new Vector2(-8, 0);
                deleteRect.sizeDelta = new Vector2(56, 40);

                var deleteImage = deleteGo.AddComponent<Image>();
                deleteImage.color = new Color(0.65f, 0.2f, 0.2f, 0.95f);

                var deleteButton = deleteGo.AddComponent<Button>();
                var deleteColors = deleteButton.colors;
                deleteColors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
                deleteColors.pressedColor = new Color(0.45f, 0.1f, 0.1f, 1f);
                deleteButton.colors = deleteColors;

                var deleteTextGo = new GameObject("Text");
                deleteTextGo.transform.SetParent(deleteGo.transform, false);
                var deleteTextRect = deleteTextGo.AddComponent<RectTransform>();
                deleteTextRect.anchorMin = Vector2.zero;
                deleteTextRect.anchorMax = Vector2.one;
                deleteTextRect.offsetMin = Vector2.zero;
                deleteTextRect.offsetMax = Vector2.zero;

                var deleteText = deleteTextGo.AddComponent<TextMeshProUGUI>();
                deleteText.text = "\u5220\u9664";
                deleteText.fontSize = 20;
                deleteText.color = Color.white;
                deleteText.alignment = TextAlignmentOptions.Center;
                deleteText.raycastTarget = false;
                DrawingViewerFontProvider.Apply(deleteText);
            }

            return go;
        }

        private async void OnDocumentSelected(DrawingDocument document)
        {
            if (document == null)
                return;

            Debug.Log($"[FileBrowserUI] Selected: {document.Name}");

            var app = DrawingViewerApp.Singleton;
            if (app == null)
                return;

            bool opened = await app.OpenDocumentAsync(document);
            if (opened)
                app.HideFileBrowser();
        }

        private async void OnDeleteRequested(DrawingDocument document)
        {
            var app = DrawingViewerApp.Singleton;
            if (app == null || document == null)
                return;

            await app.DeleteDocumentAsync(document);
        }

        public async void OnImport()
        {
            if (_importInProgress)
                return;

            var app = DrawingViewerApp.Singleton;
            if (app == null)
                return;

            _importInProgress = true;
            try
            {
                await app.ImportDrawingAsync();
            }
            finally
            {
                _importInProgress = false;
            }
        }

        public void OnClose()
        {
            DrawingViewerApp.Singleton?.HideFileBrowser();
        }

        public void OnRefresh()
        {
            DrawingViewerApp.Singleton?.RefreshDocuments();
        }

        private void ClearList()
        {
            foreach (var instance in _listItemInstances)
            {
                if (instance != null)
                    Destroy(instance);
            }
            _listItemInstances.Clear();
        }

        private void OnDestroy()
        {
            ClearList();

            if (_closeButton != null) _closeButton.onClick.RemoveListener(OnClose);
            if (_refreshButton != null) _refreshButton.onClick.RemoveListener(OnRefresh);
            if (_importButton != null) _importButton.onClick.RemoveListener(OnImport);
        }
    }
}
