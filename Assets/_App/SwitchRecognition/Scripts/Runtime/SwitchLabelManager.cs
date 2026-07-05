using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// Creates and updates world-space labels for active switch detections.
    /// </summary>
    public class SwitchLabelManager : MonoBehaviour
    {
        [SerializeField] private Transform _labelRoot;
        [SerializeField] private SwitchClassDefinition _classDefinition;

        private readonly Dictionary<int, SwitchLabelOverlay> _activeLabels = new Dictionary<int, SwitchLabelOverlay>();

        public void SetClassDefinition(SwitchClassDefinition classDefinition)
        {
            _classDefinition = classDefinition;
        }

        public void UpdateDetections(IReadOnlyList<SwitchDetection> detections)
        {
            EnsureLabelRoot();

            var seenTrackIds = new HashSet<int>();

            if (detections != null)
            {
                foreach (var detection in detections)
                {
                    seenTrackIds.Add(detection.TrackId);

                    if (!_activeLabels.TryGetValue(detection.TrackId, out var label) || label == null)
                    {
                        label = SwitchLabelOverlay.Create(_labelRoot);
                        _activeLabels[detection.TrackId] = label;
                    }

                    var classEntry = _classDefinition != null
                        ? _classDefinition.GetClass(detection.ClassId)
                        : default;

                    label.SetDetection(detection, classEntry);
                }
            }

            var staleIds = new List<int>();
            foreach (var pair in _activeLabels)
            {
                if (!seenTrackIds.Contains(pair.Key))
                    staleIds.Add(pair.Key);
            }

            foreach (int trackId in staleIds)
            {
                if (_activeLabels.TryGetValue(trackId, out var label) && label != null)
                    Destroy(label.gameObject);

                _activeLabels.Remove(trackId);
            }
        }

        public void ClearAll()
        {
            foreach (var pair in _activeLabels)
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);
            }

            _activeLabels.Clear();
        }

        private void EnsureLabelRoot()
        {
            if (_labelRoot != null)
                return;

            var root = new GameObject("SwitchLabelRoot");
            root.transform.SetParent(transform, false);
            _labelRoot = root.transform;
        }
    }
}
