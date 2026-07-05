#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// JNI bridge to the Android image picker activity.
    /// </summary>
    internal static class DrawingViewerAndroidFileBridge
    {
        private const string BridgeClass = "com.netzero.drawingviewer.DrawingViewerFileBridge";

        public static void StartImagePicker(string callbackGameObject, string callbackMethod)
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass(BridgeClass);
            bridge.CallStatic("startImagePicker", activity, callbackGameObject, callbackMethod);
        }
    }
}
#endif
