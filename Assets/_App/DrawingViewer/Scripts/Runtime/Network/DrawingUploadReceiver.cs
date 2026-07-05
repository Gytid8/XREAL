using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Receives drawing uploads from the Windows DrawingUploader companion app.
    /// Implements UDP device discovery (port 52345) and HTTP upload server (port 8080).
    /// </summary>
    [DisallowMultipleComponent]
    public class DrawingUploadReceiver : MonoBehaviour
    {
        public const string DiscoveryMessage = "DRAWINGVIEWER_DISCOVERY";
        public const string ServiceName = "drawing-uploader";

        [Header("Server")]
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private int _httpPort = 8080;
        [SerializeField] private int _discoveryPort = 52345;
        [SerializeField] private string _deviceDisplayName = "XREAL Drawing Viewer";

        private HttpListener _httpListener;
        private UdpClient _udpClient;
        private CancellationTokenSource _serverCts;
        private Task _httpTask;
        private Task _udpTask;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private bool _isRunning;

        /// <summary>
        /// Raised on the main thread after a file is saved to persistent storage.
        /// Parameters: imported file path, original file name.
        /// </summary>
        public event Action<string, string> OnDrawingReceived;

        /// <summary>
        /// Whether the upload server is currently listening.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Current HTTP listen port.
        /// </summary>
        public int HttpPort => _httpPort;

        /// <summary>
        /// Last resolved LAN IPv4 address used for discovery responses.
        /// </summary>
        public string LocalIpAddress { get; private set; }

        private void Start()
        {
            if (_autoStart)
                StartServer();
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DrawingUploadReceiver] Main-thread callback failed: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        /// <summary>
        /// Starts UDP discovery and the HTTP upload server.
        /// </summary>
        public void StartServer()
        {
            if (_isRunning)
                return;

            LocalIpAddress = LocalNetworkUtility.GetLocalIPv4();
            _serverCts = new CancellationTokenSource();

            try
            {
                StartHttpServer(_serverCts.Token);
                StartDiscoveryResponder(_serverCts.Token);
                _isRunning = true;
                Debug.Log($"[DrawingUploadReceiver] Listening on {LocalIpAddress}:{_httpPort}, discovery UDP {_discoveryPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DrawingUploadReceiver] Failed to start server: {ex.Message}");
                StopServer();
            }
        }

        /// <summary>
        /// Stops all network listeners.
        /// </summary>
        public void StopServer()
        {
            if (!_isRunning && _serverCts == null)
                return;

            _isRunning = false;

            try
            {
                _serverCts?.Cancel();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DrawingUploadReceiver] Cancel failed: {ex.Message}");
            }

            try
            {
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DrawingUploadReceiver] HTTP stop failed: {ex.Message}");
            }

            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DrawingUploadReceiver] UDP stop failed: {ex.Message}");
            }

            _httpListener = null;
            _udpClient = null;
            _serverCts?.Dispose();
            _serverCts = null;
        }

        /// <summary>
        /// Applies runtime settings from DrawingViewerSettings.
        /// </summary>
        public void ApplySettings(DrawingViewerSettings settings)
        {
            if (settings == null)
                return;

            bool wasRunning = _isRunning;
            if (wasRunning)
                StopServer();

            _autoStart = settings.EnableNetworkUpload;
            _httpPort = settings.UploadHttpPort;
            _discoveryPort = settings.DiscoveryUdpPort;
            _deviceDisplayName = string.IsNullOrWhiteSpace(settings.DeviceDisplayName)
                ? _deviceDisplayName
                : settings.DeviceDisplayName;

            if (_autoStart && wasRunning)
                StartServer();
        }

        private void StartHttpServer(CancellationToken token)
        {
            _httpListener = new HttpListener();

#if UNITY_ANDROID && !UNITY_EDITOR
            _httpListener.Prefixes.Add($"http://*:{_httpPort}/");
#else
            _httpListener.Prefixes.Add($"http://127.0.0.1:{_httpPort}/");
            _httpListener.Prefixes.Add($"http://localhost:{_httpPort}/");

            var localIp = LocalNetworkUtility.GetLocalIPv4();
            if (!string.IsNullOrEmpty(localIp))
                _httpListener.Prefixes.Add($"http://{localIp}:{_httpPort}/");

            _httpListener.Prefixes.Add($"http://+:{_httpPort}/");
#endif

            try
            {
                _httpListener.Start();
            }
            catch (HttpListenerException) when (_httpListener.Prefixes.Count > 1)
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://127.0.0.1:{_httpPort}/");
                var fallbackIp = LocalNetworkUtility.GetLocalIPv4();
                if (!string.IsNullOrEmpty(fallbackIp))
                    _httpListener.Prefixes.Add($"http://{fallbackIp}:{_httpPort}/");
                _httpListener.Start();
            }

            _httpTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    HttpListenerContext context = null;
                    try
                    {
                        context = await _httpListener.GetContextAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (HttpListenerException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                            Debug.LogWarning($"[DrawingUploadReceiver] HTTP accept error: {ex.Message}");
                        continue;
                    }

                    if (context != null)
                        _ = Task.Run(() => HandleHttpRequest(context), token);
                }
            }, token);
        }

        private void StartDiscoveryResponder(CancellationToken token)
        {
            _udpClient = new UdpClient(_discoveryPort);
            _udpClient.EnableBroadcast = true;

            _udpTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result;
                    try
                    {
                        result = await _udpClient.ReceiveAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                            Debug.LogWarning($"[DrawingUploadReceiver] UDP receive error: {ex.Message}");
                        continue;
                    }

                    var message = Encoding.UTF8.GetString(result.Buffer).Trim();
                    if (!string.Equals(message, DiscoveryMessage, StringComparison.Ordinal))
                        continue;

                    var ip = LocalNetworkUtility.GetLocalIPv4() ?? LocalIpAddress ?? result.RemoteEndPoint.Address.ToString();
                    LocalIpAddress = ip;

                    var response = BuildDiscoveryResponse(ip, _httpPort, _deviceDisplayName);
                    var responseBytes = Encoding.UTF8.GetBytes(response);

                    try
                    {
                        await _udpClient.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DrawingUploadReceiver] UDP response failed: {ex.Message}");
                    }
                }
            }, token);
        }

        private void HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

                if (request.HttpMethod == "GET" && string.Equals(path, "/api/status", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(response, 200, BuildStatusResponse(_deviceDisplayName));
                    return;
                }

                if (request.HttpMethod == "POST" && string.Equals(path, "/upload", StringComparison.OrdinalIgnoreCase))
                {
                    HandleUploadRequest(request, response);
                    return;
                }

                WriteText(response, 404, "Not Found");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DrawingUploadReceiver] Request handling failed: {ex.Message}");
                try
                {
                    WriteText(context.Response, 500, ex.Message);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void HandleUploadRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            var contentType = request.ContentType ?? string.Empty;
            if (!contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                WriteText(response, 400, "Expected multipart/form-data upload.");
                return;
            }

            if (!MultipartFormDataReader.TryReadFile(request.InputStream, contentType, out var fileBytes, out var fileName))
            {
                WriteText(response, 400, "Could not parse uploaded file.");
                return;
            }

            if (!FileImportHelper.IsSupportedDrawingExtension(fileName))
            {
                WriteText(response, 400, "Only PNG/JPG/PDF files are supported.");
                return;
            }

            string tempPath = Path.Combine(Application.temporaryCachePath, "DrawingUpload", Guid.NewGuid().ToString("N") + Path.GetExtension(fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath) ?? Application.temporaryCachePath);
            File.WriteAllBytes(tempPath, fileBytes);

            string importedPath = FileImportHelper.ImportFile(tempPath, Path.GetFileName(fileName));
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DrawingUploadReceiver] Temp cleanup failed: {ex.Message}");
            }

            if (string.IsNullOrEmpty(importedPath))
            {
                WriteText(response, 500, "Failed to save uploaded drawing.");
                return;
            }

            Debug.Log($"[DrawingUploadReceiver] Received drawing: {importedPath}");
            EnqueueMainThread(() => OnDrawingReceived?.Invoke(importedPath, fileName));

            WriteJson(response, 200, $"{{\"success\":true,\"fileName\":\"{EscapeJson(fileName)}\"}}");
        }

        private static string BuildDiscoveryResponse(string ipAddress, int port, string deviceName)
        {
            return $"{{\"service\":\"{ServiceName}\",\"ipAddress\":\"{EscapeJson(ipAddress)}\",\"port\":{port},\"deviceName\":\"{EscapeJson(deviceName)}\"}}";
        }

        private static string BuildStatusResponse(string deviceName)
        {
            return $"{{\"status\":\"ready\",\"service\":\"{ServiceName}\",\"deviceName\":\"{EscapeJson(deviceName)}\"}}";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void EnqueueMainThread(Action action)
        {
            if (action == null)
                return;

            _mainThreadQueue.Enqueue(action);
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
        {
            var buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static void WriteText(HttpListenerResponse response, int statusCode, string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text ?? string.Empty);
            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
