using System;
using System.IO;
using System.Text;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Minimal multipart/form-data parser for single-file uploads from DrawingUploader.
    /// </summary>
    internal static class MultipartFormDataReader
    {
        public static bool TryReadFile(
            Stream stream,
            string contentType,
            out byte[] fileBytes,
            out string fileName)
        {
            fileBytes = null;
            fileName = null;

            if (stream == null || string.IsNullOrEmpty(contentType))
                return false;

            var boundary = ExtractBoundary(contentType);
            if (string.IsNullOrEmpty(boundary))
                return false;

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var body = memoryStream.ToArray();
            if (body.Length == 0)
                return false;

            var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
            int startIndex = IndexOf(body, boundaryBytes, 0);
            if (startIndex < 0)
                return false;

            startIndex += boundaryBytes.Length;
            if (startIndex + 1 < body.Length && body[startIndex] == (byte)'\r' && body[startIndex + 1] == (byte)'\n')
                startIndex += 2;
            else if (startIndex < body.Length && body[startIndex] == (byte)'\n')
                startIndex += 1;

            int headerEnd = IndexOf(body, Encoding.UTF8.GetBytes("\r\n\r\n"), startIndex);
            if (headerEnd < 0)
                return false;

            var headers = Encoding.UTF8.GetString(body, startIndex, headerEnd - startIndex);
            fileName = ExtractFileName(headers);
            int contentStart = headerEnd + 4;

            int nextBoundary = IndexOf(body, boundaryBytes, contentStart);
            if (nextBoundary < 0)
                return false;

            int contentEnd = nextBoundary;
            if (contentEnd - 2 >= contentStart &&
                body[contentEnd - 2] == (byte)'\r' &&
                body[contentEnd - 1] == (byte)'\n')
            {
                contentEnd -= 2;
            }

            if (contentEnd <= contentStart)
                return false;

            fileBytes = new byte[contentEnd - contentStart];
            Buffer.BlockCopy(body, contentStart, fileBytes, 0, fileBytes.Length);
            return fileBytes.Length > 0;
        }

        private static string ExtractBoundary(string contentType)
        {
            const string marker = "boundary=";
            int index = contentType.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            var boundary = contentType.Substring(index + marker.Length).Trim();
            if (boundary.StartsWith("\"", StringComparison.Ordinal) && boundary.EndsWith("\"", StringComparison.Ordinal))
                boundary = boundary.Substring(1, boundary.Length - 2);

            return boundary;
        }

        private static string ExtractFileName(string headers)
        {
            foreach (var line in headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                    continue;

                const string marker = "filename=\"";
                int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                    continue;

                start += marker.Length;
                int end = line.IndexOf('"', start);
                if (end <= start)
                    continue;

                return line.Substring(start, end - start);
            }

            return "upload.png";
        }

        private static int IndexOf(byte[] source, byte[] pattern, int startIndex)
        {
            if (source == null || pattern == null || pattern.Length == 0 || startIndex >= source.Length)
                return -1;

            for (int i = startIndex; i <= source.Length - pattern.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                    return i;
            }

            return -1;
        }
    }
}
