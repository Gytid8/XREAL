using TMPro;
using UnityEngine;
using Unity.XR.XREAL.DrawingViewer;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// World-space popup label shown above a detected switch.
    /// </summary>
    public class SwitchLabelOverlay : MonoBehaviour
    {
        [SerializeField] private float _fontSize = 0.08f;
        [SerializeField] private Vector3 _panelSize = new Vector3(0.34f, 0.12f, 0.01f);

        private Transform _background;
        private TextMeshPro _titleText;
        private TextMeshPro _confidenceText;
        private int _trackId = -1;

        public int TrackId => _trackId;

        public static SwitchLabelOverlay Create(Transform parent)
        {
            var root = new GameObject("SwitchLabelOverlay");
            root.transform.SetParent(parent, false);
            var overlay = root.AddComponent<SwitchLabelOverlay>();
            overlay.BuildVisuals();
            return overlay;
        }

        private void BuildVisuals()
        {
            _background = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            _background.name = "Background";
            _background.SetParent(transform, false);
            _background.localScale = _panelSize;

            var collider = _background.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = _background.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
                renderer.sharedMaterial.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            }

            _titleText = CreateText("TitleText", new Vector3(0f, 0.015f, -0.006f), 0.9f);
            _confidenceText = CreateText("ConfidenceText", new Vector3(0f, -0.03f, -0.006f), 0.55f);
        }

        private TextMeshPro CreateText(string name, Vector3 localPosition, float sizeScale)
        {
            var textGo = new GameObject(name);
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = localPosition;

            var text = textGo.AddComponent<TextMeshPro>();
            var font = DrawingViewerFontProvider.GetFont();
            if (font != null)
                text.font = font;

            text.fontSize = _fontSize * sizeScale;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(0.32f, 0.08f);
            return text;
        }

        public void SetDetection(SwitchDetection detection, SwitchClassDefinition.SwitchClassEntry classEntry)
        {
            _trackId = detection.TrackId;
            transform.position = detection.WorldPosition;

            if (_titleText != null)
                _titleText.text = detection.DisplayName;

            if (_confidenceText != null)
                _confidenceText.text = $"{detection.Confidence * 100f:0}%";

            if (_background != null)
            {
                var renderer = _background.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    Color baseColor = classEntry.labelColor;
                    renderer.sharedMaterial.color = new Color(baseColor.r * 0.25f, baseColor.g * 0.25f, baseColor.b * 0.25f, 0.92f);
                }
            }

            if (_titleText != null)
            {
                Color accent = classEntry.labelColor;
                if (accent.a <= 0.001f)
                    accent = new Color(0.35f, 0.85f, 1f, 1f);

                _titleText.color = accent;
            }

            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            Camera cam = AppBootstrapper.Singleton?.MainCamera ?? DrawingViewerCamera.MainCamera;
            if (cam == null)
                return;

            Vector3 toCamera = cam.transform.position - transform.position;
            if (toCamera.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }
}
