using System;
using System.IO;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Helper for importing and deleting user drawings in persistent storage.
    /// Built-in StreamingAssets drawings inside the APK cannot be deleted at runtime.
    /// </summary>
    public static class FileImportHelper
    {
        public const string DrawingsFolderName = "Drawings";

        private static readonly string[] SupportedExtensions = DrawingFileTypes.AllDrawingExtensions;

        /// <summary>
        /// Copies a file from an external path to the app's persistent Drawings folder.
        /// </summary>
        /// <returns>Imported file path, or null on failure.</returns>
        public static string ImportFile(string sourcePath, string targetFileName = null)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    UnityEngine.Debug.LogError($"[FileImportHelper] Source file not found: {sourcePath}");
                    return null;
                }

                if (!DrawingFileTypes.IsSupportedDrawingExtension(sourcePath))
                {
                    UnityEngine.Debug.LogError($"[FileImportHelper] Unsupported file type: {sourcePath}");
                    return null;
                }

                string drawingsDir = GetDrawingsFolder();
                string fileName = targetFileName ?? Path.GetFileName(sourcePath);
                string targetPath = GetUniqueFilePath(drawingsDir, fileName);

                File.Copy(sourcePath, targetPath, overwrite: false);

                UnityEngine.Debug.Log($"[FileImportHelper] Imported: {sourcePath} -> {targetPath}");
                return targetPath;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FileImportHelper] Import failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Imports multiple files from a directory.
        /// </summary>
        public static int ImportDirectory(string sourceDir, string[] extensions = null)
        {
            int count = 0;

            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    UnityEngine.Debug.LogError($"[FileImportHelper] Source directory not found: {sourceDir}");
                    return 0;
                }

                var files = Directory.GetFiles(sourceDir);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file)?.ToLowerInvariant();
                    if (extensions != null && Array.IndexOf(extensions, ext) < 0)
                        continue;

                    if (ImportFile(file) != null)
                        count++;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FileImportHelper] Directory import failed: {ex.Message}");
            }

            return count;
        }

        /// <summary>
        /// Gets the full path to the persistent Drawings folder.
        /// </summary>
        public static string GetDrawingsFolder()
        {
            string path = Path.Combine(UnityEngine.Application.persistentDataPath, DrawingsFolderName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns true when the document can be deleted at runtime.
        /// </summary>
        public static bool CanDelete(DrawingDocument document)
        {
            return document != null && document.Source == DrawingDocument.StorageSource.PersistentData;
        }

        /// <summary>
        /// Deletes a user-imported drawing from persistent storage.
        /// </summary>
        public static bool DeleteDocument(DrawingDocument document)
        {
            if (!CanDelete(document))
            {
                UnityEngine.Debug.LogWarning($"[FileImportHelper] Cannot delete built-in drawing: {document?.Name}");
                return false;
            }

            try
            {
                if (document.IsPdf)
                {
                    string filePath = PdfPathResolver.ResolveAbsolutePdfPath(document.RelativePath, document.Source);
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        File.Delete(filePath);
                }
                else if (document.PageCount <= 1 && document.PageFileNames != null && document.PageFileNames.Count == 1)
                {
                    string filePath = document.GetPagePath(0);
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        File.Delete(filePath);
                }
                else if (!string.IsNullOrEmpty(document.FolderPath) && Directory.Exists(document.FolderPath))
                {
                    Directory.Delete(document.FolderPath, recursive: true);
                }
                else if (!string.IsNullOrEmpty(document.FolderPath))
                {
                    foreach (var pageName in document.PageFileNames)
                    {
                        string pagePath = Path.Combine(document.FolderPath, pageName);
                        if (File.Exists(pagePath))
                            File.Delete(pagePath);
                    }
                }

                UnityEngine.Debug.Log($"[FileImportHelper] Deleted drawing: {document.Name}");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FileImportHelper] Delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes all imported drawings from persistent storage.
        /// </summary>
        public static void ClearImportedDrawings()
        {
            string path = Path.Combine(UnityEngine.Application.persistentDataPath, DrawingsFolderName);

            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                Directory.CreateDirectory(path);
                UnityEngine.Debug.Log("[FileImportHelper] Imported drawings cleared.");
            }
        }

        public static bool IsSupportedImageExtension(string path)
        {
            return DrawingFileTypes.IsImageExtension(path);
        }

        public static bool IsSupportedDrawingExtension(string path)
        {
            return DrawingFileTypes.IsSupportedDrawingExtension(path);
        }

        private static string GetUniqueFilePath(string directory, string fileName)
        {
            string targetPath = Path.Combine(directory, fileName);
            if (!File.Exists(targetPath))
                return targetPath;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int index = 1;

            while (true)
            {
                string candidate = Path.Combine(directory, $"{name}_{index}{ext}");
                if (!File.Exists(candidate))
                    return candidate;
                index++;
            }
        }
    }
}
