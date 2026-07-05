using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Runtime configuration for the Drawing Viewer application.
    /// Create via: Assets > Create > Drawing Viewer > Settings
    /// </summary>
    [CreateAssetMenu(fileName = "DrawingViewerSettings", menuName = "Drawing Viewer/Settings")]
    public class DrawingViewerSettings : ScriptableObject
    {
        [Header("Tracking")]
        [Tooltip("Default tracking mode on app start.")]
        public TrackingType DefaultTrackingType = TrackingType.MODE_6DOF;

        [Tooltip("Default input source mode.")]
        public InputSource DefaultInputSource = InputSource.ControllerAndHands;

        [Header("Display")]
        [Tooltip("Distance (in meters) from camera to place new panels.")]
        [Range(0.5f, 5f)]
        public float PanelDefaultDistance = 2.0f;

        [Tooltip("Vertical offset from the camera when placing new panels (negative = below eye level).")]
        [Range(-1.5f, 0.5f)]
        public float PanelVerticalOffset = -0.2f;

        [Tooltip("Width (in meters) of the drawing panel.")]
        [Range(0.3f, 3f)]
        public float PanelDefaultWidth = 1.2f;

        [Tooltip("Default aspect ratio for panels without loaded content.")]
        [Range(0.5f, 2.5f)]
        public float DefaultAspectRatio = 1.414f; // A4-ish

        [Tooltip("Maximum number of simultaneously loaded pages.")]
        [Range(1, 20)]
        public int MaxLoadedPages = 10;

        [Tooltip("Maximum texture size for loaded drawings.")]
        [Range(512, 4096)]
        public int TextureMaxSize = 2048;

        [Header("Interaction")]
        [Tooltip("Sensitivity of pinch-to-zoom gesture.")]
        [Range(0.001f, 0.1f)]
        public float PinchZoomSpeed = 0.005f;

        [Tooltip("Sensitivity of touchpad scrolling.")]
        [Range(0.001f, 0.1f)]
        public float TouchpadScrollSpeed = 0.01f;

        [Tooltip("Speed of panel drag movement.")]
        [Range(0.1f, 5f)]
        public float DragSpeed = 1.0f;

        [Tooltip("Speed of two-hand rotation.")]
        [Range(0.1f, 5f)]
        public float TwoHandRotationSpeed = 1.0f;

        [Tooltip("Degrees rotated per toolbar button press or keyboard step.")]
        [Range(1f, 90f)]
        public float PanelRotationStepDegrees = 15f;

        [Tooltip("Touchpad rotation speed while holding GRIP (degrees per unit scroll).")]
        [Range(0.1f, 5f)]
        public float TouchpadRotationSpeed = 1.5f;

        [Tooltip("Maximum distance for laser/ray interaction.")]
        [Range(1f, 30f)]
        public float LaserRayDistance = 10f;

        [Tooltip("Smoothing factor for hand tracking jitter (OneEuroFilter beta).")]
        [Range(0f, 1f)]
        public float HandJitterSmoothing = 0.3f;

        [Tooltip("Deadzone threshold for touchpad input.")]
        [Range(0f, 0.5f)]
        public float TouchpadDeadzone = 0.15f;

        [Tooltip("Damping speed for panel smooth follow (higher = faster).")]
        [Range(1f, 20f)]
        public float PanelSmoothDamping = 8f;

        [Tooltip("Minimum allowed zoom scale.")]
        [Range(0.1f, 1f)]
        public float MinZoomScale = 0.2f;

        [Tooltip("Maximum allowed zoom scale.")]
        [Range(1f, 10f)]
        public float MaxZoomScale = 5f;

        [Header("Layout")]
        [Tooltip("Default page layout mode.")]
        public MultiPageLayout DefaultLayout = MultiPageLayout.Single;

        [Tooltip("Horizontal gap between pages in SideBySide / Spread layouts (meters).")]
        [Range(0.05f, 0.5f)]
        public float HorizontalPageGap = 0.15f;

        [Tooltip("Legacy spacing field kept for asset compatibility.")]
        [Range(0.1f, 2f)]
        public float GridSpacing = 0.3f;

        [Header("Network Upload")]
        [Tooltip("Enable PC upload receiver (UDP discovery + HTTP server).")]
        public bool EnableNetworkUpload = true;

        [Tooltip("HTTP port for drawing uploads from the Windows uploader.")]
        [Range(1024, 65535)]
        public int UploadHttpPort = 8080;

        [Tooltip("UDP port for LAN device discovery.")]
        [Range(1024, 65535)]
        public int DiscoveryUdpPort = 52345;

        [Tooltip("Device name shown in the Windows uploader device list.")]
        public string DeviceDisplayName = "XREAL Drawing Viewer";

        [Header("UI")]
        [Tooltip("Distance of UI canvas from camera (meters).")]
        [Range(0.3f, 2f)]
        public float UICanvasDistance = 0.8f;

        [Tooltip("Width of the world-space UI canvas (meters).")]
        [Range(0.5f, 3f)]
        public float UICanvasWidth = 1.6f;

        [Tooltip("Vertical offset of UI canvas relative to camera center (meters). Negative moves UI down.")]
        [Range(-0.3f, 0.3f)]
        public float UICanvasVerticalOffset = -0.02f;

        [Tooltip("Extra lift for the bottom page navigator inside the UI canvas (pixels).")]
        [Range(0f, 120f)]
        public float UIBottomBarLiftPixels = 40f;

        [Tooltip("Minimum button size for finger poke interaction (meters).")]
        [Range(0.02f, 0.1f)]
        public float MinButtonSize = 0.04f;

        /// <summary>
        /// Available page layout strategies.
        /// </summary>
        public enum MultiPageLayout
        {
            /// <summary>Show only the current page.</summary>
            Single,
            /// <summary>Current page with adjacent neighbors side by side.</summary>
            SideBySide,
            /// <summary>Up to five pages in a horizontal strip.</summary>
            Spread
        }

        /// <summary>
        /// Applies safe default values to all fields.
        /// </summary>
        public void ApplyDefaults()
        {
            DefaultTrackingType = TrackingType.MODE_6DOF;
            DefaultInputSource = InputSource.ControllerAndHands;
            PanelDefaultDistance = 2.0f;
            PanelVerticalOffset = -0.2f;
            PanelDefaultWidth = 1.2f;
            DefaultAspectRatio = 1.414f;
            MaxLoadedPages = 10;
            TextureMaxSize = 2048;
            PinchZoomSpeed = 0.005f;
            TouchpadScrollSpeed = 0.01f;
            DragSpeed = 1.0f;
            TwoHandRotationSpeed = 1.0f;
            PanelRotationStepDegrees = 15f;
            TouchpadRotationSpeed = 1.5f;
            LaserRayDistance = 10f;
            HandJitterSmoothing = 0.3f;
            TouchpadDeadzone = 0.15f;
            PanelSmoothDamping = 8f;
            MinZoomScale = 0.2f;
            MaxZoomScale = 5f;
            DefaultLayout = MultiPageLayout.Single;
            HorizontalPageGap = 0.15f;
            UICanvasDistance = 0.8f;
            UICanvasWidth = 1.6f;
            UICanvasVerticalOffset = -0.02f;
            UIBottomBarLiftPixels = 40f;
            MinButtonSize = 0.04f;
            EnableNetworkUpload = true;
            UploadHttpPort = 8080;
            DiscoveryUdpPort = 52345;
            DeviceDisplayName = "XREAL Drawing Viewer";
        }
    }
}
