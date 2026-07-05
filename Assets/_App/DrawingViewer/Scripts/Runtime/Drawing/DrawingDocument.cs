using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Data model representing a multi-page engineering drawing document.
    /// Supports image sequences and PDF files.
    /// </summary>
    [Serializable]
    public class DrawingDocument
    {
        public const string PdfPagePathPrefix = "pdfpage://";

        /// <summary>Display name of the document (derived from file/folder name).</summary>
        public string Name;

        /// <summary>Full path to the document directory or single file.</summary>
        public string FolderPath;

        /// <summary>Relative path within StreamingAssets or persistent data.</summary>
        public string RelativePath;

        /// <summary>List of page file names (e.g., "page-01.png"). Empty for PDF documents.</summary>
        public List<string> PageFileNames;

        /// <summary>Number of pages for PDF documents. Populated lazily if zero.</summary>
        public int PdfPageCount;

        /// <summary>Underlying storage format.</summary>
        public DocumentFormat Format = DocumentFormat.Image;

        /// <summary>When the document was imported/last modified.</summary>
        public DateTime ImportDate;

        /// <summary>The storage source type.</summary>
        public StorageSource Source;

        /// <summary>Total number of pages.</summary>
        public int PageCount => Format == DocumentFormat.Pdf
            ? PdfPageCount
            : PageFileNames?.Count ?? 0;

        public bool IsPdf => Format == DocumentFormat.Pdf;

        /// <summary>Gets the full path for a specific page.</summary>
        public string GetPagePath(int pageIndex)
        {
            if (Format == DocumentFormat.Pdf)
            {
                if (pageIndex < 0 || (PdfPageCount > 0 && pageIndex >= PdfPageCount))
                    return null;

                return $"{PdfPagePathPrefix}{(int)Source}/{RelativePath}#{pageIndex}";
            }

            if (PageFileNames == null || pageIndex < 0 || pageIndex >= PageFileNames.Count)
                return null;

            if (Source == StorageSource.StreamingAssets)
            {
                string relativePath = string.IsNullOrEmpty(RelativePath)
                    ? PageFileNames[pageIndex]
                    : Path.Combine(RelativePath, PageFileNames[pageIndex]);

                return relativePath.Replace('\\', '/');
            }

            return Path.Combine(FolderPath, PageFileNames[pageIndex]);
        }

        /// <summary>
        /// Creates a single-page document from a single image file.
        /// </summary>
        public static DrawingDocument FromSingleFile(string name, string relativePath, StorageSource source, string folderPath = null)
        {
            string normalizedPath = NormalizePath(relativePath);
            string directory = NormalizePath(Path.GetDirectoryName(normalizedPath));
            string fileName = Path.GetFileName(normalizedPath);

            return new DrawingDocument
            {
                Name = name,
                FolderPath = folderPath,
                RelativePath = directory,
                PageFileNames = new List<string> { fileName },
                ImportDate = DateTime.Now,
                Source = source,
                Format = DocumentFormat.Image
            };
        }

        /// <summary>
        /// Creates a PDF document. Page count may be filled later.
        /// </summary>
        public static DrawingDocument FromPdfFile(string name, string relativePath, StorageSource source, string folderPath = null, int pageCount = 0)
        {
            return new DrawingDocument
            {
                Name = name,
                FolderPath = folderPath,
                RelativePath = NormalizePath(relativePath),
                PageFileNames = new List<string>(),
                PdfPageCount = pageCount,
                ImportDate = DateTime.Now,
                Source = source,
                Format = DocumentFormat.Pdf
            };
        }

        /// <summary>
        /// Creates a multi-page document from a folder of image files.
        /// </summary>
        public static DrawingDocument FromFolder(string name, string relativePath, List<string> pageFiles, StorageSource source, string folderPath = null)
        {
            return new DrawingDocument
            {
                Name = name,
                FolderPath = folderPath,
                RelativePath = NormalizePath(relativePath),
                PageFileNames = pageFiles,
                ImportDate = DateTime.Now,
                Source = source,
                Format = DocumentFormat.Image
            };
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        /// <summary>
        /// The storage location type for this document.
        /// </summary>
        public enum StorageSource
        {
            StreamingAssets,
            PersistentData,
            ExternalStorage
        }

        public enum DocumentFormat
        {
            Image,
            Pdf
        }

        public override string ToString()
        {
            return $"{Name} ({PageCount} pages) [{Source}/{Format}]";
        }
    }
}
