using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.SwitchRecognition
{
    /// <summary>
    /// Abstraction for switch detection backends (Mock, YOLO/Sentis, etc.).
    /// </summary>
    public interface ISwitchDetector
    {
        bool IsReady { get; }

        string StatusMessage { get; }

        IReadOnlyList<SwitchDetection> Detect(Texture2D frame, Camera viewCamera);
    }
}
