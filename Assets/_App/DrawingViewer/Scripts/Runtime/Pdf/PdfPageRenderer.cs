using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Renders PDF pages to textures for the drawing viewer.
    /// Android uses the system PdfRenderer; Editor uses Docnet/PDFium.
    /// </summary>
    public static class PdfPageRenderer
    {
        private static readonly object RenderLock = new object();

        public static bool TryParsePagePath(string pagePath, out DrawingDocument.StorageSource source, out string relativePdfPath, out int pageIndex)
        {
            source = DrawingDocument.StorageSource.StreamingAssets;
            relativePdfPath = null;
            pageIndex = -1;

            if (string.IsNullOrEmpty(pagePath) || !pagePath.StartsWith(DrawingDocument.PdfPagePathPrefix, StringComparison.Ordinal))
                return false;

            string payload = pagePath.Substring(DrawingDocument.PdfPagePathPrefix.Length);
            int hashIndex = payload.LastIndexOf('#');
            if (hashIndex <= 0 || hashIndex >= payload.Length - 1)
                return false;

            string pathPart = payload.Substring(0, hashIndex);
            string pagePart = payload.Substring(hashIndex + 1);
            if (!int.TryParse(pagePart, out pageIndex))
                return false;

            int slashIndex = pathPart.IndexOf('/');
            if (slashIndex <= 0)
                return false;

            if (!int.TryParse(pathPart.Substring(0, slashIndex), out int sourceValue))
                return false;

            source = (DrawingDocument.StorageSource)sourceValue;
            relativePdfPath = pathPart.Substring(slashIndex + 1);
            return !string.IsNullOrEmpty(relativePdfPath);
        }

        public static async Task<int> GetPageCountAsync(string relativePdfPath, DrawingDocument.StorageSource source)
        {
            string localPath = await PdfPathResolver.EnsureLocalPdfPathAsync(relativePdfPath, source);
            if (string.IsNullOrEmpty(localPath))
                return 0;

            return GetPageCountFromLocalPath(localPath);
        }

        public static int GetPageCountFromLocalPath(string localPath)
        {
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
                return 0;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var bridge = new AndroidJavaClass("com.netzero.drawingviewer.DrawingViewerPdfBridge");
                return bridge.CallStatic<int>("getPageCount", localPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PdfPageRenderer] Android page count failed: {ex.Message}");
                return 0;
            }
#else
            return GetPageCountDocnet(localPath);
#endif
        }

        public static async Task<Texture2D> RenderPageAsync(
            string relativePdfPath,
            int pageIndex,
            DrawingDocument.StorageSource source,
            int maxSize = 2048)
        {
            string localPath = await PdfPathResolver.EnsureLocalPdfPathAsync(relativePdfPath, source);
            if (string.IsNullOrEmpty(localPath))
                return null;

#if UNITY_ANDROID && !UNITY_EDITOR
            return await RenderPageAndroidAsync(localPath, pageIndex, maxSize);
#else
            // Docnet/PDFium must run on the main thread in Unity.
            await Task.Yield();
            return RenderPageDocnet(localPath, pageIndex, maxSize);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task<Texture2D> RenderPageAndroidAsync(string localPath, int pageIndex, int maxSize)
        {
            string cacheDir = Path.Combine(Application.temporaryCachePath, "PdfRenderCache", Path.GetFileNameWithoutExtension(localPath));
            string pngPath = null;

            await Task.Run(() =>
            {
                using var bridge = new AndroidJavaClass("com.netzero.drawingviewer.DrawingViewerPdfBridge");
                pngPath = bridge.CallStatic<string>("renderPageToPng", localPath, pageIndex, maxSize, cacheDir);
            });

            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
            {
                Debug.LogError($"[PdfPageRenderer] Android PDF render failed for page {pageIndex}.");
                return null;
            }

            return await LoadPngTextureAsync(pngPath, maxSize);
        }
#endif

        private static async Task<Texture2D> LoadPngTextureAsync(string pngPath, int maxSize)
        {
            using var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(new Uri(pngPath).AbsoluteUri);
            request.timeout = 30;
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[PdfPageRenderer] Failed to load rendered PNG: {request.error}");
                return null;
            }

            var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
            if (texture.width > maxSize || texture.height > maxSize)
                texture = ResizeTexture(texture, maxSize);

            return texture;
        }

        private static Texture2D ResizeTexture(Texture2D source, int maxSize)
        {
            int newWidth = source.width;
            int newHeight = source.height;

            if (source.width > source.height)
            {
                newWidth = maxSize;
                newHeight = Mathf.RoundToInt((float)source.height / source.width * maxSize);
            }
            else
            {
                newHeight = maxSize;
                newWidth = Mathf.RoundToInt((float)source.width / source.height * maxSize);
            }

            var rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.Default);
            rt.filterMode = FilterMode.Bilinear;

            var previousRT = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            var result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(source);
            return result;
        }

#if !UNITY_ANDROID || UNITY_EDITOR
        private static int GetPageCountDocnet(string localPath)
        {
            lock (RenderLock)
            {
                try
                {
                    using var reader = Docnet.Core.DocLib.Instance.GetDocReader(
                        localPath,
                        new Docnet.Core.Models.PageDimensions(1, 1));
                    return reader.GetPageCount();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PdfPageRenderer] Docnet page count failed: {ex.Message}");
                    return 0;
                }
            }
        }

        private static Texture2D RenderPageDocnet(string localPath, int pageIndex, int maxSize)
        {
            lock (RenderLock)
            {
                try
                {
                    using var docReader = Docnet.Core.DocLib.Instance.GetDocReader(
                        localPath,
                        new Docnet.Core.Models.PageDimensions(maxSize, maxSize));
                    using var pageReader = docReader.GetPageReader(pageIndex);
                    var bytes = pageReader.GetImage(new Docnet.Core.Converters.NaiveTransparencyRemover(255, 255, 255));
                    int width = pageReader.GetPageWidth();
                    int height = pageReader.GetPageHeight();

                    if (bytes == null || bytes.Length == 0 || width <= 0 || height <= 0)
                    {
                        Debug.LogError($"[PdfPageRenderer] Docnet returned empty page data ({width}x{height}).");
                        return null;
                    }

                    var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    var rgba = ConvertBgraToRgba(bytes, width, height);
                    texture.LoadRawTextureData(rgba);
                    texture.Apply();
                    texture.name = $"{Path.GetFileNameWithoutExtension(localPath)}_p{pageIndex + 1}";
                    Debug.Log($"[PdfPageRenderer] Rendered PDF page {pageIndex + 1}: {width}x{height}");
                    return texture;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PdfPageRenderer] Docnet render failed: {ex.Message}");
                    return null;
                }
            }
        }

        private static byte[] ConvertBgraToRgba(byte[] bgra, int width, int height)
        {
            var rgba = new byte[bgra.Length];
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int src = i * 4;
                rgba[src] = bgra[src + 2];
                rgba[src + 1] = bgra[src + 1];
                rgba[src + 2] = bgra[src];
                rgba[src + 3] = bgra[src + 3];
            }

            return rgba;
        }
#endif
    }
}
