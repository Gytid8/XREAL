using TMPro;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// World-space title label rendered below the active drawing panel.
    /// </summary>
    public class DrawingPanelTitleLabel : MonoBehaviour
    {
        [SerializeField] private float _gapBelowPanelMeters = 0.06f;
        [SerializeField] private float _fontSize = 0.12f;

        private TextMeshPro _text;
        private DrawingPanelController _panel;
        private string _title = string.Empty;

        public static DrawingPanelTitleLabel EnsureOnPanel(DrawingPanelController panel)
        {
            if (panel == null) return null;

            var existing = panel.GetComponentInChildren<DrawingPanelTitleLabel>(true);
            if (existing != null)
            {
                existing._panel = panel;
                return existing;
            }

            var labelGo = new GameObject("DrawingTitleLabel");
            labelGo.transform.SetParent(panel.transform, false);

            var label = labelGo.AddComponent<DrawingPanelTitleLabel>();
            label._panel = panel;
            label.EnsureTextComponent();
            return label;
        }

        private void Awake()
        {
            if (_panel == null)
                _panel = GetComponentInParent<DrawingPanelController>();

            EnsureTextComponent();
        }

        private void EnsureTextComponent()
        {
            if (_text != null) return;

            _text = GetComponent<TextMeshPro>();
            if (_text == null)
                _text = gameObject.AddComponent<TextMeshPro>();

            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = _fontSize;
            _text.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            _text.overflowMode = TextOverflowModes.Ellipsis;
            _text.enableWordWrapping = false;
            _text.rectTransform.sizeDelta = new Vector2(2.4f, 0.2f);

            DrawingViewerFontProvider.Apply(_text);
        }

        public void SetTitle(string title)
        {
            _title = string.IsNullOrEmpty(title) ? "\u672a\u6253\u5f00\u56fe\u7eb8" : title;
            EnsureTextComponent();

            if (_text != null)
            {
                _text.text = _title;
                DrawingViewerFontProvider.Apply(_text);
            }

            gameObject.SetActive(!string.IsNullOrEmpty(title));
        }

        private void LateUpdate()
        {
            if (_panel == null || _text == null)
                return;

            var display = _panel.PageDisplay;
            if (display == null || !display.IsLoaded)
            {
                gameObject.SetActive(false);
                return;
            }

            float halfHeight = Mathf.Max(0.01f, _panel.transform.localScale.y * 0.5f);
            transform.localPosition = new Vector3(0f, -halfHeight - _gapBelowPanelMeters, -0.005f);
            transform.localRotation = Quaternion.identity;

            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (cam != null)
            {
                Vector3 toCamera = cam.transform.position - transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            }
        }
    }
}
