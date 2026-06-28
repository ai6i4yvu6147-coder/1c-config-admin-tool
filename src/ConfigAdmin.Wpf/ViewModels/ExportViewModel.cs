using System.Windows.Input;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ExportViewModel : ObservableObject
{
    private readonly ExportService _exportService;
    private readonly ProfileService _profileService;
    private readonly ConfigMcpSyncService _configMcpSyncService;
    private readonly INavigationService _navigationService;

    private Guid _baseId;
    private string _baseDisplayName = string.Empty;
    private bool _exportConfiguration;
    private bool _exportAllExtensions;
    private string _selectedExtensionsText = string.Empty;
    private bool _saveSettingsToProfile = true;
    private bool _syncToMcpAfterExport = true;
    private string _mcpFollowUpText = string.Empty;
    private string _progressText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public ExportViewModel(
        ExportService exportService,
        ProfileService profileService,
        ConfigMcpSyncService configMcpSyncService,
        INavigationService navigationService)
    {
        _exportService = exportService;
        _profileService = profileService;
        _configMcpSyncService = configMcpSyncService;
        _navigationService = navigationService;

        ExportCommand = new RelayCommand(ExportAsync, CanExport);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public string BaseDisplayName
    {
        get => _baseDisplayName;
        private set => SetProperty(ref _baseDisplayName, value);
    }

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

    public bool IsSelectedExtensionsEnabled => !ExportAllExtensions;

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
    public RelayCommand BackCommand { get; }

    public async void Begin(Guid baseId, string displayName)
    {
        _baseId = baseId;
        BaseDisplayName = displayName;
        ProgressText = string.Empty;
        StatusMessage = string.Empty;
        McpFollowUpText = string.Empty;

        var profile = await _profileService.GetInfobaseByIdAsync(baseId);
        if (profile is null)
        {
            StatusMessage = "База не найдена.";
            IsMcpSyncAvailable = false;
            RaisePropertyChanged(nameof(IsMcpSyncAvailable));
            return;
        }

        IsMcpSyncAvailable = profile.ConfigMcpProjectId is Guid id && id != Guid.Empty;
        SyncToMcpAfterExport = IsMcpSyncAvailable;
        RaisePropertyChanged(nameof(IsMcpSyncAvailable));

        ExportConfiguration = profile.ExportConfiguration;
        ExportAllExtensions = profile.ExportAllExtensions;
        SelectedExtensionsText = string.Join(Environment.NewLine, profile.SelectedExtensions);
    }

    private bool CanExport() => !IsBusy && BuildPlan().HasWork;

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
            profile.ExportFormat);
    }

    private async Task TrySyncToMcpAsync()
    {
        try
        {
            var syncResult = await _configMcpSyncService.SyncInfobaseAsync(_baseId);
            if (syncResult.Success)
            {
                StatusMessage += " Синхронизация с config-mcp выполнена.";
            }
            else
            {
                StatusMessage += $" Синхронизация с config-mcp не выполнена: {syncResult.Message}";
            }

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
