using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Shared UI layout helpers used at runtime and in the Editor scene.
    /// </summary>
    public static class DrawingViewerUILayout
    {
        public const float DesignWidthPixels = 1600f;
        public const float DesignHeightPixels = 900f;
        public const float DefaultCanvasWidthMeters = 1.6f;
        public const float DefaultBottomBarLiftPixels = 40f;
        public const float ToolbarTopInsetPixels = 12f;

        public static float GetCanvasScale(float canvasWidthMeters = DefaultCanvasWidthMeters)
        {
            return canvasWidthMeters / DesignWidthPixels;
        }

        public static void ConfigureCanvasRoot(Transform canvasRoot, float canvasWidthMeters = DefaultCanvasWidthMeters)
        {
            if (canvasRoot == null) return;

            var rect = canvasRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(DesignWidthPixels, DesignHeightPixels);
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
            }

            float scale = GetCanvasScale(canvasWidthMeters);
            canvasRoot.localScale = Vector3.one * scale;
        }

public static void ApplyAll(Transform uiCanvasRoot, float bottomBarLiftPixels = DefaultBottomBarLiftPixels)
        {
            if (uiCanvasRoot == null) return;

            ApplyToolbar(uiCanvasRoot.Find("Toolbar"));
            ApplyPageNavigator(uiCanvasRoot.Find("PageNavigator"), bottomBarLiftPixels);
            ApplyFileBrowser(uiCanvasRoot.Find("FileBrowser"));
            ApplyMessageText(uiCanvasRoot.Find("MessageText"));
            Unity.XR.XREAL.App.Core.AppUiTheme.ApplyDrawingViewerCanvas(uiCanvasRoot);
        }

        public static void ApplyEditorSceneTransform(Transform canvasRoot)
        {
            if (canvasRoot == null) return;

            canvasRoot.position = new Vector3(0f, 1.6f, 0.8f);
            canvasRoot.rotation = Quaternion.identity;
        }

        public static void ApplyToolbar(Transform toolbar)
        {
            if (toolbar == null) return;

            var rect = toolbar.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -ToolbarTopInsetPixels);
            rect.sizeDelta = new Vector2(0, 88);

            var legacyTitle = toolbar.Find("DrawingNameText");
            if (legacyTitle != null)
                legacyTitle.gameObject.SetActive(false);
        }

        public static void ApplyPageNavigator(Transform pageNavigator, float liftPixels)
        {
            if (pageNavigator == null) return;

            var rect = pageNavigator.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, liftPixels);
            rect.sizeDelta = new Vector2(0, 72);
        }

        public static void ApplyFileBrowser(Transform fileBrowser)
        {
            if (fileBrowser == null) return;

            var rect = fileBrowser.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -96 - ToolbarTopInsetPixels);
            rect.sizeDelta = new Vector2(480, 360);

            var header = fileBrowser.Find("Header");
            if (header != null)
            {
                var headerRect = header.GetComponent<RectTransform>();
                if (headerRect != null)
                    headerRect.sizeDelta = new Vector2(0, 52);
            }

            var scrollView = fileBrowser.Find("ScrollView");
            if (scrollView != null)
            {
                var scrollRect = scrollView.GetComponent<RectTransform>();
                if (scrollRect != null)
                {
                    scrollRect.anchorMin = Vector2.zero;
                    scrollRect.anchorMax = Vector2.one;
                    scrollRect.offsetMin = new Vector2(8, 8);
                    scrollRect.offsetMax = new Vector2(-8, -60);
                }
            }
        }

        public static void ApplyMessageText(Transform messageText)
        {
            if (messageText == null) return;

            var rect = messageText.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 120);
            rect.sizeDelta = new Vector2(700, 64);
        }
    }
}
