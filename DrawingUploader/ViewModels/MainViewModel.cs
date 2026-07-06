using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DrawingUploader.Models;
using DrawingUploader.Services;

namespace DrawingUploader.ViewModels;

/// <summary>
/// Main view model for the Drawing Uploader application.
/// Manages device discovery, file selection, and upload operations.
/// </summary>
public class MainViewModel : ObservableObject
{
    private readonly DeviceDiscoveryService _discoveryService;
    private readonly UploadService _uploadService;

    private DrawingDevice? _selectedDevice;
    private string _selectedFilePath = string.Empty;
    private string _selectedFileName = string.Empty;
    private double _uploadProgress;
    private string _statusMessage = "就绪";
    private bool _isUploading;
    private bool _isDiscovering;
    private bool _isDragOver;
    private string _manualIp = string.Empty;
    private string _manualPort = "8080";

    public MainViewModel()
    {
        _discoveryService = new DeviceDiscoveryService();
        _uploadService = new UploadService();

        Devices = new ObservableCollection<DrawingDevice>();

        WireDiscoveryEvents();
    }

    private void WireDiscoveryEvents()
    {
        _discoveryService.OnDeviceDiscovered += device =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // Avoid duplicates by IP
                var existing = Devices.FirstOrDefault(d => d.IpAddress == device.IpAddress);
                if (existing != null)
                {
                    existing.IsOnline = true;
                    existing.Port = device.Port;
                    existing.DeviceName = device.DeviceName;
                }
                else
                {
                    Devices.Add(device);
                }

                StatusMessage = $"发现设备: {device.DisplayText}";
            });
        };

        _discoveryService.OnDiscoveryComplete += () =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsDiscovering = false;
                if (Devices.Count == 0)
                {
                    StatusMessage = "未发现设备，请确认 XREAL 眼镜已连接同一 WiFi 网络";
                }
            });
        };
    }

    /// <summary>
    /// List of discovered devices.
    /// </summary>
    public ObservableCollection<DrawingDevice> Devices { get; }

    /// <summary>
    /// Currently selected device for upload.
    /// </summary>
    public DrawingDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            SetProperty(ref _selectedDevice, value);
            OnPropertyChanged(nameof(CanUpload));
        }
    }

    /// <summary>
    /// Full path of the selected file.
    /// </summary>
    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            SetProperty(ref _selectedFilePath, value);
            SelectedFileName = !string.IsNullOrEmpty(value) ? Path.GetFileName(value) : string.Empty;
            OnPropertyChanged(nameof(CanUpload));
        }
    }

    /// <summary>
    /// Display name of the selected file.
    /// </summary>
    public string SelectedFileName
    {
        get => _selectedFileName;
        set => SetProperty(ref _selectedFileName, value);
    }

    /// <summary>
    /// Upload progress (0.0 to 1.0).
    /// </summary>
    public double UploadProgress
    {
        get => _uploadProgress;
        set => SetProperty(ref _uploadProgress, value);
    }

    /// <summary>
    /// Status message displayed to the user.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Whether an upload is currently in progress.
    /// </summary>
    public bool IsUploading
    {
        get => _isUploading;
        set
        {
            SetProperty(ref _isUploading, value);
            OnPropertyChanged(nameof(CanUpload));
            OnPropertyChanged(nameof(CanSelectFile));
        }
    }

    /// <summary>
    /// Whether discovery is currently running.
    /// </summary>
    public bool IsDiscovering
    {
        get => _isDiscovering;
        set
        {
            SetProperty(ref _isDiscovering, value);
            OnPropertyChanged(nameof(CanStartDiscovery));
        }
    }

    /// <summary>
    /// Whether the drag-drop area is being hovered.
    /// </summary>
    public bool IsDragOver
    {
        get => _isDragOver;
        set => SetProperty(ref _isDragOver, value);
    }

    /// <summary>
    /// Manual IP entry.
    /// </summary>
    public string ManualIp
    {
        get => _manualIp;
        set => SetProperty(ref _manualIp, value);
    }

    /// <summary>
    /// Manual port entry.
    /// </summary>
    public string ManualPort
    {
        get => _manualPort;
        set => SetProperty(ref _manualPort, value);
    }

    // ── Commands ──

    public ICommand StartDiscoveryCommand => new RelayCommand(_ => StartDiscovery());
    public ICommand StopDiscoveryCommand => new RelayCommand(_ => StopDiscovery());
    public ICommand SelectFileCommand => new RelayCommand(_ => SelectFile());
    public ICommand UploadCommand => new RelayCommand(_ => UploadAsync(), _ => CanUpload);
    public ICommand CancelCommand => new RelayCommand(_ => CancelUpload());
    public ICommand ConnectManualCommand => new RelayCommand(_ => ConnectManual());
    public ICommand ClearLogCommand => new RelayCommand(_ => StatusMessage = "就绪");

    public bool CanUpload => SelectedDevice != null && !string.IsNullOrEmpty(SelectedFilePath) && !IsUploading;
    public bool CanSelectFile => !IsUploading;
    public bool CanStartDiscovery => !IsDiscovering;

    private CancellationTokenSource? _uploadCts;

    /// <summary>
    /// Starts UDP device discovery.
    /// </summary>
    public void StartDiscovery()
    {
        Devices.Clear();
        StatusMessage = "扫描中...确保 XREAL 眼镜已开机并连接同一 WiFi";
        IsDiscovering = true;
        _discoveryService.StartDiscovery();
    }

    /// <summary>
    /// Stops device discovery.
    /// </summary>
    public void StopDiscovery()
    {
        _discoveryService.StopDiscovery();
        IsDiscovering = false;
    }

    /// <summary>
    /// Opens a file dialog to select a drawing (PNG/JPG).
    /// </summary>
    public void SelectFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图纸文件",
            Filter = "图纸文件 (*.png;*.jpg;*.jpeg;*.pdf)|*.png;*.jpg;*.jpeg;*.pdf|所有文件 (*.*)|*.*",
            Multiselect = false,
            CheckFileExists = true,
            RestoreDirectory = true
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFilePath = dialog.FileName;
            StatusMessage = $"已选择: {SelectedFileName}";
        }
    }

    /// <summary>
    /// Handles a dropped file from drag-drop operation.
    /// </summary>
    public void HandleFileDrop(string[] files)
    {
        if (files == null || files.Length == 0) return;

        var file = files[0];
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".pdf")
        {
            SelectedFilePath = file;
            StatusMessage = $"已拖拽文件: {SelectedFileName}";
        }
        else
        {
            StatusMessage = "仅支持 PNG/JPG 格式的图纸文件";
        }
    }

    /// <summary>
    /// Uploads the selected file to the selected device.
    /// </summary>
    public async void UploadAsync()
    {
        if (!CanUpload) return;

        var device = SelectedDevice!;
        IsUploading = true;
        UploadProgress = 0;
        _uploadCts = new CancellationTokenSource();

        StatusMessage = $"正在上传 {SelectedFileName} → {device.DisplayText}...";

        var progress = new Progress<double>(p => UploadProgress = p);

        var result = await _uploadService.UploadFileAsync(
            SelectedFilePath,
            device.BaseUrl,
            progress,
            _uploadCts.Token);

        IsUploading = false;
        UploadProgress = result.Success ? 1.0 : 0;

        StatusMessage = $"[{DateTime.Now:HH:mm:ss}] {result.Message}";
        _uploadCts = null;

        if (result.Success)
        {
            // Auto-clear file selection after successful upload
            SelectedFilePath = string.Empty;
        }
    }

    /// <summary>
    /// Cancels the current upload.
    /// </summary>
    public void CancelUpload()
    {
        _uploadCts?.Cancel();
        StatusMessage = "上传已取消";
        IsUploading = false;
    }

    /// <summary>
    /// Connects to a device using manually entered IP and port.
    /// </summary>
    private async void ConnectManual()
    {
        if (string.IsNullOrWhiteSpace(ManualIp))
        {
            StatusMessage = "请输入 IP 地址";
            return;
        }

        if (!int.TryParse(ManualPort, out var port) || port < 1 || port > 65535)
        {
            StatusMessage = "端口号无效 (1-65535)";
            return;
        }

        StatusMessage = $"正在连接 {ManualIp}:{port}...";
        var serverUrl = $"http://{ManualIp}:{port}";
        var isReachable = await _uploadService.CheckServerAsync(serverUrl);

        if (isReachable)
        {
            var existing = Devices.FirstOrDefault(d => d.IpAddress == ManualIp);
            if (existing != null)
            {
                SelectedDevice = existing;
            }
            else
            {
                var device = new DrawingDevice
                {
                    DeviceName = "手动连接",
                    IpAddress = ManualIp,
                    Port = port,
                    IsOnline = true
                };
                Devices.Add(device);
                SelectedDevice = device;
            }

            StatusMessage = $"已连接到 {ManualIp}:{port}";
        }
        else
        {
            StatusMessage = $"无法连接到 {ManualIp}:{port}，请检查设备是否在线";
        }
    }
}
