using System.Windows.Input;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ExportViewModel : ObservableObject
{
    private readonly ExportService _exportService;
    private readonly ProfileService _profileService;
    private readonly ConfigMcpSyncService _configMcpSyncService;
    private readonly RemoteSyncOrchestrator _remoteSyncOrchestrator;
    private readonly VaultSessionService _vaultSessionService;
    private readonly INavigationService _navigationService;

    private Guid _baseId;
    private string _baseDisplayName = string.Empty;
    private bool _isRemoteBase;
    private bool _exportConfiguration = true;
    private bool _exportAllExtensions;
    private string _selectedExtensionsText = string.Empty;
    private bool _saveSettingsToProfile = true;
    private bool _syncToMcpAfterExport = true;
    private string _mcpFollowUpText = string.Empty;
    private string _progressText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private Guid? _activeJobId;

    public ExportViewModel(
        ExportService exportService,
        ProfileService profileService,
        ConfigMcpSyncService configMcpSyncService,
        RemoteSyncOrchestrator remoteSyncOrchestrator,
        VaultSessionService vaultSessionService,
        INavigationService navigationService)
    {
        _exportService = exportService;
        _profileService = profileService;
        _configMcpSyncService = configMcpSyncService;
        _remoteSyncOrchestrator = remoteSyncOrchestrator;
        _vaultSessionService = vaultSessionService;
        _navigationService = navigationService;

        ExportCommand = new RelayCommand(ExportAsync, CanExport);
        RemoteSyncCommand = new RelayCommand(RemoteSyncAsync, CanRemoteSync);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public string BaseDisplayName
    {
        get => _baseDisplayName;
        private set => SetProperty(ref _baseDisplayName, value);
    }

    public bool IsRemoteBase
    {
        get => _isRemoteBase;
        private set
        {
            SetProperty(ref _isRemoteBase, value);
            RaisePropertyChanged(nameof(IsLocalBase));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsLocalBase => !IsRemoteBase;

    public bool ExportConfiguration
    {
        get => _exportConfiguration;
        set => SetProperty(ref _exportConfiguration, value);
    }

    public bool ExportAllExtensions
    {
        get => _exportAllExtensions;
        set
        {
            SetProperty(ref _exportAllExtensions, value);
            RaisePropertyChanged(nameof(IsSelectedExtensionsEnabled));
        }
    }

    public string SelectedExtensionsText
    {
        get => _selectedExtensionsText;
        set => SetProperty(ref _selectedExtensionsText, value);
    }

    public bool IsSelectedExtensionsEnabled => !ExportAllExtensions && IsLocalBase;

    public bool SaveSettingsToProfile
    {
        get => _saveSettingsToProfile;
        set => SetProperty(ref _saveSettingsToProfile, value);
    }

    public bool SyncToMcpAfterExport
    {
        get => _syncToMcpAfterExport;
        set => SetProperty(ref _syncToMcpAfterExport, value);
    }

    public bool IsMcpSyncAvailable { get; private set; }

    public string McpFollowUpText
    {
        get => _mcpFollowUpText;
        set => SetProperty(ref _mcpFollowUpText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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

    public RelayCommand ExportCommand { get; }
    public RelayCommand RemoteSyncCommand { get; }
    public RelayCommand BackCommand { get; }

    public async void Begin(Guid baseId, string displayName)
    {
        _baseId = baseId;
        BaseDisplayName = displayName;
        ProgressText = string.Empty;
        StatusMessage = string.Empty;
        McpFollowUpText = string.Empty;
        _activeJobId = null;

        var profile = await _profileService.GetInfobaseByIdAsync(baseId);
        if (profile is null)
        {
            StatusMessage = "База не найдена.";
            IsMcpSyncAvailable = false;
            IsRemoteBase = false;
            RaisePropertyChanged(nameof(IsMcpSyncAvailable));
            return;
        }

        IsRemoteBase = profile.ExportLocation == ExportLocation.Remote;
        IsMcpSyncAvailable = profile.ConfigMcpProjectId is Guid id && id != Guid.Empty;
        SyncToMcpAfterExport = IsMcpSyncAvailable;
        RaisePropertyChanged(nameof(IsMcpSyncAvailable));
        RaisePropertyChanged(nameof(IsSelectedExtensionsEnabled));

        ExportConfiguration = profile.ExportConfiguration;
        ExportAllExtensions = profile.ExportAllExtensions;
        SelectedExtensionsText = string.Join(Environment.NewLine, profile.SelectedExtensions);
    }

    private bool CanExport() => !IsBusy && IsLocalBase && BuildPlan().HasWork;

    private bool CanRemoteSync() =>
        !IsBusy && IsRemoteBase && ExportConfiguration && _vaultSessionService.IsUnlocked;

    private ExportPlan BuildPlan()
    {
        var extensions = SelectedExtensionsText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new ExportPlan
        {
            ExportConfiguration = ExportConfiguration,
            ExportAllExtensions = ExportAllExtensions,
            SelectedExtensions = ExportAllExtensions ? [] : extensions
        };
    }

    private async Task RemoteSyncAsync()
    {
        if (!_vaultSessionService.IsUnlocked)
        {
            StatusMessage = "Разблокируйте vault перед синхронизацией с RDP.";
            return;
        }

        if (!ExportConfiguration)
        {
            StatusMessage = "Включите выгрузку основной конфигурации.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Создание задачи sync...";
        ProgressText = string.Empty;

        try
        {
            if (SaveSettingsToProfile)
                await SaveProfileSettingsAsync(BuildPlan());

            var job = await _remoteSyncOrchestrator.RequestSyncAsync(
                _baseId,
                SyncToMcpAfterExport && IsMcpSyncAvailable);
            _activeJobId = job.Id;
            StatusMessage = "Задача создана, ожидание Передатчика на RDP…";

            await PollJobUntilDoneAsync(job.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _activeJobId = null;
        }
    }

    private async Task PollJobUntilDoneAsync(Guid jobId)
    {
        while (true)
        {
            var job = await _remoteSyncOrchestrator.GetJobAsync(jobId);
            if (job is null)
            {
                StatusMessage = "Задача не найдена.";
                return;
            }

            var progress = _remoteSyncOrchestrator.GetJobProgress(jobId);
            StatusMessage = FormatRemoteSyncStatus(job, progress);
            ProgressText = FormatRemoteSyncProgress(job, progress);

            if (job.Status == SyncJobStatus.Completed)
            {
                StatusMessage = "Синхронизация с RDP завершена.";
                ProgressText = progress?.Message ?? ProgressText;
                if (job.SyncMcpAfterComplete && IsMcpSyncAvailable)
                    McpFollowUpText = "MCP sync выполнен на Hub после доставки (если receiver был активен).";
                return;
            }

            if (job.Status is SyncJobStatus.Failed or SyncJobStatus.Cancelled)
            {
                StatusMessage = job.ErrorMessage ?? $"Задача завершилась: {FormatStatusLabel(job.Status)}";
                return;
            }

            await Task.Delay(1500);
        }
    }

    private static string FormatRemoteSyncStatus(
        SyncJobProfile job,
        SyncJobProgressStore.JobProgressEntry? progress)
    {
        if (!string.IsNullOrWhiteSpace(progress?.Message))
            return progress.Message;

        return job.Status switch
        {
            SyncJobStatus.Pending => "Задача в очереди, ожидание опроса Передатчиком…",
            SyncJobStatus.Claimed => "Передатчик получил задачу, подготовка…",
            SyncJobStatus.Exporting => "Выгрузка конфигурации 1С на RDP (может занять много времени)…",
            SyncJobStatus.Uploading => "Передача zip на Hub…",
            SyncJobStatus.Applying => "Применение в локальный ExportRoot…",
            _ => FormatStatusLabel(job.Status)
        };
    }

    private static string FormatRemoteSyncProgress(
        SyncJobProfile job,
        SyncJobProgressStore.JobProgressEntry? progress)
    {
        var parts = new List<string> { FormatStatusLabel(job.Status) };

        if (job.StartedAt is not null)
        {
            var elapsed = DateTimeOffset.Now - job.StartedAt.Value.ToLocalTime();
            parts.Add($"прошло {FormatElapsed(elapsed)}");
        }

        if (progress is not null)
            parts.Add($"обновлено {progress.UpdatedAt.ToLocalTime():HH:mm:ss}");

        if (job.BytesTotal > 0)
        {
            var pct = job.BytesTotal == 0 ? 0 : (int)(job.BytesReceived * 100 / job.BytesTotal);
            parts.Add($"передано {FormatBytes(job.BytesReceived)}/{FormatBytes(job.BytesTotal)} ({pct}%)");
        }

        return string.Join(" · ", parts);
    }

    private static string FormatStatusLabel(SyncJobStatus status) => status switch
    {
        SyncJobStatus.Pending => "Ожидание",
        SyncJobStatus.Claimed => "Получено передатчиком",
        SyncJobStatus.Exporting => "Выгрузка 1С на RDP",
        SyncJobStatus.Uploading => "Загрузка на Hub",
        SyncJobStatus.Applying => "Применение",
        SyncJobStatus.Completed => "Завершено",
        SyncJobStatus.Failed => "Ошибка",
        SyncJobStatus.Cancelled => "Отменено",
        _ => status.ToString()
    };

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours} ч {elapsed.Minutes} мин";
        if (elapsed.TotalMinutes >= 1)
            return $"{(int)elapsed.TotalMinutes} мин";
        return $"{(int)elapsed.TotalSeconds} сек";
    }

    private static string FormatBytes(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };

    private async Task ExportAsync()
    {
        var plan = BuildPlan();
        if (!plan.HasWork)
        {
            StatusMessage = "Отметьте хотя бы один тип выгрузки.";
            return;
        }

        if (!ExportAllExtensions && plan.SelectedExtensions.Count == 0 && !ExportConfiguration)
        {
            StatusMessage = "Укажите имена расширений или включите «Все расширения».";
            return;
        }

        IsBusy = true;
        StatusMessage = "Запуск выгрузки...";
        ProgressText = string.Empty;

        try
        {
            if (SaveSettingsToProfile)
                await SaveProfileSettingsAsync(plan);

            var progress = new Progress<ExportProgress>(p =>
            {
                ProgressText = string.IsNullOrWhiteSpace(p.Detail)
                    ? $"{p.Stage} ({p.CompletedSteps}/{p.TotalSteps})"
                    : $"{p.Stage}: {p.Detail} ({p.CompletedSteps}/{p.TotalSteps})";
            });

            var result = await _exportService.ExportByIdAsync(_baseId, plan, progress);

            if (result.Success)
            {
                var details = new List<string>();
                if (plan.ExportConfiguration)
                    details.Add("конфигурация");
                if (result.ExportedExtensions.Count > 0)
                    details.Add($"расширения: {string.Join(", ", result.ExportedExtensions)}");
                else if (plan.ExportAllExtensions)
                    details.Add("все расширения");

                StatusMessage = $"Выгрузка завершена за {result.Duration:g}. {string.Join("; ", details)}";

                if (SyncToMcpAfterExport && IsMcpSyncAvailable && plan.ExportConfiguration)
                    await TrySyncToMcpAsync();
            }
            else
            {
                var stepErrors = string.Join("; ",
                    result.Steps.Where(s => !s.Success).Select(s => $"{s.StepName}: {s.ErrorMessage}"));
                StatusMessage = string.IsNullOrWhiteSpace(stepErrors)
                    ? $"Ошибка: {result.ErrorMessage}"
                    : stepErrors;
            }

            if (!string.IsNullOrWhiteSpace(result.OutputPath))
                ProgressText = $"Каталог: {result.OutputPath}";
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

    private async Task SaveProfileSettingsAsync(ExportPlan plan)
    {
        var profile = await _profileService.GetInfobaseByIdAsync(_baseId)
            ?? throw new InvalidOperationException("База не найдена.");

        var clients = await _profileService.GetClientsAsync();
        var client = clients.FirstOrDefault(c => c.Id == profile.ClientId)
            ?? throw new InvalidOperationException("Клиент не найден.");

        await _profileService.AddOrUpdateInfobaseAsync(
            client.Name,
            profile.Name,
            profile.PlatformPath,
            profile.ConnectionType,
            profile.ConnectionString,
            profile.Username,
            password: null,
            plan.ExportConfiguration,
            plan.ExportAllExtensions,
            plan.SelectedExtensions,
            profile.ExportFormat,
            profile.ExportLocation,
            profile.RemoteNodeId,
            profile.RemoteExportPath);
    }

    private async Task TrySyncToMcpAsync()
    {
        try
        {
            var syncResult = await _configMcpSyncService.SyncInfobaseAsync(_baseId);
            if (syncResult.Success)
                StatusMessage += " Синхронизация с config-mcp выполнена.";
            else
                StatusMessage += $" Синхронизация с config-mcp не выполнена: {syncResult.Message}";

            if (syncResult.FollowUpHints.Count > 0)
            {
                McpFollowUpText = "Рекомендуемые действия config-mcp:\n" +
                                  string.Join("\n", syncResult.FollowUpHints.Select(h => h.DisplayText));
            }
        }
        catch (Exception ex)
        {
            StatusMessage += $" Синхронизация с config-mcp: {ex.Message}";
        }
    }
}
