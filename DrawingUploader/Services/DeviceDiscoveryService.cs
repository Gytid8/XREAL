using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DrawingUploader.Models;

namespace DrawingUploader.Services;

/// <summary>
/// UDP broadcast discovery service for finding XREAL DrawingViewer devices on the local network.
/// Sends discovery broadcasts and listens for device responses.
/// </summary>
public class DeviceDiscoveryService : IDisposable
{
    private const int DiscoveryPort = 52345;
    private const string DiscoveryMessage = "DRAWINGVIEWER_DISCOVERY";
    private const int DiscoveryTimeoutMs = 5000;

    private readonly UdpClient _udpClient;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    /// <summary>
    /// Raised when a device is discovered.
    /// </summary>
    public event Action<DrawingDevice>? OnDeviceDiscovered;

    /// <summary>
    /// Raised when discovery completes (timeout or cancellation).
    /// </summary>
    public event Action? OnDiscoveryComplete;

    public DeviceDiscoveryService()
    {
        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;
    }

    /// <summary>
    /// Starts device discovery. Sends broadcast and listens for responses asynchronously.
    /// </summary>
    public void StartDiscovery()
    {
        StopDiscovery();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                // Send discovery broadcast
                var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                var data = Encoding.UTF8.GetBytes(DiscoveryMessage);
                await _udpClient.SendAsync(data, data.Length, broadcastAddress);

                // Listen for responses with timeout
                using var timeoutCts = new CancellationTokenSource(DiscoveryTimeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                while (!linkedCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync(linkedCts.Token);
                        var response = Encoding.UTF8.GetString(result.Buffer);

                        if (TryParseDiscoveryResponse(response, result.RemoteEndPoint, out var device))
                        {
                            OnDeviceDiscovered?.Invoke(device);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Discovery] Error: {ex.Message}");
            }
            finally
            {
                OnDiscoveryComplete?.Invoke();
            }
        }, token);
    }

    /// <summary>
    /// Stops the current discovery process.
    /// </summary>
    public void StopDiscovery()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private static bool TryParseDiscoveryResponse(string response, IPEndPoint remote, out DrawingDevice device)
    {
        device = null!;

        try
        {
            // Try JSON format first
            if (response.TrimStart().StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("service", out var service) &&
                    service.GetString() == "drawing-uploader")
                {
                    var ip = root.TryGetProperty("ipAddress", out var ipEl)
                        ? ipEl.GetString() ?? remote.Address.ToString()
                        : remote.Address.ToString();

                    var port = root.TryGetProperty("port", out var portEl)
                        ? portEl.GetInt32()
                        : 8080;

                    var name = root.TryGetProperty("deviceName", out var nameEl)
                        ? nameEl.GetString() ?? "XREAL Device"
                        : "XREAL Device";

                    device = new DrawingDevice
                    {
                        DeviceName = name,
                        IpAddress = ip,
                        Port = port,
                        IsOnline = true
                    };
                    return true;
                }
            }
            else if (response.Contains(':'))
            {
                // Simple "IP:Port" format
                var parts = response.Split(':');
                if (parts.Length == 2 && IPAddress.TryParse(parts[0], out _) && int.TryParse(parts[1], out var port))
                {
                    device = new DrawingDevice
                    {
                        DeviceName = "XREAL Device",
                        IpAddress = parts[0],
                        Port = port,
                        IsOnline = true
                    };
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore parse failures
        }

        return false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopDiscovery();
        _udpClient.Close();
        _udpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
