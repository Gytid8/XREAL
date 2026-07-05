using UnityEngine;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// A single detected electrical switch instance.
    /// </summary>
    public struct SwitchDetection
    {
        public int ClassId;
        public string DisplayName;
        public float Confidence;
        public Rect BoundingBox;
        public Vector3 WorldPosition;
        public int TrackId;
    }
}
