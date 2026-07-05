using System;
using System.IO;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Shared drawing file extension rules for scanner, import, and upload paths.
    /// </summary>
    public static class DrawingFileTypes
    {
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
        public static readonly string[] PdfExtensions = { ".pdf" };

        public static readonly string[] AllDrawingExtensions = { ".png", ".jpg", ".jpeg", ".pdf" };

        public static bool IsImageExtension(string path)
        {
            string ext = Path.GetExtension(path)?.ToLowerInvariant();
            return Array.IndexOf(ImageExtensions, ext) >= 0;
        }

        public static bool IsPdfExtension(string path)
        {
            string ext = Path.GetExtension(path)?.ToLowerInvariant();
            return Array.IndexOf(PdfExtensions, ext) >= 0;
        }

        public static bool IsSupportedDrawingExtension(string path)
        {
            return IsImageExtension(path) || IsPdfExtension(path);
        }
    }
}
