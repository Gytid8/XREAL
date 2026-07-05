using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.XR.XREAL.DrawingViewer
{
    public class PageNavigatorUI : MonoBehaviour
    {
        [Header("Navigation Buttons")]
        [SerializeField] private Button _previousPageButton;
        [SerializeField] private Button _nextPageButton;

        [Header("Page Counter")]
        [SerializeField] private TextMeshProUGUI _pageCounterText;

        [Header("Customization")]
        [SerializeField] private string _pageCounterFormat = "{0} / {1}";

        public int CurrentPage { get; private set; }
        public int TotalPages { get; private set; }

        private float _lastNavClickTime;
        private const float NavClickCooldown = 0.35f;

        private void Awake()
        {
            RefreshButtonBindings();
        }

        private void Start()
        {
            var app = DrawingViewerApp.Singleton;
            if (app != null)
            {
                app.OnPageChanged += OnPageChanged;
                app.OnDocumentOpened += OnDocumentOpened;
            }

            UpdateButtonStates();
        }

        public void RefreshButtonBindings()
        {
            DrawingViewerUiFactory.EnsurePageNavigatorButtons(transform);
            ResolveReferences();
            WireButtonListeners();
            Unity.XR.XREAL.App.Core.AppUiTheme.ApplyPageNavigator(transform);
            DrawingViewerFontProvider.ApplyAllInChildren(transform);
        }

        private void ResolveReferences()
        {
            if (_previousPageButton == null)
            {
                var prev = transform.Find("Btn_Previous");
                if (prev != null)
                    _previousPageButton = prev.GetComponent<Button>();
            }

            if (_nextPageButton == null)
            {
                var next = transform.Find("Btn_Next");
                if (next != null)
                    _nextPageButton = next.GetComponent<Button>();
            }

            if (_pageCounterText == null)
            {
                var counter = transform.Find("PageCounter");
                if (counter != null)
                    _pageCounterText = counter.GetComponent<TextMeshProUGUI>();
            }
        }

        private void WireButtonListeners()
        {
            if (_previousPageButton != null)
            {
                _previousPageButton.onClick.RemoveListener(OnPreviousPage);
                _previousPageButton.onClick.AddListener(OnPreviousPage);
            }

            if (_nextPageButton != null)
            {
                _nextPageButton.onClick.RemoveListener(OnNextPage);
                _nextPageButton.onClick.AddListener(OnNextPage);
            }
        }

        public void UpdatePageDisplay(int currentPage, int totalPages)
        {
            CurrentPage = currentPage;
            TotalPages = totalPages;

            if (_pageCounterText != null)
                _pageCounterText.text = string.Format(_pageCounterFormat, currentPage + 1, totalPages);

            UpdateButtonStates();
        }

        private bool CanNavigateNow()
        {
            if (Time.unscaledTime - _lastNavClickTime < NavClickCooldown)
                return false;

            _lastNavClickTime = Time.unscaledTime;
            return true;
        }

        public void OnPreviousPage()
        {
            if (!CanNavigateNow())
                return;

            DrawingViewerApp.Singleton?.PreviousPage();
        }

        public void OnNextPage()
        {
            if (!CanNavigateNow())
                return;

            DrawingViewerApp.Singleton?.NextPage();
        }

        private void UpdateButtonStates()
        {
            if (_previousPageButton != null)
                _previousPageButton.interactable = CurrentPage > 0;

            if (_nextPageButton != null)
                _nextPageButton.interactable = CurrentPage < TotalPages - 1;
        }

        private void OnDocumentOpened(DrawingDocument document)
        {
            UpdatePageDisplay(0, document.PageCount);
        }

        private void OnPageChanged(int pageIndex, int totalPages)
        {
            UpdatePageDisplay(pageIndex, totalPages);
        }

        private void OnDestroy()
        {
            if (_previousPageButton != null) _previousPageButton.onClick.RemoveListener(OnPreviousPage);
            if (_nextPageButton != null) _nextPageButton.onClick.RemoveListener(OnNextPage);

            var app = DrawingViewerApp.Singleton;
            if (app != null)
            {
                app.OnPageChanged -= OnPageChanged;
                app.OnDocumentOpened -= OnDocumentOpened;
            }
        }
    }
}
