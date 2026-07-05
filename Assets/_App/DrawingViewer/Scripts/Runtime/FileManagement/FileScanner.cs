using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Scans StreamingAssets and persistent data for drawing files (PNG, JPG, PDF).
    /// On Android, uses a manifest-based approach since StreamingAssets are inside the APK.
    /// </summary>
    public class FileScanner : MonoBehaviour
    {
        private const string DrawingsFolder = "Drawings";

        public async Task<List<DrawingDocument>> ScanStreamingAssetsAsync()
        {
            var documents = new List<DrawingDocument>();

            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                documents = await ScanStreamingAssetsManifestAsync();
#else
                string standardPath = Path.Combine(Application.streamingAssetsPath, DrawingsFolder);
                string projectPath = Path.Combine(Application.dataPath, "_App/DrawingViewer/StreamingAssets/Drawings");

                string scanPath = null;
                if (Directory.Exists(standardPath))
                    scanPath = standardPath;
                else if (Directory.Exists(projectPath))
                    scanPath = projectPath;

                if (string.IsNullOrEmpty(scanPath))
                {
                    Debug.LogWarning($"[FileScanner] Drawings folder not found at:\n  {standardPath}\n  {projectPath}");
                    return documents;
                }

                Debug.Log($"[FileScanner] Scanning drawings at: {scanPath}");
                documents.AddRange(ScanDirectory(scanPath, DrawingDocument.StorageSource.StreamingAssets, null));
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileScanner] Error scanning StreamingAssets: {ex.Message}");
            }

            await Task.CompletedTask;
            return documents;
        }

        private async Task<List<DrawingDocument>> ScanStreamingAssetsManifestAsync()
        {
            var documents = new List<DrawingDocument>();

#if UNITY_ANDROID && !UNITY_EDITOR
            string manifestUri = $"file:///android_asset/{DrawingsFolder}/manifest.txt";
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(manifestUri))
            {
                request.timeout = 10;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var lines = request.downloadHandler.text.Split('\n');
                    var topLevelImages = new List<string>();
                    var topLevelPdfs = new List<string>();
                    var folderFiles = new Dictionary<string, List<string>>();

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        if (!DrawingFileTypes.IsSupportedDrawingExtension(trimmed))
                            continue;

                        var dir = Path.GetDirectoryName(trimmed)?.Replace('\\', '/');
                        var fileName = Path.GetFileName(trimmed);

                        if (string.IsNullOrEmpty(dir) || dir == ".")
                        {
                            if (DrawingFileTypes.IsPdfExtension(trimmed))
                                topLevelPdfs.Add(fileName);
                            else
                                topLevelImages.Add(fileName);
                            continue;
                        }

                        string relativeDir = $"{DrawingsFolder}/{dir}";
                        if (!folderFiles.ContainsKey(relativeDir))
                            folderFiles[relativeDir] = new List<string>();

                        folderFiles[relativeDir].Add(fileName);
                    }

                    foreach (var fileName in topLevelImages)
                    {
                        documents.Add(DrawingDocument.FromSingleFile(
                            Path.GetFileNameWithoutExtension(fileName),
                            $"{DrawingsFolder}/{fileName}",
                            DrawingDocument.StorageSource.StreamingAssets));
                    }

                    foreach (var fileName in topLevelPdfs)
                    {
                        documents.Add(DrawingDocument.FromPdfFile(
                            Path.GetFileNameWithoutExtension(fileName),
                            $"{DrawingsFolder}/{fileName}",
                            DrawingDocument.StorageSource.StreamingAssets));
                    }

                    foreach (var kvp in folderFiles)
                    {
                        kvp.Value.Sort();
                        if (kvp.Value.All(DrawingFileTypes.IsPdfExtension))
                            continue;

                        var imagePages = kvp.Value.Where(f => DrawingFileTypes.IsImageExtension(f)).ToList();
                        if (imagePages.Count == 0)
                            continue;

                        documents.Add(DrawingDocument.FromFolder(
                            Path.GetFileName(kvp.Key),
                            kvp.Key,
                            imagePages,
                            DrawingDocument.StorageSource.StreamingAssets));
                    }
                }
            }
