using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class DataMcpViewModel : BusyViewModelBase, IRefreshOnNavigate
{
    private readonly DataMcpSettingsService _settingsService;
    private readonly DataMcpSyncService _syncService;
    private readonly DataMcpToolClient _toolClient;
    private readonly VaultSessionService _vaultSessionService;
    private readonly UiActivityLog _activityLog;

    private Guid _toolInstanceId;
    private string _rootPath = string.Empty;
    private string _statusSummary = string.Empty;
    private string _mcpReadiness = string.Empty;
    private string _endpoint = string.Empty;
    private string _region = string.Empty;
    private string _bucket = string.Empty;
    private string _defaultPrefix = string.Empty;
    private string _sealedSecretsPath = "credentials.sealed.json";
    private string _dmcpPassword = string.Empty;
    private string _accessKeyId = string.Empty;
    private string _secretAccessKey = string.Empty;
    private bool _hasStoredDmcpPassword;
    private bool _hasSealedCredentialsFile;
    private string? _portableCredentialsPath;
    private bool _initialized;

    public DataMcpViewModel(
        DataMcpSettingsService settingsService,
        DataMcpSyncService syncService,
        DataMcpToolClient toolClient,
        VaultSessionService vaultSessionService,
        UiActivityLog activityLog)
    {
        _settingsService = settingsService;
        _syncService = syncService;
        _toolClient = toolClient;
        _vaultSessionService = vaultSessionService;
        _activityLog = activityLog;

        Connections = new ObservableCollection<DataMcpConnectionRow>();

        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        SavePathCommand = new RelayCommand(SavePathAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
        OpenPortableCommand = new RelayCommand(OpenPortableFolderImpl, () => Directory.Exists(RootPath));
        SaveHubCommand = new RelayCommand(SaveHubAsync, CanSaveHub);
        SyncPortableCommand = new RelayCommand(SyncPortableAsync, CanSyncPortable);
    }

    public ObservableCollection<DataMcpConnectionRow> Connections { get; }

    public string RootPath
    {
        get => _rootPath;
        set => SetProperty(ref _rootPath, value);
    }

    public string StatusSummary
    {
        get => _statusSummary;
        set => SetProperty(ref _statusSummary, value);
    }

    public string McpReadiness
    {
        get => _mcpReadiness;
        set => SetProperty(ref _mcpReadiness, value);
    }

    public string Endpoint
    {
        get => _endpoint;
        set => SetProperty(ref _endpoint, value);
    }

    public string Region
    {
        get => _region;
        set => SetProperty(ref _region, value);
    }

    public string Bucket
    {
        get => _bucket;
        set => SetProperty(ref _bucket, value);
    }

    public string DefaultPrefix
    {
        get => _defaultPrefix;
        set => SetProperty(ref _defaultPrefix, value);
    }

    public string SealedSecretsPath
    {
        get => _sealedSecretsPath;
        set => SetProperty(ref _sealedSecretsPath, value);
    }

    public string DmcpPassword
    {
        get => _dmcpPassword;
        set => SetProperty(ref _dmcpPassword, value);
    }

    public string AccessKeyId
    {
        get => _accessKeyId;
        set => SetProperty(ref _accessKeyId, value);
    }

    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set => SetProperty(ref _secretAccessKey, value);
    }

    public bool VaultUnlocked => _vaultSessionService.IsUnlocked;

    public string VaultHint => VaultUnlocked
        ? HasStoredDmcpPassword
            ? "D-MCP password сохранён в Hub. Оставьте поле пустым, чтобы не менять."
            : "Укажите D-MCP password для managed-режима."
        : "Разблокируйте хранилище Hub для сохранения.";

    public string HubSettingsHint =>
        "Сохраняет pairing, профиль bucket и D-MCP password в SQLite Hub. " +
        "Агент получает databaseid через resolve_infobase_context. D-MCP CLI не вызывается.";

    public string PortableSyncHint =>
        "Записывает настройки Hub на portable через D-MCP CLI: apply-registry (всегда); " +
        "apply-secrets — если указаны S3 keys ниже. Перед синхронизацией сохраняет текущие поля в Hub.";

    public string SealedCredentialsHint
    {
        get
        {
            if (HasSealedCredentialsFile)
            {
                var path = string.IsNullOrWhiteSpace(PortableCredentialsPath)
                    ? "sealed-файл"
                    : PortableCredentialsPath;
                return $"S3-ключи записаны на portable ({path}). Hub их не хранит — поля пустые намеренно. Введите новые значения только для замены.";
            }

            return "Ключи S3 будут записаны в sealed-файл на portable (не в SQLite Hub).";
        }
    }

    public string S3AccessKeyPlaceholder => HasSealedCredentialsFile
        ? "сохранено на portable"
        : string.Empty;

    public string S3SecretKeyPlaceholder => HasSealedCredentialsFile
        ? "сохранено на portable"
        : string.Empty;

    public string? PortableCredentialsPath
    {
        get => _portableCredentialsPath;
        private set => SetProperty(ref _portableCredentialsPath, value);
    }

    public bool HasStoredDmcpPassword
    {
        get => _hasStoredDmcpPassword;
        private set => SetProperty(ref _hasStoredDmcpPassword, value);
    }

    public bool HasSealedCredentialsFile
    {
        get => _hasSealedCredentialsFile;
        private set
        {
            if (Equals(_hasSealedCredentialsFile, value))
                return;

            _hasSealedCredentialsFile = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SealedCredentialsHint));
            RaisePropertyChanged(nameof(S3AccessKeyPlaceholder));
            RaisePropertyChanged(nameof(S3SecretKeyPlaceholder));
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SavePathCommand { get; }
    public RelayCommand OpenPortableCommand { get; }
    public RelayCommand SaveHubCommand { get; }
    public RelayCommand SyncPortableCommand { get; }

    public async Task RefreshOnNavigateAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            var loaded = await _settingsService.LoadAsync();
            _toolInstanceId = loaded.ToolInstanceId;
            RootPath = loaded.RootPath;
            Endpoint = loaded.Endpoint;
            Region = loaded.Region;
            Bucket = loaded.Bucket;
            DefaultPrefix = loaded.DefaultPrefix;
            SealedSecretsPath = loaded.SealedSecretsPath;
            HasStoredDmcpPassword = loaded.HasStoredDmcpPassword;
            HasSealedCredentialsFile = loaded.HasSealedCredentialsFile;
            PortableCredentialsPath = loaded.PortableCredentialsPath;
            DmcpPassword = string.Empty;
            AccessKeyId = string.Empty;
            SecretAccessKey = string.Empty;

            Connections.Clear();
            foreach (var item in loaded.Connections)
            {
                Connections.Add(new DataMcpConnectionRow(item));
            }

            RaisePropertyChanged(nameof(VaultUnlocked));
            RaisePropertyChanged(nameof(VaultHint));

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _activityLog.LogError("data-mcp", "refresh", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshStatusAsync()
    {
        if (!Directory.Exists(RootPath))
        {
            StatusSummary = "Portable каталог не найден.";
            McpReadiness = string.Empty;
            return;
        }

        try
        {
            var inventory = await _toolClient.GetInventoryAsync();
            var status = await _toolClient.GetStatusAsync();
            StatusSummary = $"{inventory.ModuleId} v{inventory.ModuleVersion}; CLI: {(inventory.CliSupport ? "да" : "нет")}";
            McpReadiness = $"{status.Status} / {status.Readiness}; {status.Summary}";
            if (status.Details?.CredentialsExists == true)
            {
                HasSealedCredentialsFile = true;
                if (string.IsNullOrWhiteSpace(PortableCredentialsPath))
                {
                    var loaded = await _settingsService.LoadAsync();
                    PortableCredentialsPath = loaded.PortableCredentialsPath;
                }
            }

            if (status.Details?.Bucket is { Length: > 0 } portableBucket && string.IsNullOrWhiteSpace(Bucket))
                Bucket = portableBucket;
        }
        catch (Exception ex)
        {
            StatusSummary = "CLI недоступен.";
            McpReadiness = ex.Message;
        }
    }

    private async Task SavePathAsync()
    {
        try
        {
            IsBusy = true;
            await _settingsService.SaveRootPathAsync(RootPath);
            StatusMessage = "Путь к portable сохранён.";
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveHubAsync()
    {
        if (!CanSaveHub())
        {
            StatusMessage = "Разблокируйте хранилище Hub для сохранения.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            await PersistHubSettingsAsync(clearEnteredDmcpPassword: true);

            StatusMessage = "Настройки сохранены в Hub (pairing, bucket, пароль). Portable не изменён.";
            _activityLog.LogInfo("data-mcp", "save-hub", "Hub settings saved without portable sync");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _activityLog.LogError("data-mcp", "save-hub", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncPortableAsync()
    {
        if (!CanSyncPortable())
        {
            StatusMessage = "Разблокируйте хранилище Hub и укажите путь к portable.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            var accessKeyId = string.IsNullOrWhiteSpace(AccessKeyId) ? null : AccessKeyId.Trim();
            var secretAccessKey = string.IsNullOrEmpty(SecretAccessKey) ? null : SecretAccessKey;
            var dmcpPassword = string.IsNullOrEmpty(DmcpPassword) ? null : DmcpPassword;

            await PersistHubSettingsAsync(clearEnteredDmcpPassword: false);

            var syncResult = await _syncService.SyncPortableAsync(new DataMcpPortableSyncOptions
            {
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                DmcpPassword = dmcpPassword
            });

            DmcpPassword = string.Empty;
            AccessKeyId = string.Empty;
            SecretAccessKey = string.Empty;
            if (!string.IsNullOrEmpty(dmcpPassword))
                HasStoredDmcpPassword = true;
            if (syncResult.SecretsApplied)
                HasSealedCredentialsFile = true;

            var loaded = await _settingsService.LoadAsync();
            HasSealedCredentialsFile = loaded.HasSealedCredentialsFile || syncResult.SecretsApplied;
            PortableCredentialsPath = loaded.PortableCredentialsPath
                                      ?? (syncResult.SecretsApplied ? SealedSecretsPath : null);
            RaisePropertyChanged(nameof(VaultHint));

            Connections.Clear();
            foreach (var item in loaded.Connections)
                Connections.Add(new DataMcpConnectionRow(item));

            if (!syncResult.Success)
            {
                StatusMessage = $"Hub сохранён. {syncResult.Message}";
                _activityLog.LogError("data-mcp", "sync-portable", syncResult.Message);
                return;
            }

            StatusMessage = syncResult.Skipped
                ? $"Hub сохранён. {syncResult.Message}"
                : syncResult.Message;
            _activityLog.LogInfo(
                "data-mcp",
                "sync-portable",
                syncResult.Skipped ? "Hub saved; portable sync skipped" : "Hub + portable sync OK");
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _activityLog.LogError("data-mcp", "sync-portable", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PersistHubSettingsAsync(bool clearEnteredDmcpPassword)
    {
        var dmcpPassword = string.IsNullOrEmpty(DmcpPassword) ? null : DmcpPassword;

        if (!string.IsNullOrWhiteSpace(RootPath))
            await _settingsService.SaveRootPathAsync(RootPath);

        await _settingsService.SaveAsync(new DataMcpSettingsSaveRequest
        {
            ToolInstanceId = _toolInstanceId,
            Endpoint = Endpoint,
            Region = Region,
            Bucket = Bucket,
            DefaultPrefix = DefaultPrefix,
            SealedSecretsPath = SealedSecretsPath,
            DmcpPassword = dmcpPassword,
            Connections = Connections.Select(row => row.ToItem()).ToList()
        });

        if (clearEnteredDmcpPassword && !string.IsNullOrEmpty(dmcpPassword))
        {
            DmcpPassword = string.Empty;
            HasStoredDmcpPassword = true;
            RaisePropertyChanged(nameof(VaultHint));
        }

        var loaded = await _settingsService.LoadAsync();
        Connections.Clear();
        foreach (var item in loaded.Connections)
            Connections.Add(new DataMcpConnectionRow(item));
    }

    private bool CanSaveHub() => !IsBusy && _vaultSessionService.IsUnlocked;

    private bool CanSyncPortable() =>
        !IsBusy
        && _vaultSessionService.IsUnlocked
        && Directory.Exists(RootPath)
        && !string.IsNullOrWhiteSpace(Bucket);

    private void OpenPortableFolderImpl()
    {
        if (!Directory.Exists(RootPath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = RootPath,
            UseShellExecute = true
        });
    }
}

public sealed class DataMcpConnectionRow : ObservableObject
{
    public DataMcpConnectionRow(DataMcpConnectionItem item)
    {
        InfobaseId = item.InfobaseId;
        ClientName = item.ClientName;
        InfobaseName = item.InfobaseName;
        ConnectionId = item.ConnectionId;
        DatabaseId = item.DatabaseId;
        DisplayName = item.DisplayName;
    }

    public Guid InfobaseId { get; }
    public Guid? ConnectionId { get; }
    public string ClientName { get; }
    public string InfobaseName { get; }

    private string _databaseId = string.Empty;
    private string _displayName = string.Empty;

    public string DatabaseId
    {
        get => _databaseId;
        set
        {
            SetProperty(ref _databaseId, value);
            RaisePropertyChanged(nameof(IsPaired));
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public bool IsPaired => !string.IsNullOrWhiteSpace(DatabaseId);

    public DataMcpConnectionItem ToItem() => new()
    {
        InfobaseId = InfobaseId,
        ClientName = ClientName,
        InfobaseName = InfobaseName,
        ConnectionId = ConnectionId,
        DatabaseId = DatabaseId,
        DisplayName = DisplayName
    };
}
