using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Monitors app performance: FPS, memory usage, frame timings.
    /// Useful for optimizing mobile XR performance.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool _showFPSInLog = false;
        [SerializeField] private bool _showMemoryInLog = false;
        [SerializeField] private float _logInterval = 5f;

        [Header("Thresholds")]
        [SerializeField] private int _warningFPSTreshold = 30;
        [SerializeField] private long _warningMemoryMB = 512;

        /// <summary>
        /// Current frames per second (smoothed).
        /// </summary>
        public float CurrentFPS { get; private set; }

        /// <summary>
        /// Min FPS recorded in the current interval.
        /// </summary>
        public float MinFPS { get; private set; }

        /// <summary>
        /// Total memory used by the application in MB.
        /// </summary>
        public long UsedMemoryMB { get; private set; }

        /// <summary>
        /// Event when FPS drops below warning threshold.
        /// </summary>
        public System.Action<float> OnFPSWarning;

        /// <summary>
        /// Event when memory exceeds warning threshold.
        /// </summary>
        public System.Action<long> OnMemoryWarning;

        private float _fpsAccumulator;
        private int _frameCount;
        private float _lastLogTime;
        private float _lastFPSMeasureTime;

        private void Start()
        {
            MinFPS = float.MaxValue;
            _lastLogTime = Time.time;
            _lastFPSMeasureTime = Time.time;
        }

        private void Update()
        {
            _frameCount++;
            _fpsAccumulator += Time.unscaledDeltaTime;

            // Measure FPS every 0.5 seconds
            if (Time.time - _lastFPSMeasureTime >= 0.5f)
            {
                CurrentFPS = _frameCount / _fpsAccumulator;

                if (CurrentFPS < MinFPS && _frameCount > 10)
                    MinFPS = CurrentFPS;

                _frameCount = 0;
                _fpsAccumulator = 0;
                _lastFPSMeasureTime = Time.time;

                // Check FPS warning
                if (CurrentFPS < _warningFPSTreshold)
                {
                    OnFPSWarning?.Invoke(CurrentFPS);
                }
            }

            // Log periodically
            if (Time.time - _lastLogTime >= _logInterval)
            {
                UpdateMemoryUsage();

                if (_showFPSInLog)
                {
                    Debug.Log($"[Performance] FPS: {CurrentFPS:F1} (Min: {MinFPS:F1})");
                }

                if (_showMemoryInLog)
                {
                    Debug.Log($"[Performance] Memory: {UsedMemoryMB} MB");
                }

                _lastLogTime = Time.time;
                MinFPS = float.MaxValue; // Reset min FPS for next interval
            }
        }

        /// <summary>
        /// Updates memory usage statistics.
        /// </summary>
        private void UpdateMemoryUsage()
        {
            UsedMemoryMB = System.GC.GetTotalMemory(false) / (1024 * 1024);

            // Also check Unity's memory
            long unityUsed = (long)(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024));

            if (unityUsed > UsedMemoryMB)
                UsedMemoryMB = unityUsed;

            if (UsedMemoryMB > _warningMemoryMB)
            {
                OnMemoryWarning?.Invoke(UsedMemoryMB);
            }
        }

        /// <summary>
        /// Gets a formatted performance summary string.
        /// </summary>
        public string GetPerformanceSummary()
        {
            return $"FPS: {CurrentFPS:F1} | Memory: {UsedMemoryMB} MB";
        }

        /// <summary>
        /// Logs the current performance state.
        /// </summary>
        public void LogPerformanceState()
        {
            Debug.Log($"[Performance] Summary - {GetPerformanceSummary()}");
        }
    }
}
