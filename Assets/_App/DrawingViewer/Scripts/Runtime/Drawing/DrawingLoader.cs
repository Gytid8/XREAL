using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Orchestrates loading of drawing textures from files.
    /// Manages async loading with concurrency control and caching.
    /// </summary>
    public class DrawingLoader : MonoBehaviour
    {
        [Header("Loading Settings")]
        [SerializeField] private int _maxTextureSize = 2048;
        [SerializeField] private int _maxConcurrentLoads = 2;

        /// <summary>
        /// Cached textures by path for quick access.
        /// </summary>
        private Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Currently active load operations.
        /// </summary>
        private readonly List<Task<Texture2D>> _activeLoads = new List<Task<Texture2D>>();

        /// <summary>
        /// Concurrency semaphore to limit simultaneous loads.
        /// </summary>
        private System.Threading.SemaphoreSlim _loadSemaphore;

        /// <summary>
        /// Event raised when a texture finishes loading.
        /// </summary>
        public event Action<string, Texture2D> OnTextureLoaded;

        /// <summary>
        /// Event raised when texture loading fails.
        /// </summary>
        public event Action<string, string> OnTextureLoadFailed; // (path, error)

        private void Awake()
        {
            _loadSemaphore = new System.Threading.SemaphoreSlim(_maxConcurrentLoads);
        }

        /// <summary>
        /// Loads a drawing texture asynchronously from a page path.
        /// </summary>
        public async Task<Texture2D> LoadDrawingAsync(string pagePath)
        {
            if (string.IsNullOrEmpty(pagePath))
            {
                Debug.LogError("[DrawingLoader] Cannot load: null or empty path.");
                return null;
            }

            // Check cache first
            if (_textureCache.TryGetValue(pagePath, out var cachedTexture))
            {
                Debug.Log($"[DrawingLoader] Cache hit for: {pagePath}");
                return cachedTexture;
            }

            await _loadSemaphore.WaitAsync();

            try
            {
                if (PdfPageRenderer.TryParsePagePath(pagePath, out var pdfSource, out var pdfRelativePath, out var pdfPageIndex))
                {
                    var pdfTexture = await PdfPageRenderer.RenderPageAsync(pdfRelativePath, pdfPageIndex, pdfSource, _maxTextureSize);
                    if (pdfTexture != null)
                    {
                        CacheTexture(pagePath, pdfTexture);
                        OnTextureLoaded?.Invoke(pagePath, pdfTexture);
                    }
                    else
                    {
                        OnTextureLoadFailed?.Invoke(pagePath, "PDF render returned null");
                    }

                    return pdfTexture;
                }

                var source = DetermineStorageSource(pagePath);

                var texture = await TextureAsyncLoader.LoadTextureAsync(pagePath, source, _maxTextureSize);

                if (texture != null)
                {
                    // Cache the loaded texture
                    CacheTexture(pagePath, texture);
                    OnTextureLoaded?.Invoke(pagePath, texture);
                }
                else
                {
                    OnTextureLoadFailed?.Invoke(pagePath, "Load returned null");
                }

                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DrawingLoader] Error loading {pagePath}: {ex.Message}");
                OnTextureLoadFailed?.Invoke(pagePath, ex.Message);
                return null;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        /// <summary>
        /// Preloads a set of page paths in the background.
        /// </summary>
        public async Task<List<Texture2D>> PreloadPagesAsync(List<string> pagePaths, int maxConcurrency = 2)
        {
            var tasks = new List<Task<Texture2D>>();

            foreach (var path in pagePaths)
            {
                if (_textureCache.ContainsKey(path))
                    continue;

                tasks.Add(LoadDrawingAsync(path));
            }

            var results = await Task.WhenAll(tasks);
            return new List<Texture2D>(results);
        }

        /// <summary>
        /// Caches a loaded texture.
        /// </summary>
        private void CacheTexture(string path, Texture2D texture)
        {
            _textureCache[path] = texture;
        }

        /// <summary>
        /// Removes a texture from the cache and destroys it.
        /// </summary>
        public void UnloadTexture(string path)
        {
            if (_textureCache.TryGetValue(path, out var texture))
            {
                _textureCache.Remove(path);

                if (texture != null)
                {
                    Debug.Log($"[DrawingLoader] Unloading: {path}");
                    Destroy(texture);
                }
            }
        }

        /// <summary>
        /// Unloads textures for paths that don't match the current document's nearby pages.
        /// Keeps pages within a window around the current page.
        /// </summary>
        public void UnloadDistantTextures(DrawingDocument document, int currentPage, int keepWindow = 3)
        {
            if (document == null) return;

            var pathsToRemove = new List<string>();

            foreach (var kvp in _textureCache)
            {
                // Check if this cached texture belongs to the current document
                // and if it's outside the keep window
                for (int i = 0; i < document.PageCount; i++)
                {
                    string pagePath = document.GetPagePath(i);
                    if (kvp.Key == pagePath)
                    {
                        if (Mathf.Abs(i - currentPage) > keepWindow)
                        {
                            pathsToRemove.Add(kvp.Key);
                        }
                        break;
                    }
                }
            }

            foreach (var path in pathsToRemove)
            {
                UnloadTexture(path);
            }
        }

        /// <summary>
        /// Clears all cached textures.
        /// </summary>
        public void ClearCache()
        {
            foreach (var kvp in _textureCache)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _textureCache.Clear();
            Debug.Log("[DrawingLoader] Cache cleared.");
        }

        /// <summary>
        /// Determines the storage source from a path string.
        /// </summary>
        private DrawingDocument.StorageSource DetermineStorageSource(string path)
        {
            if (path.Contains("android_asset") || path.Contains("StreamingAssets"))
                return DrawingDocument.StorageSource.StreamingAssets;

            if (path.Contains(Application.persistentDataPath))
                return DrawingDocument.StorageSource.PersistentData;

            if (System.IO.Path.IsPathRooted(path))
                return DrawingDocument.StorageSource.ExternalStorage;

            // Default to StreamingAssets for relative paths
            return DrawingDocument.StorageSource.StreamingAssets;
        }

        private void OnDestroy()
        {
            ClearCache();
            _loadSemaphore?.Dispose();
        }
    }
}