#endif

            await Task.CompletedTask;
            return documents;
        }

        public async Task<List<DrawingDocument>> ScanPersistentDataAsync()
        {
            var documents = new List<DrawingDocument>();

            try
            {
                string scanPath = Path.Combine(Application.persistentDataPath, DrawingsFolder);
                if (!Directory.Exists(scanPath))
                    return documents;

                documents.AddRange(ScanDirectory(scanPath, DrawingDocument.StorageSource.PersistentData, scanPath));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileScanner] Error scanning persistent data: {ex.Message}");
            }

            await Task.CompletedTask;
            return documents;
        }

        private static List<DrawingDocument> ScanDirectory(string scanPath, DrawingDocument.StorageSource source, string folderPath)
        {
            var documents = new List<DrawingDocument>();

            foreach (var ext in DrawingFileTypes.ImageExtensions)
            {
                foreach (var file in Directory.GetFiles(scanPath, $"*{ext}", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    string relativePath = $"{DrawingsFolder}/{fileName}";
                    documents.Add(DrawingDocument.FromSingleFile(
                        Path.GetFileNameWithoutExtension(file),
                        relativePath,
                        source,
                        folderPath));
                }
            }

            foreach (var file in Directory.GetFiles(scanPath, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                string relativePath = $"{DrawingsFolder}/{fileName}";
                int pageCount = 0;
#if UNITY_EDITOR
                pageCount = PdfPageRenderer.GetPageCountFromLocalPath(file);
#endif
                documents.Add(DrawingDocument.FromPdfFile(
                    Path.GetFileNameWithoutExtension(file),
                    relativePath,
                    source,
                    folderPath,
                    pageCount));
            }

            foreach (var subDir in Directory.GetDirectories(scanPath))
            {
                var pageFiles = new List<string>();
                foreach (var ext in DrawingFileTypes.ImageExtensions)
                {
                    pageFiles.AddRange(Directory.GetFiles(subDir, $"*{ext}", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName));
                }

                if (pageFiles.Count > 0)
                {
                    pageFiles.Sort();
                    string dirName = Path.GetFileName(subDir);
                    string relativePath = $"{DrawingsFolder}/{dirName}";
                    documents.Add(DrawingDocument.FromFolder(
                        dirName,
                        relativePath,
                        pageFiles,
                        source,
                        subDir));
                }
            }

            return documents;
        }

#if UNITY_EDITOR
        public static void GenerateStreamingAssetsManifest()
        {
            string scanPath = ResolveEditorStreamingDrawingsPath();
            if (!Directory.Exists(scanPath))
            {
                Debug.LogWarning($"[FileScanner] Cannot generate manifest: {scanPath} does not exist.");
                return;
            }

            var allFiles = new List<string>();
            CollectFilesRecursive(scanPath, scanPath, allFiles);

            string manifestPath = Path.Combine(scanPath, "manifest.txt");
            File.WriteAllLines(manifestPath, allFiles.Select(f => f.Replace('\\', '/')));
            Debug.Log($"[FileScanner] Generated manifest with {allFiles.Count} entries at {manifestPath}");
        }

        private static void CollectFilesRecursive(string basePath, string currentPath, List<string> result)
        {
            foreach (var file in Directory.GetFiles(currentPath))
            {
                if (Path.GetFileName(file).Equals("manifest.txt", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (DrawingFileTypes.IsSupportedDrawingExtension(file))
                {
                    string relative = file.Substring(basePath.Length + 1);
                    result.Add(relative);
                }
            }

            foreach (var dir in Directory.GetDirectories(currentPath))
                CollectFilesRecursive(basePath, dir, result);
        }

        private static string ResolveEditorStreamingDrawingsPath()
        {
            string standardPath = Path.Combine(Application.streamingAssetsPath, DrawingsFolder);
            if (Directory.Exists(standardPath))
                return standardPath;

            return Path.Combine(Application.dataPath, "_App/DrawingViewer/StreamingAssets/Drawings");
        }
#endif
    }
}
