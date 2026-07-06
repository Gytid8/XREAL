using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DrawingUploader.Services;

/// <summary>
/// HTTP multipart upload service for sending drawing files to the XREAL device.
/// Supports progress reporting and cancellation.
/// </summary>
public class UploadService : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _isDisposed;

    public UploadService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // Allow large file uploads
        };
    }

    /// <summary>
    /// Upload result returned after processing.
    /// </summary>
    public class UploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Uploads a file to the specified device via HTTP POST /upload.
    /// Reports progress via IProgress{double} (0.0 to 1.0).
    /// </summary>
    public async Task<UploadResult> UploadFileAsync(
        string filePath,
        string serverUrl,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new UploadResult
        {
            FileName = Path.GetFileName(filePath),
            FileSize = new FileInfo(filePath).Length
        };

        var startTime = DateTime.UtcNow;

        try
        {
            if (!File.Exists(filePath))
            {
                result.Message = $"文件不存在: {filePath}";
                return result;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".pdf")
            {
                result.Message = "仅支持 PNG/JPG/PDF 格式的图纸文件";
                return result;
            }

            var uploadUrl = serverUrl.TrimEnd('/') + "/upload";

            using var formData = new MultipartFormDataContent();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var progressStream = progress != null
                ? new ProgressStream(fileStream, result.FileSize, progress, cancellationToken)
                : null;

            var streamContent = progressStream != null
                ? new StreamContent(progressStream)
                : new StreamContent(fileStream);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue(
                ext == ".png" ? "image/png" :
                ext == ".pdf" ? "application/pdf" :
                "image/jpeg");

            formData.Add(streamContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(uploadUrl, formData, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            result.Duration = DateTime.UtcNow - startTime;
            result.Success = response.IsSuccessStatusCode;

            if (response.IsSuccessStatusCode)
            {
                result.Message = $"上传成功！({FormatFileSize(result.FileSize)}, {result.Duration.TotalSeconds:F1}秒)";
            }
            else
            {
                result.Message = $"上传失败 ({(int)response.StatusCode}): {responseBody}";
            }
        }
        catch (TaskCanceledException)
        {
            result.Message = "上传已取消（超时或用户取消）";
        }
        catch (HttpRequestException ex)
        {
            result.Message = $"网络错误: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Message = $"上传出错: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Checks if a device server is reachable by calling GET /api/status.
    /// </summary>
    public async Task<bool> CheckServerAsync(string serverUrl, CancellationToken ct = default)
    {
        try
        {
            var url = serverUrl.TrimEnd('/') + "/api/status";
            using var timeoutCts = new CancellationTokenSource(3000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var response = await _httpClient.GetAsync(url, linkedCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Progress-tracking stream wrapper that reports bytes read.
    /// </summary>
    private class ProgressStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly long _totalBytes;
        private readonly IProgress<double> _progress;
        private readonly CancellationToken _cancellationToken;
        private long _bytesRead;

        public ProgressStream(Stream innerStream, long totalBytes, IProgress<double> progress, CancellationToken ct)
        {
            _innerStream = innerStream;
            _totalBytes = totalBytes;
            _progress = progress;
            _cancellationToken = ct;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _totalBytes;
        public override long Position { get => _bytesRead; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var read = _innerStream.Read(buffer, offset, count);
            _bytesRead += read;
            if (_totalBytes > 0)
                _progress.Report((double)_bytesRead / _totalBytes);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken).Token;
            linkedToken.ThrowIfCancellationRequested();
            var read = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
            _bytesRead += read;
            if (_totalBytes > 0)
                _progress.Report((double)_bytesRead / _totalBytes);
            return read;
        }

        public override void Flush() => _innerStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _innerStream.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
