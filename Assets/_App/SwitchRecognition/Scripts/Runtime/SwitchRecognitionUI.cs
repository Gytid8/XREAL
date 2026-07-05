using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.XREAL.App.Core;
using Unity.XR.XREAL.DrawingViewer;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// UI shell for the switch recognition module.
    /// </summary>
    public class SwitchRecognitionUI : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _canvasRoot;

        [Header("Controls")]
        [SerializeField] private Button _returnToMenuButton;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Layout")]
        [SerializeField] private float _canvasWidthMeters = 1.6f;
        [SerializeField] private float _canvasDistance = 0.8f;
        [SerializeField] private float _verticalOffset = -0.02f;

        private void Awake()
        {
            ResolveReferences();

            if (_returnToMenuButton != null)
                _returnToMenuButton.onClick.AddListener(OnReturnToMenu);
        }

private void Start()
        {
            ConfigureCanvas();
            ApplyChineseFont();
            AppUiTheme.ApplyRecognitionToolbar(transform.Find("Toolbar"));

            if (_titleText != null)
                _titleText.text = "图像识别器";
        }

        public void Show()
        {
            if (_canvasRoot != null)
                _canvasRoot.gameObject.SetActive(true);

            if (_canvas != null)
                _canvas.enabled = true;
        }

        public void Hide()
        {
            if (_canvas != null)
                _canvas.enabled = false;

            if (_canvasRoot != null)
                _canvasRoot.gameObject.SetActive(false);
        }

        public void SetStatusMessage(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        public void PositionRelativeToCamera(Camera camera)
        {
            if (camera == null || _canvasRoot == null)
                return;

            Transform camTransform = camera.transform;
            _canvasRoot.position = camTransform.position
                + camTransform.forward * _canvasDistance
                + camTransform.up * _verticalOffset;
            _canvasRoot.rotation = Quaternion.LookRotation(
                _canvasRoot.position - camTransform.position,
                Vector3.up);

            if (_canvas != null && _canvas.worldCamera == null)
                _canvas.worldCamera = camera;
        }

        private void ConfigureCanvas()
        {
            if (_canvasRoot == null)
                return;

            const float designWidth = 1600f;
            const float designHeight = 900f;

            var rect = _canvasRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(designWidth, designHeight);
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
            }

            float scale = _canvasWidthMeters / designWidth;
            _canvasRoot.localScale = Vector3.one * scale;

            if (_canvas != null)
                _canvas.renderMode = RenderMode.WorldSpace;
        }

        private void ApplyChineseFont()
        {
            if (_canvasRoot != null)
                DrawingViewerFontProvider.ApplyAllInChildren(_canvasRoot);
        }

        private void ResolveReferences()
        {
            if (_canvasRoot == null)
                _canvasRoot = transform;

            if (_canvas == null)
                _canvas = _canvasRoot.GetComponent<Canvas>();

            if (_returnToMenuButton == null)
                _returnToMenuButton = transform.Find("Toolbar/Btn_ReturnToMenu")?.GetComponent<Button>();

            if (_statusText == null)
                _statusText = transform.Find("Toolbar/StatusText")?.GetComponent<TextMeshProUGUI>();

            if (_titleText == null)
                _titleText = transform.Find("Toolbar/TitleText")?.GetComponent<TextMeshProUGUI>();
        }

        private void OnReturnToMenu()
        {
            if (AppShell.Singleton != null)
                AppShell.Singleton.ReturnToLauncher();
        }

        private void OnDestroy()
        {
            if (_returnToMenuButton != null)
                _returnToMenuButton.onClick.RemoveListener(OnReturnToMenu);
        }
    }
}
