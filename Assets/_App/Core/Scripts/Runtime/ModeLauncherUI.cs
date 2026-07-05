using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.XR.XREAL.App.Core
{
    /// <summary>
    /// World-space launcher menu for selecting the active application module.
    /// </summary>
    public class ModeLauncherUI : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _canvasRoot;

        [Header("Controls")]
        [SerializeField] private Button _drawingViewerButton;
        [SerializeField] private Button _switchRecognitionButton;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Settings")]
        [SerializeField] private AppShellSettings _settings;

        public event Action OnDrawingViewerSelected;
        public event Action OnSwitchRecognitionSelected;

        private void Awake()
        {
            ResolveReferences();

            if (_drawingViewerButton != null)
                _drawingViewerButton.onClick.AddListener(HandleDrawingViewerClicked);

            if (_switchRecognitionButton != null)
                _switchRecognitionButton.onClick.AddListener(HandleSwitchRecognitionClicked);
        }

private void Start()
        {
            ConfigureCanvas();
            ApplyLabels();
            ApplyChineseFont();
            EnsureManipulator();
            ApplyTechTheme();
            RefreshPlacement();
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

public void PositionRelativeToCamera(Camera camera)
        {
            if (camera == null || _canvasRoot == null)
                return;

            float distance = _settings != null ? _settings.LauncherCanvasDistance : 0.85f;
            float verticalOffset = _settings != null ? _settings.LauncherVerticalOffset : 0.05f;

            if (_canvas != null)
                _canvas.worldCamera = camera;

            var manipulator = GetComponent<WorldSpaceUiManipulator>();
            if (manipulator != null)
            {
                if (!manipulator.ShouldFollowCamera)
                    return;

#if UNITY_EDITOR
                manipulator.SnapToCamera(camera, distance, verticalOffset);
#else
                manipulator.FollowCamera(camera, distance, verticalOffset);
#endif
                return;
            }

            Transform camTransform = camera.transform;
#if UNITY_EDITOR
            Vector3 worldPos = camTransform.position + camTransform.forward * distance + camTransform.up * verticalOffset;
            _canvasRoot.SetParent(null, true);
            _canvasRoot.SetPositionAndRotation(
                worldPos,
                Quaternion.LookRotation(worldPos - camTransform.position, Vector3.up));
#else
            _canvasRoot.SetParent(camTransform, false);
            _canvasRoot.localPosition = new Vector3(0f, verticalOffset, distance);
            _canvasRoot.localRotation = Quaternion.identity;
#endif
        }

public void RefreshPlacement()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("MainCamera");
                if (tagged != null)
                    tagged.TryGetComponent(out camera);
            }

            if (camera != null)
                PositionRelativeToCamera(camera);
        }


        private void EnsureManipulator()
        {
            if (GetComponent<WorldSpaceUiManipulator>() == null)
                gameObject.AddComponent<WorldSpaceUiManipulator>();
        }

        private void ConfigureCanvas()
        {
            if (_canvasRoot == null)
                return;

            float widthMeters = _settings != null ? _settings.LauncherCanvasWidthMeters : 1.6f;
            const float designWidth = 1600f;
            const float designHeight = 900f;

            var rect = _canvasRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(designWidth, designHeight);
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
            }

            float scale = widthMeters / designWidth;
            _canvasRoot.localScale = Vector3.one * scale;

            if (_canvas != null)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                _canvas.sortingOrder = 200;
            }
        }

private void ApplyLabels()
        {
            if (_titleText != null)
            {
#if UNITY_EDITOR
                _titleText.text = "Net Zero 工具箱";
#else
                _titleText.text = "Net Zero 工具箱\n菜单随头部转动\n拖拽空白处可移动位置";
#endif
            }

            SetButtonLabel(_drawingViewerButton, "图纸查看器");
            SetButtonLabel(_switchRecognitionButton, "图像识别器");
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                text.text = label;
        }

        private void ApplyChineseFont()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/DrawingViewerChinese SDF");
            if (font == null)
                return;

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                text.font = font;
        }

private void ApplyTechTheme()
        {
            RepairLauncherButtonSizes();
            AppUiTheme.ApplyLauncher(transform);
        }


        private void RepairLauncherButtonSizes()
        {
            var panel = transform.Find("Panel");
            if (panel == null)
                return;

            const float minPixels = 40f;
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                var rect = button.GetComponent<RectTransform>();
                if (rect == null)
                    continue;

                if (rect.rect.width < minPixels || rect.rect.height < minPixels)
                    rect.sizeDelta = new Vector2(560f, 96f);
            }
        }

        
private void ResolveReferences()
        {
            if (_canvasRoot == null)
                _canvasRoot = transform;

            if (_canvas == null)
                _canvas = _canvasRoot.GetComponent<Canvas>();

            if (_drawingViewerButton == null)
                _drawingViewerButton = transform.Find("Panel/Btn_DrawingViewer")?.GetComponent<Button>();

            if (_switchRecognitionButton == null)
                _switchRecognitionButton = transform.Find("Panel/Btn_SwitchRecognition")?.GetComponent<Button>();

            if (_titleText == null)
                _titleText = transform.Find("Panel/TitleText")?.GetComponent<TextMeshProUGUI>();
        }

        private void HandleDrawingViewerClicked()
        {
            OnDrawingViewerSelected?.Invoke();
        }

        private void HandleSwitchRecognitionClicked()
        {
            OnSwitchRecognitionSelected?.Invoke();
        }

        private void OnDestroy()
        {
            if (_drawingViewerButton != null)
                _drawingViewerButton.onClick.RemoveListener(HandleDrawingViewerClicked);

            if (_switchRecognitionButton != null)
                _switchRecognitionButton.onClick.RemoveListener(HandleSwitchRecognitionClicked);
        }
    }
}
