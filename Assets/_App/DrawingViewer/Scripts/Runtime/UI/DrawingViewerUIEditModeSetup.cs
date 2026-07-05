using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Keeps world-space UI visible and correctly scaled in the Scene view when not playing.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DrawingViewerUIEditModeSetup : MonoBehaviour
    {
        private void OnEnable()
        {
            ApplyEditModeLayout();
        }

        private void OnValidate()
        {
            ApplyEditModeLayout();
        }

        private void ApplyEditModeLayout()
        {
            if (Application.isPlaying)
                return;

            var settings = Resources.Load<DrawingViewerSettings>("DrawingViewerSettings");
            float canvasWidth = settings != null ? settings.UICanvasWidth : DrawingViewerUILayout.DefaultCanvasWidthMeters;
            float bottomLift = settings != null ? settings.UIBottomBarLiftPixels : DrawingViewerUILayout.DefaultBottomBarLiftPixels;

            DrawingViewerUILayout.ConfigureCanvasRoot(transform, canvasWidth);
            DrawingViewerUILayout.ApplyAll(transform, bottomLift);
            DrawingViewerUILayout.ApplyEditorSceneTransform(transform);
            DrawingViewerFontProvider.ApplyAllInChildren(transform);
        }
    }
}
