using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Resolves PDF files to local filesystem paths usable by native renderers.
    /// </summary>
    public static class PdfPathResolver
    {
        public static async Task<string> EnsureLocalPdfPathAsync(string relativePath, DrawingDocument.StorageSource source)
        {
            string absolutePath = ResolveAbsolutePdfPath(relativePath, source);
            if (string.IsNullOrEmpty(absolutePath))
                return null;

            if (File.Exists(absolutePath))
                return absolutePath;

#if UNITY_ANDROID && !UNITY_EDITOR
            return await CopyStreamingAssetPdfToCacheAsync(relativePath);
#else
            return absolutePath;
#endif
        }

        public static string ResolveAbsolutePdfPath(string relativePath, DrawingDocument.StorageSource source)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            relativePath = relativePath.Replace('\\', '/');

            switch (source)
            {
                case DrawingDocument.StorageSource.PersistentData:
                    if (Path.IsPathRooted(relativePath))
                        return relativePath;
                    return Path.Combine(Application.persistentDataPath, relativePath);

                case DrawingDocument.StorageSource.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    return GetCachedPdfPath(relativePath);
#else
                    string standardPath = Path.Combine(Application.streamingAssetsPath, relativePath);
                    if (File.Exists(standardPath))
                        return standardPath;

                    string appLocalPath = Path.Combine(
                        Application.dataPath,
                        "_App/DrawingViewer/StreamingAssets",
                        relativePath);
                    return File.Exists(appLocalPath) ? appLocalPath : standardPath;
#endif

                case DrawingDocument.StorageSource.ExternalStorage:
                    return relativePath;

                default:
                    return null;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string GetCachedPdfPath(string relativePath)
        {
            string safeName = relativePath.Replace('/', '_');
            return Path.Combine(Application.persistentDataPath, "PdfSourceCache", safeName, Path.GetFileName(relativePath));
        }

        private static async Task<string> CopyStreamingAssetPdfToCacheAsync(string relativePath)
        {
            string cachePath = GetCachedPdfPath(relativePath);
            if (File.Exists(cachePath))
                return cachePath;

            string uri = $"file:///android_asset/{relativePath}";
            using var request = UnityWebRequest.Get(uri);
            request.timeout = 120;
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[PdfPathResolver] Failed to read PDF from StreamingAssets: {relativePath} ({request.error})");
                return null;
            }

            string directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(cachePath, request.downloadHandler.data);
            Debug.Log($"[PdfPathResolver] Cached PDF to: {cachePath}");
            return cachePath;
        }
#endif
    }
}
