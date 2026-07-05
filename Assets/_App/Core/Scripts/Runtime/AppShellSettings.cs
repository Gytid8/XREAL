using UnityEngine;

namespace Unity.XR.XREAL.App.Core
{
    [CreateAssetMenu(fileName = "AppShellSettings", menuName = "App Core/App Shell Settings")]
    public class AppShellSettings : ScriptableObject
    {
        [Header("Launcher UI")]
        [SerializeField] private float _launcherCanvasWidthMeters = 1.6f;
        [SerializeField] private float _launcherCanvasDistance = 0.85f;
        [SerializeField] private float _launcherVerticalOffset = 0.05f;

        public float LauncherCanvasWidthMeters => _launcherCanvasWidthMeters;
        public float LauncherCanvasDistance => _launcherCanvasDistance;
        public float LauncherVerticalOffset => _launcherVerticalOffset;
    }
}
