using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Provides async texture loading from various sources with fallback support.
    /// Handles Android StreamingAssets (inside APK) and persistent data paths.
    /// </summary>
    public static class TextureAsyncLoader
    {
        /// <summary>
        /// Loads a texture asynchronously from a relative path.
        /// Automatically handles Android StreamingAssets URI scheme.
        /// </summary>
        /// <param name="relativePath">Relative path to the texture file.</param>
        /// <param name="source">Storage source type.</param>
        /// <param name="maxSize">Maximum texture size (width/height).</param>
        /// <returns>The loaded Texture2D, or null on failure.</returns>
        public static async Task<Texture2D> LoadTextureAsync(
            string relativePath,
            DrawingDocument.StorageSource source,
            int maxSize = 2048)
        {
            string uri = GetLoadUri(relativePath, source);

            if (string.IsNullOrEmpty(uri))
            {
                Debug.LogError($"[TextureAsyncLoader] Invalid path: {relativePath}");
                return null;
            }

            using (var request = UnityWebRequestTexture.GetTexture(uri))
            {
                request.timeout = 30;
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);

                    // Limit maximum size for memory efficiency
                    if (texture.width > maxSize || texture.height > maxSize)
                    {
                        texture = ResizeTexture(texture, maxSize);
                    }

                    texture.name = System.IO.Path.GetFileNameWithoutExtension(relativePath);
                    Debug.Log($"[TextureAsyncLoader] Loaded: {uri} ({texture.width}x{texture.height})");
                    return texture;
                }
                else
                {
                    Debug.LogError($"[TextureAsyncLoader] Failed to load {uri}: {request.error}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Loads multiple textures in parallel with a concurrency limit.
        /// </summary>
        public static async Task<List<Texture2D>> LoadTexturesAsync(
            List<string> relativePaths,
            DrawingDocument.StorageSource source,
            int maxConcurrency = 3,
            int maxSize = 2048)
        {
            var results = new List<Texture2D>();
            var semaphore = new System.Threading.SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            foreach (var path in relativePaths)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var tex = await LoadTextureAsync(path, source, maxSize);
                        lock (results)
                        {
                            results.Add(tex);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// Builds the correct URI for loading based on storage source.
        /// </summary>
        private static string GetLoadUri(string relativePath, DrawingDocument.StorageSource source)
        {
            switch (source)
            {
                case DrawingDocument.StorageSource.StreamingAssets:
                    // On Android, StreamingAssets are inside the APK and require jar:file:// or file:///android_asset/
                    if (Application.platform == RuntimePlatform.Android)
                    {
                        return $"file:///android_asset/{relativePath}";
                    }
                    else
                    {
                        // Editor/Standalone: prefer Unity's standard StreamingAssets path,
                        // then fall back to the app-local sample/content folder used by this project.
                        string standardPath = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);
                        if (System.IO.File.Exists(standardPath))
                            return ToFileUri(standardPath);

                        string appLocalPath = System.IO.Path.Combine(
                            Application.dataPath,
                            "_App/DrawingViewer/StreamingAssets",
                            relativePath);
                        return ToFileUri(appLocalPath);
                    }

                case DrawingDocument.StorageSource.PersistentData:
                    if (System.IO.Path.IsPathRooted(relativePath))
                        return ToFileUri(relativePath);

                    return ToFileUri(System.IO.Path.Combine(Application.persistentDataPath, relativePath));

                case DrawingDocument.StorageSource.ExternalStorage:
                    return ToFileUri(relativePath);

                default:
                    Debug.LogError($"[TextureAsyncLoader] Unknown storage source: {source}");
                    return null;
            }
        }

        private static string ToFileUri(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            return new Uri(path).AbsoluteUri;
        }

        /// <summary>
        /// Resizes a texture to fit within maxSize while maintaining aspect ratio.
        /// Uses RenderTexture for GPU-accelerated scaling.
        /// </summary>
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

        /// <summary>
        /// Creates a simple placeholder texture (checkerboard pattern) for loading states.
        /// </summary>
        public static Texture2D CreatePlaceholderTexture(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isWhite = ((x / 32) + (y / 32)) % 2 == 0;
                    pixels[y * size + x] = isWhite
                        ? new Color32(200, 200, 200, 255)
                        : new Color32(160, 160, 160, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = "Placeholder";
            return tex;
        }
    }
}
