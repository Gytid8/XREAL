namespace DrawingUploader.Models;

/// <summary>
/// Represents a discovered XREAL Drawing Viewer device on the local network.
/// </summary>
public class DrawingDevice : ObservableObject
{
    private string _deviceName = string.Empty;
    private string _ipAddress = string.Empty;
    private int _port = 8080;
    private string _status = "离线";
    private bool _isOnline;

    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public int Port
    {
        get => _port;
        set
        {
            SetProperty(ref _port, value);
            OnPropertyChanged(nameof(BaseUrl));
        }
    }

    public string BaseUrl => $"http://{IpAddress}:{Port}";

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            SetProperty(ref _isOnline, value);
            Status = value ? "在线" : "离线";
        }
    }

    public string DisplayText => $"{DeviceName} ({IpAddress}:{Port})";
}
