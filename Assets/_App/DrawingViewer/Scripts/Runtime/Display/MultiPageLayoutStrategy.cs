using System.Collections.Generic;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Arranges drawing panels in XR-friendly layout modes: Single, SideBySide, Spread.
    /// </summary>
    public class MultiPageLayoutStrategy : MonoBehaviour
    {
        [Header("Layout Settings")]
        [SerializeField] private float _gridSpacing = 0.3f;
        [SerializeField] private float _horizontalGap = 0.15f;

        public LayoutMode CurrentMode { get; set; } = LayoutMode.Single;

        public enum LayoutMode
        {
            Single = 0,
            SideBySide = 1,
            Spread = 2
        }

        private void Start()
        {
            var settings = DrawingViewerApp.Singleton?.Settings;
            if (settings != null)
            {
                _gridSpacing = settings.GridSpacing;
                _horizontalGap = settings.HorizontalPageGap;
                CurrentMode = LayoutMode.Single;
            }
        }

        public void ApplyLayout(List<DrawingPanelController> panels, int activePageIndex, Camera referenceCamera)
        {
            if (panels == null || panels.Count == 0)
                return;

            int totalPages = DrawingViewerApp.Singleton?.CurrentDocument?.PageCount ?? panels.Count;

            switch (CurrentMode)
            {
                case LayoutMode.Single:
                    ApplySingleLayout(panels, activePageIndex);
                    break;
                case LayoutMode.SideBySide:
                    if (totalPages <= 1)
                        ApplySingleLayout(panels, activePageIndex);
                    else
                        ApplyTwoPageLayout(panels, activePageIndex, totalPages);
                    break;
                case LayoutMode.Spread:
                    if (totalPages <= 1)
                        ApplySingleLayout(panels, activePageIndex);
                    else
                    {
                        var anchorPanel = FindPanel(panels, activePageIndex) ?? panels[0];
                        ApplyHorizontalLayout(panels, anchorPanel, activePageIndex, neighborRadius: 2);
                    }
                    break;
            }
        }

        public static int GetSideBySideCompanionPage(int activePageIndex, int totalPages)
        {
            if (totalPages <= 1)
                return activePageIndex;

            return activePageIndex < totalPages - 1
                ? activePageIndex + 1
                : activePageIndex - 1;
        }

        public static IEnumerable<int> GetRequiredNeighborIndices(LayoutMode mode, int activePageIndex, int totalPages)
        {
            if (totalPages <= 1)
                yield break;

            switch (mode)
            {
                case LayoutMode.SideBySide:
                    yield return GetSideBySideCompanionPage(activePageIndex, totalPages);
                    break;
                case LayoutMode.Spread:
                    for (int i = activePageIndex - 2; i <= activePageIndex + 2; i++)
                    {
                        if (i >= 0 && i < totalPages && i != activePageIndex)
                            yield return i;
                    }
                    break;
            }
        }

        private void ApplySingleLayout(List<DrawingPanelController> panels, int activePageIndex)
        {
            foreach (var panel in panels)
            {
                bool isActive = panel.PageDisplay.PageIndex == activePageIndex;
                panel.PageDisplay.gameObject.SetActive(isActive);

                if (isActive)
                    panel.SnapToTargets();
            }
        }

        private void ApplyTwoPageLayout(List<DrawingPanelController> panels, int activePageIndex, int totalPages)
        {
            int companionPage = GetSideBySideCompanionPage(activePageIndex, totalPages);
            var anchorPanel = FindPanel(panels, activePageIndex) ?? panels[0];

            Vector3 anchorPos = anchorPanel.transform.position;
            Quaternion anchorRot = anchorPanel.transform.rotation;
            Vector3 right = anchorPanel.transform.right;
            float activeScale = anchorPanel.CurrentScale;

            foreach (var panel in panels)
            {
                int pageIndex = panel.PageDisplay.PageIndex;
                bool visible = pageIndex == activePageIndex || pageIndex == companionPage;

                panel.PageDisplay.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                float horizontalOffset = 0f;
                if (pageIndex == companionPage)
                {
                    horizontalOffset = companionPage > activePageIndex
                        ? ComputeHorizontalOffset(panels, activePageIndex, companionPage, _horizontalGap)
                        : ComputeHorizontalOffset(panels, activePageIndex, companionPage, _horizontalGap);
                }

                panel.SetTargetPosition(anchorPos + right * horizontalOffset);
                panel.SetTargetRotation(anchorRot);
                panel.SetZoom(activeScale);
                panel.SnapToTargets();
            }
        }

        private void ApplyHorizontalLayout(
            List<DrawingPanelController> panels,
            DrawingPanelController anchorPanel,
            int activePageIndex,
            int neighborRadius)
        {
            Vector3 anchorPos = anchorPanel.transform.position;
            Quaternion anchorRot = anchorPanel.transform.rotation;
            Vector3 right = anchorPanel.transform.right;
            float activeScale = anchorPanel.CurrentScale;

            foreach (var panel in panels)
            {
                int pageIndex = panel.PageDisplay.PageIndex;
                int offset = pageIndex - activePageIndex;
                bool visible = Mathf.Abs(offset) <= neighborRadius;

                panel.PageDisplay.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                float horizontalOffset = ComputeHorizontalOffset(panels, activePageIndex, pageIndex, _horizontalGap);
                panel.SetTargetPosition(anchorPos + right * horizontalOffset);
                panel.SetTargetRotation(anchorRot);
                panel.SetZoom(activeScale);
                panel.SnapToTargets();
            }
        }

        public static string GetDisplayName(LayoutMode mode)
        {
            switch (mode)
            {
                case LayoutMode.Single:
                    return "\u5355\u9875";
                case LayoutMode.SideBySide:
                    return "\u53cc\u9875\u5bf9\u6bd4";
                case LayoutMode.Spread:
                    return "\u591a\u9875\u5c55\u5f00";
                default:
                    return mode.ToString();
            }
        }

        private static DrawingPanelController FindPanel(List<DrawingPanelController> panels, int pageIndex)
        {
            return panels.Find(p => p.PageDisplay.PageIndex == pageIndex);
        }

        private static float GetReferencePanelWidth(List<DrawingPanelController> panels, int pageIndex)
        {
            var panel = FindPanel(panels, pageIndex);
            if (panel != null && panel.PageDisplay.PanelWidth > 0f)
                return panel.PageDisplay.PanelWidth;

            var settings = DrawingViewerApp.Singleton?.Settings;
            return settings != null ? settings.PanelDefaultWidth : 1.2f;
        }

        private static float ComputeHorizontalOffset(
            List<DrawingPanelController> panels,
            int activePageIndex,
            int targetPageIndex,
            float gap)
        {
            if (targetPageIndex == activePageIndex)
                return 0f;

            int direction = targetPageIndex > activePageIndex ? 1 : -1;
            float offset = 0f;
            int current = activePageIndex;

            while (current != targetPageIndex)
            {
                int next = current + direction;
                float currentWidth = GetReferencePanelWidth(panels, current);
                float nextWidth = GetReferencePanelWidth(panels, next);
                float step = currentWidth * 0.5f + gap + nextWidth * 0.5f;
                offset += direction * step;
                current = next;
            }

            return offset;
        }

        public void SetMode(LayoutMode mode, List<DrawingPanelController> panels, int activePageIndex, Camera referenceCamera)
        {
            if (CurrentMode == mode) return;

            CurrentMode = mode;
            Debug.Log($"[MultiPageLayoutStrategy] Layout mode: {GetDisplayName(mode)}");
            ApplyLayout(panels, activePageIndex, referenceCamera);
        }
    }
}
