using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// Placeholder detector that returns demo detections in front of the camera.
    /// </summary>
    public class MockSwitchDetector : ISwitchDetector
    {
        private readonly SwitchClassDefinition _classDefinition;
        private readonly float _distanceMeters;
        private readonly float _horizontalSpacingMeters;
        private readonly List<SwitchDetection> _results = new List<SwitchDetection>(3);

        public MockSwitchDetector(SwitchClassDefinition classDefinition, float distanceMeters = 1.2f, float horizontalSpacingMeters = 0.35f)
        {
            _classDefinition = classDefinition;
            _distanceMeters = distanceMeters;
            _horizontalSpacingMeters = horizontalSpacingMeters;
        }

        public bool IsReady => true;

        public string StatusMessage => "演示模式：模型未就绪，显示模拟识别标签";

        public IReadOnlyList<SwitchDetection> Detect(Texture2D frame, Camera viewCamera)
        {
            _results.Clear();

            if (viewCamera == null)
                return _results;

            SwitchClassDefinition.SwitchClassEntry[] classes = _classDefinition != null
                ? _classDefinition.Classes
                : null;

            if (classes == null || classes.Length == 0)
            {
                classes = new[]
                {
                    new SwitchClassDefinition.SwitchClassEntry { classId = 0, displayName = "断路器", labelColor = new Color(0.2f, 0.75f, 1f) },
                    new SwitchClassDefinition.SwitchClassEntry { classId = 1, displayName = "隔离开关", labelColor = new Color(0.45f, 0.95f, 0.55f) },
                    new SwitchClassDefinition.SwitchClassEntry { classId = 2, displayName = "负荷开关", labelColor = new Color(1f, 0.78f, 0.35f) }
                };
            }

            int count = Mathf.Min(3, classes.Length);
            float startOffset = -((count - 1) * 0.5f) * _horizontalSpacingMeters;

            Transform cam = viewCamera.transform;
            for (int i = 0; i < count; i++)
            {
                var classEntry = classes[i];
                Vector3 worldPosition = cam.position
                    + cam.forward * _distanceMeters
                    + cam.right * (startOffset + i * _horizontalSpacingMeters)
                    + cam.up * 0.05f;

                _results.Add(new SwitchDetection
                {
                    ClassId = classEntry.classId,
                    DisplayName = classEntry.displayName,
                    Confidence = 0.92f,
                    BoundingBox = new Rect(0.3f + i * 0.2f, 0.45f, 0.12f, 0.18f),
                    WorldPosition = worldPosition,
                    TrackId = i + 1
                });
            }

            return _results;
        }
    }
}
