using System.Collections.ObjectModel;
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
    private readonly InfobaseConfigurationService _configurationService;
    private readonly ConfigMcpSyncService _configMcpSyncService;
    private readonly RemoteSyncOrchestrator _remoteSyncOrchestrator;
    private readonly VaultSessionService _vaultSessionService;
    private readonly INavigationService _navigationService;

    private Guid _baseId;
    private string _baseDisplayName = string.Empty;
    private bool _isRemoteBase;
    private bool _syncToMcpAfterExport = true;
    private string _mcpFollowUpText = string.Empty;
    private string _progressText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private readonly List<Guid> _activeJobIds = [];

    public ExportViewModel(
        ExportService exportService,
        ProfileService profileService,
        InfobaseConfigurationService configurationService,
        ConfigMcpSyncService configMcpSyncService,
        RemoteSyncOrchestrator remoteSyncOrchestrator,
        VaultSessionService vaultSessionService,
        INavigationService navigationService)
    {
        _exportService = exportService;
        _profileService = profileService;
        _configurationService = configurationService;
        _configMcpSyncService = configMcpSyncService;
        _remoteSyncOrchestrator = remoteSyncOrchestrator;
        _vaultSessionService = vaultSessionService;
        _navigationService = navigationService;

        ExportPlanItems = new ObservableCollection<ExportPlanListItem>();

        ExportCommand = new RelayCommand(ExportAsync, CanExport);
        RemoteSyncCommand = new RelayCommand(RemoteSyncAsync, CanRemoteSync);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public ObservableCollection<ExportPlanListItem> ExportPlanItems { get; }

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
        _activeJobIds.Clear();
        ExportPlanItems.Clear();

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
        var instances = await _configurationService.GetInstancesAsync(baseId);
        IsMcpSyncAvailable = instances.Any(i => i.IsMcpLinked && i.ExportEnabled);
        SyncToMcpAfterExport = IsMcpSyncAvailable;
        RaisePropertyChanged(nameof(IsMcpSyncAvailable));

        var plan = await _configurationService.BuildExportPlanAsync(baseId);
        foreach (var item in plan.Instances)
        {
            ExportPlanItems.Add(new ExportPlanListItem
            {
                DisplayName = item.DisplayName,
                KindLabel = item.Kind == ConfigurationKind.Base ? "основная" : "расширение",
                DesignerName = item.DesignerName ?? "—"
            });
        }

        if (!plan.HasWork)
            StatusMessage = "План выгрузки пуст. Настройте конфигурации в карточке базы.";
    }

    private bool CanExport() => !IsBusy && IsLocalBase && ExportPlanItems.Count > 0;

    private bool CanRemoteSync() =>
        !IsBusy && IsRemoteBase && ExportPlanItems.Count > 0 && _vaultSessionService.IsUnlocked;

    private async Task RemoteSyncAsync()
    {
        if (!_vaultSessionService.IsUnlocked)
        {
            StatusMessage = "Разблокируйте vault перед синхронизацией с RDP.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Создание задач sync…";
        ProgressText = string.Empty;

        try
        {
            var jobs = await _remoteSyncOrchestrator.RequestSyncAsync(
                _baseId,
                SyncToMcpAfterExport && IsMcpSyncAvailable);

            _activeJobIds.Clear();
            _activeJobIds.AddRange(jobs.Select(j => j.Id));
            StatusMessage = $"Создано задач: {jobs.Count}. Ожидание Передатчика на RDP…";

            foreach (var job in jobs)
                await PollJobUntilDoneAsync(job.Id);

            StatusMessage = "Синхронизация с RDP завершена.";
            if (SyncToMcpAfterExport && IsMcpSyncAvailable)
                McpFollowUpText = "MCP sync выполнен на Hub после доставки (если receiver был активен).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _activeJobIds.Clear();
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
                return;

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
        IsBusy = true;
        StatusMessage = "Запуск выгрузки...";
        ProgressText = string.Empty;

        try
        {
            var progress = new Progress<ExportProgress>(p =>
            {
                ProgressText = string.IsNullOrWhiteSpace(p.Detail)
                    ? $"{p.Stage} ({p.CompletedSteps}/{p.TotalSteps})"
                    : $"{p.Stage}: {p.Detail} ({p.CompletedSteps}/{p.TotalSteps})";
            });

            var result = await _exportService.ExportByIdAsync(_baseId, planOverride: null, progress);

            if (result.Success)
            {
                var details = result.ExportedExtensions.Count > 0
                    ? $"расширения: {string.Join(", ", result.ExportedExtensions)}"
                    : "конфигурации из плана";
                StatusMessage = $"Выгрузка завершена за {result.Duration:g}. {details}";

                if (SyncToMcpAfterExport && IsMcpSyncAvailable)
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

    private async Task TrySyncToMcpAsync()
    {
        try
        {
            var syncResult = await _configMcpSyncService.SyncInfobaseAsync(_baseId);
            if (syncResult.Success)
            {
                var indexHint = syncResult.IndexRebuildsSucceeded > 0
                    ? $" Индекс MCP: {syncResult.IndexRebuildsSucceeded} database."
                    : string.Empty;
                StatusMessage += $" Синхронизация с config-mcp выполнена.{indexHint}";
            }
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

public sealed class ExportPlanListItem
{
    public string DisplayName { get; init; } = string.Empty;
    public string KindLabel { get; init; } = string.Empty;
    public string DesignerName { get; init; } = string.Empty;
}
