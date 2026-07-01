using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class SyncAgentViewModel : ObservableObject
{
    private const int MaxLogLines = 100;

    private readonly SyncAgentConnectionService _connectionService;
    private readonly AgentSettingsStore _agentSettingsStore;
    private readonly AgentDataCleanupService _cleanupService;
    private readonly AppModeService _appModeService;
    private string _hubUrl = string.Empty;
    private string _nodeId = string.Empty;
    private string _pairingPassword = string.Empty;
    private string _statusText = "Отключено";
    private string _lastHeartbeatText = "—";
    private string _statusMessage = string.Empty;
    private string _currentProgress = string.Empty;
    private bool _isBusy;
    private bool _isProcessingJob;
    private bool _isConnected;

    public SyncAgentViewModel(
        SyncAgentConnectionService connectionService,
        AgentSettingsStore agentSettingsStore,
        AgentDataCleanupService cleanupService,
        AppModeService appModeService)
    {
        _connectionService = connectionService;
        _agentSettingsStore = agentSettingsStore;
        _cleanupService = cleanupService;
        _appModeService = appModeService;

        LogLines = new ObservableCollection<string>();

        _connectionService.LogLineAdded += OnLogLineAdded;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.ProgressChanged += OnProgressChanged;

        ConnectCommand = new RelayCommand(ConnectAsync, CanConnect);
        DisconnectCommand = new RelayCommand(DisconnectAsync, () => IsConnected && !IsBusy);
        CopyLogCommand = new RelayCommand(CopyLog, () => LogLines.Count > 0);
        CopyStatusCommand = new RelayCommand(CopyStatus, () => !string.IsNullOrWhiteSpace(StatusMessage));
        CleanupJobDirsCommand = new RelayCommand(CleanupJobDirsAsync, CanCleanup);
        CleanupAllAgentDataCommand = new RelayCommand(CleanupAllAgentDataAsync, CanCleanup);
        ResetModeCommand = new RelayCommand(ResetModeAsync, () => !IsBusy);

        LoadSettings();
    }

    public ObservableCollection<string> LogLines { get; }

    public string HubUrl
    {
        get => _hubUrl;
        set => SetProperty(ref _hubUrl, value);
    }

    public string NodeId
    {
        get => _nodeId;
        set => SetProperty(ref _nodeId, value);
    }

    public string PairingPassword
    {
        get => _pairingPassword;
        set => SetProperty(ref _pairingPassword, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string LastHeartbeatText
    {
        get => _lastHeartbeatText;
        set => SetProperty(ref _lastHeartbeatText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            SetProperty(ref _statusMessage, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string CurrentProgress
    {
        get => _currentProgress;
        set => SetProperty(ref _currentProgress, value);
    }

    public bool IsProcessingJob
    {
        get => _isProcessingJob;
        set => SetProperty(ref _isProcessingJob, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            SetProperty(ref _isConnected, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string LogText => string.Join(Environment.NewLine, LogLines);

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand CopyStatusCommand { get; }
    public RelayCommand CleanupJobDirsCommand { get; }
    public RelayCommand CleanupAllAgentDataCommand { get; }
    public RelayCommand ResetModeCommand { get; }

    private void LoadSettings()
    {
        var settings = _agentSettingsStore.Load();
        HubUrl = settings.HubUrl;
        NodeId = settings.NodeId;
    }

    private bool CanConnect() =>
        !IsBusy &&
        !IsConnected &&
        !string.IsNullOrWhiteSpace(HubUrl) &&
        !string.IsNullOrWhiteSpace(NodeId) &&
        !string.IsNullOrWhiteSpace(PairingPassword);

    private async Task ConnectAsync()
    {
        try
        {
            if (!Guid.TryParse(NodeId.Trim(), out var nodeGuid))
            {
                StatusMessage = "Node ID должен быть UUID.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            await _connectionService.ConnectAsync(HubUrl.Trim(), nodeGuid, PairingPassword);

            _agentSettingsStore.Save(new AgentSettings
            {
                HubUrl = HubUrl.Trim(),
                NodeId = NodeId.Trim(),
                AccessToken = _connectionService.AccessToken ?? string.Empty
            });

            PairingPassword = string.Empty;
        }
        catch (SyncAgentClientException ex) when (ex.StatusCode == 401)
        {
            StatusMessage = "Неверный pairing-пароль или узел отключён.";
            AddLog(StatusMessage);
        }
        catch (SyncAgentClientException ex)
        {
            StatusMessage = ex.Message;
            AddLog($"Connect failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            StatusMessage = FormatConnectFailure(ex);
            AddLog($"Connect failed: {StatusMessage}");
        }
        finally
        {
            IsBusy = false;
            IsConnected = _connectionService.IsConnected;
            StatusText = _connectionService.StatusText;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            IsBusy = true;
            await _connectionService.DisconnectAsync();
        }
        finally
        {
            IsBusy = false;
            IsConnected = false;
            StatusText = _connectionService.StatusText;
            LastHeartbeatText = "—";
        }
    }

    private void OnLogLineAdded(string line)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            LogLines.Insert(0, line);
            while (LogLines.Count > MaxLogLines)
                LogLines.RemoveAt(LogLines.Count - 1);

            RaisePropertyChanged(nameof(LogText));
            CommandManager.InvalidateRequerySuggested();

            StatusText = _connectionService.StatusText;
            if (_connectionService.LastHeartbeatAt is { } heartbeat)
                LastHeartbeatText = heartbeat.ToString("G");
        });
    }

    private void OnProgressChanged(string? progress)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentProgress = progress ?? string.Empty;
            IsProcessingJob = !string.IsNullOrWhiteSpace(progress);
            CommandManager.InvalidateRequerySuggested();
        });
    }

    private void OnConnectionStateChanged(bool connected)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsConnected = connected;
            StatusText = _connectionService.StatusText;
        });
    }

    private void AddLog(string line) => OnLogLineAdded(line);

    private bool CanCleanup() => !IsProcessingJob && !IsBusy;

    private async Task ResetModeAsync()
    {
        try
        {
            IsBusy = true;
            await _appModeService.ResetModeAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CleanupJobDirsAsync()
    {
        var confirm = System.Windows.MessageBox.Show(
            "Удалить все каталоги задач в agent\\work\\? Активная задача (если есть) не будет затронута.",
            "Очистка job-каталогов",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var result = _cleanupService.CleanupJobDirectories(_connectionService.ActiveJobId, includeResume: true);
            var message = $"Удалено: {result.Deleted.Count}";
            if (result.Skipped.Count > 0)
                message += $", пропущено (активный job): {result.Skipped.Count}";

            StatusMessage = string.Empty;
            AddLog(message);
            foreach (var path in result.Deleted)
                AddLog($"  удалено: {path}");
            foreach (var path in result.Skipped)
                AddLog($"  пропущено: {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddLog($"Очистка job-каталогов: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task CleanupAllAgentDataAsync()
    {
        if (IsConnected)
        {
            var warn = System.Windows.MessageBox.Show(
                "Перед полной очисткой будет выполнено отключение от Hub.",
                "Полная очистка",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (warn != MessageBoxResult.OK)
                return;

            await DisconnectAsync();
        }

        var confirm = System.Windows.MessageBox.Show(
            "Удалить все следы ConfigAdmin на этой машине (%AppData%\\ConfigAdmin\\: agent, logs, configadmin.db)? " +
            "Каталоги выгрузки Hub на этом ПК не затрагиваются.",
            "Полная очистка следов ПО",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var result = _cleanupService.CleanupAllAgentData();
            _agentSettingsStore.Clear();

            HubUrl = string.Empty;
            NodeId = string.Empty;
            PairingPassword = string.Empty;
            StatusMessage = string.Empty;
            CurrentProgress = string.Empty;
            LastHeartbeatText = "—";
            StatusText = "Отключено";

            AddLog($"Полная очистка: удалено {result.Deleted.Count} объектов");
            foreach (var path in result.Deleted)
                AddLog($"  удалено: {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddLog($"Полная очистка: {ex.Message}");
        }
    }

    private void CopyLog()
    {
        var text = LogText;
        if (!string.IsNullOrWhiteSpace(text))
            System.Windows.Clipboard.SetText(text);
    }

    private void CopyStatus()
    {
        if (!string.IsNullOrWhiteSpace(StatusMessage))
            System.Windows.Clipboard.SetText(StatusMessage);
    }

    private static string FormatConnectFailure(Exception ex)
    {
        if (ex is HttpRequestException { InnerException: SocketException sex })
            return FormatSocketMessage(sex);

        if (ex is SocketException direct)
            return FormatSocketMessage(direct);

        var msg = ex.Message;
        if (msg.Contains("11004", StringComparison.Ordinal) ||
            msg.Contains("WSANO_DATA", StringComparison.OrdinalIgnoreCase))
        {
            return "DNS на RDP не может разрешить Hub URL. Системный DNS блокирует *.ts.net; " +
                   "публичный DNS (DoH) тоже недоступен. Проверьте исходящий HTTPS.";
        }

        return msg;
    }

    private static string FormatSocketMessage(SocketException ex) =>
        ex.NativeErrorCode == 11004 || ex.SocketErrorCode == SocketError.HostNotFound
            ? "DNS на RDP не может разрешить Hub URL. Системный DNS блокирует *.ts.net."
            : ex.Message;
}
