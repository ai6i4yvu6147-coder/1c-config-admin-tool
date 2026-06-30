using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ConfigMcpViewModel : ObservableObject, IRefreshOnNavigate
{
    private readonly ConfigMcpSyncService _syncService;
    private readonly ProfileService _profileService;
    private readonly InfobaseConfigurationService _configurationService;
    private readonly INavigationService _navigationService;
    private readonly UiActivityLog _activityLog;
    private readonly LogsViewModel _logsViewModel;

    private string _rootPath = string.Empty;
    private string _statusSummary = string.Empty;
    private string _mcpReadiness = string.Empty;
    private string _statusMessage = string.Empty;
    private string _followUpText = string.Empty;
    private string _registryFilePath = string.Empty;
    private bool _isBusy;
    private ConfigMcpInstanceLinkItem? _selectedInstance;
    private McpProjectOption? _selectedMcpProject;
    private McpDatabaseOption? _selectedMcpDatabase;

    public ConfigMcpViewModel(
        ConfigMcpSyncService syncService,
        ProfileService profileService,
        InfobaseConfigurationService configurationService,
        INavigationService navigationService,
        UiActivityLog activityLog,
        LogsViewModel logsViewModel)
    {
        _syncService = syncService;
        _profileService = profileService;
        _configurationService = configurationService;
        _navigationService = navigationService;
        _activityLog = activityLog;
        _logsViewModel = logsViewModel;

        ConfigInstances = new ObservableCollection<ConfigMcpInstanceLinkItem>();
        McpProjects = new ObservableCollection<McpProjectOption>();
        McpDatabases = new ObservableCollection<McpDatabaseOption>();

        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        SavePathCommand = new RelayCommand(SavePathAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
        OpenPortableCommand = new RelayCommand(OpenPortableFolder, () => Directory.Exists(RootPath));
        LinkCommand = new RelayCommand(LinkSelectedAsync, CanLink);
        SyncSelectedCommand = new RelayCommand(SyncSelectedAsync, () => SelectedInstance?.IsLinked == true && !IsBusy);
        SyncAllCommand = new RelayCommand(SyncAllAsync, () => ConfigInstances.Any(b => b.IsLinked) && !IsBusy);
        OpenJournalCommand = new RelayCommand(OpenJournal);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public ObservableCollection<ConfigMcpInstanceLinkItem> ConfigInstances { get; }
    public ObservableCollection<McpProjectOption> McpProjects { get; }
    public ObservableCollection<McpDatabaseOption> McpDatabases { get; }

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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FollowUpText
    {
        get => _followUpText;
        set => SetProperty(ref _followUpText, value);
    }

    public string RegistryFilePath
    {
        get => _registryFilePath;
        set => SetProperty(ref _registryFilePath, value);
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

    public ConfigMcpInstanceLinkItem? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            SetProperty(ref _selectedInstance, value);
            if (value is not null)
                ApplySelectionDefaults(value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public McpProjectOption? SelectedMcpProject
    {
        get => _selectedMcpProject;
        set
        {
            if (EqualityComparer<McpProjectOption?>.Default.Equals(_selectedMcpProject, value))
                return;

            SetProperty(ref _selectedMcpProject, value);
            ReloadMcpDatabases(preserveDatabaseSelection: false);
            if (SelectedMcpDatabase is null && McpDatabases.Count > 0)
                SelectedMcpDatabase = McpDatabases[0];

            CommandManager.InvalidateRequerySuggested();
        }
    }

    public McpDatabaseOption? SelectedMcpDatabase
    {
        get => _selectedMcpDatabase;
        set
        {
            if (EqualityComparer<McpDatabaseOption?>.Default.Equals(_selectedMcpDatabase, value))
                return;

            SetProperty(ref _selectedMcpDatabase, value);

            if (value is { IsCreateNew: false })
            {
                var project = McpProjects.FirstOrDefault(p => p.ProjectId == value.ProjectId && !p.IsCreateNew);
                if (project is not null && !EqualityComparer<McpProjectOption?>.Default.Equals(_selectedMcpProject, project))
                {
                    _selectedMcpProject = project;
                    RaisePropertyChanged(nameof(SelectedMcpProject));
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SavePathCommand { get; }
    public RelayCommand OpenPortableCommand { get; }
    public RelayCommand LinkCommand { get; }
    public RelayCommand SyncSelectedCommand { get; }
    public RelayCommand SyncAllCommand { get; }
    public RelayCommand OpenJournalCommand { get; }
    public RelayCommand BackCommand { get; }

    public Task RefreshOnNavigateAsync() => RefreshAsync();

    public async Task InitializeAsync()
    {
        var tool = await _syncService.EnsureConfigMcpToolAsync();
        RootPath = tool.RootPath;
        RegistryFilePath = ManagedToolRegistryService.ResolveProjectsJsonPath(tool.RootPath);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        FollowUpText = string.Empty;

        try
        {
            await _syncService.MigrateLegacyInfobaseLinksAsync();
            var selectedInstanceId = SelectedInstance?.InstanceId;
            await LoadInstancesAsync();

            var status = await _syncService.GetStatusAsync();
            StatusSummary = status.Summary ?? $"{status.Projects.Count} project(s)";
            McpReadiness = $"{status.Status} / {status.Readiness}";
            RegistryFilePath = ManagedToolRegistryService.ResolveProjectsJsonPath(RootPath);

            McpProjects.Clear();
            McpProjects.Add(McpProjectOption.CreateNew());
            foreach (var project in status.Projects.OrderBy(p => p.Name))
            {
                if (!Guid.TryParse(project.ProjectId, out var projectId))
                    continue;

                McpProjects.Add(new McpProjectOption
                {
                    ProjectId = projectId,
                    Name = project.Name,
                    Active = project.Active,
                    DatabaseCount = project.Databases.Count,
                    Databases = project.Databases
                });
            }

            foreach (var item in ConfigInstances)
                item.RefreshLinkLabels(McpProjects);

            if (selectedInstanceId is Guid instanceId)
                SelectedInstance = ConfigInstances.FirstOrDefault(i => i.InstanceId == instanceId);
            else if (SelectedInstance is not null)
                ApplySelectionDefaults(SelectedInstance);
        }
        catch (Exception ex)
        {
            UiStatusReporter.ReportException(_activityLog, "MCP", ex, m => StatusMessage = m);
            McpProjects.Clear();
            McpDatabases.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadInstancesAsync()
    {
        var clients = await _profileService.GetClientsAsync();
        var clientMap = clients.ToDictionary(c => c.Id, c => c);
        var bases = await _profileService.GetInfobasesAsync();

        ConfigInstances.Clear();
        foreach (var infobase in bases.OrderBy(b => b.Name))
        {
            clientMap.TryGetValue(infobase.ClientId, out var client);
            await _configurationService.EnsureBaseInstanceAsync(infobase.Id);
            var instances = await _configurationService.GetInstancesAsync(infobase.Id);

            foreach (var instance in instances.OrderBy(i => i.SortOrder).ThenBy(i => i.DisplayName))
            {
                ConfigInstances.Add(new ConfigMcpInstanceLinkItem
                {
                    InstanceId = instance.Id,
                    InfobaseId = infobase.Id,
                    ClientId = infobase.ClientId,
                    ClientName = client?.Name ?? "?",
                    BaseName = infobase.Name,
                    DisplayName = instance.DisplayName,
                    KindLabel = instance.Kind == ConfigurationKind.Base ? "основная" : "расширение",
                    ConfigMcpProjectId = instance.ConfigMcpProjectId,
                    ConfigMcpDatabaseId = instance.ConfigMcpDatabaseId
                });
            }
        }
    }

    private void ApplySelectionDefaults(ConfigMcpInstanceLinkItem item)
    {
        var projectInMcp = item.ConfigMcpProjectId is Guid projectId
            ? McpProjects.FirstOrDefault(p => p.ProjectId == projectId && !p.IsCreateNew)
            : null;

        if (projectInMcp is not null)
            SelectedMcpProject = projectInMcp;
        else
            SelectedMcpProject = McpProjects.FirstOrDefault(p => p.IsCreateNew) ?? McpProjects.FirstOrDefault();

        ReloadMcpDatabases(preserveDatabaseSelection: false);

        if (item.ConfigMcpDatabaseId is Guid databaseId)
        {
            var existingDatabase = McpDatabases.FirstOrDefault(d => d.DatabaseId == databaseId);
            if (existingDatabase is not null)
                SelectedMcpDatabase = existingDatabase;
            else
                SelectedMcpDatabase = McpDatabases.FirstOrDefault(d => d.IsCreateNew) ?? McpDatabases.FirstOrDefault();
        }
        else
        {
            SelectedMcpDatabase = McpDatabases.FirstOrDefault(d => d.IsCreateNew) ?? McpDatabases.FirstOrDefault();
        }
    }

    private void ReloadMcpDatabases(bool preserveDatabaseSelection)
    {
        var preserveId = preserveDatabaseSelection ? SelectedMcpDatabase?.DatabaseId : null;
        McpDatabases.Clear();

        var project = SelectedMcpProject;
        if (project is null)
            return;

        if (!project.IsCreateNew)
        {
            McpDatabases.Add(McpDatabaseOption.CreateNew(project));
            foreach (var db in project.Databases.OrderBy(d => d.Name))
            {
                if (!Guid.TryParse(db.InfobaseId, out var databaseId))
                    continue;

                McpDatabases.Add(new McpDatabaseOption
                {
                    ProjectId = project.ProjectId,
                    ProjectName = project.Name,
                    DatabaseId = databaseId,
                    Name = db.Name,
                    Type = db.Type,
                    SourcePathExists = db.SourcePathExists ?? false,
                    IsOutdated = db.IsOutdated
                });
            }
        }
        else
        {
            McpDatabases.Add(McpDatabaseOption.CreateNew(project));
        }

        if (preserveId is Guid id)
        {
            var restored = McpDatabases.FirstOrDefault(d => d.DatabaseId == id);
            if (restored is not null)
                SelectedMcpDatabase = restored;
        }
    }

    private ConfigMcpLinkSelection BuildLinkSelection()
    {
        var project = SelectedMcpProject ?? throw new InvalidOperationException("Выберите проект MCP.");
        var database = SelectedMcpDatabase ?? throw new InvalidOperationException("Выберите database MCP.");

        if (project.IsCreateNew)
        {
            return new ConfigMcpLinkSelection { CreateNewProject = true, CreateNewDatabase = true };
        }

        if (database.IsCreateNew)
        {
            return new ConfigMcpLinkSelection
            {
                CreateNewProject = false,
                CreateNewDatabase = true,
                ProjectId = project.ProjectId,
                ProjectName = project.Name
            };
        }

        return new ConfigMcpLinkSelection
        {
            CreateNewProject = false,
            CreateNewDatabase = false,
            ProjectId = database.ProjectId,
            ProjectName = database.ProjectName,
            DatabaseId = database.DatabaseId
        };
    }

    private async Task SavePathAsync()
    {
        IsBusy = true;
        try
        {
            await _syncService.SaveConfigMcpRootPathAsync(RootPath);
            RegistryFilePath = ManagedToolRegistryService.ResolveProjectsJsonPath(RootPath);
            StatusMessage = "Путь к portable сохранён.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UiStatusReporter.ReportException(_activityLog, "MCP", ex, m => StatusMessage = m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenPortableFolder()
    {
        if (!Directory.Exists(RootPath))
            return;

        Process.Start(new ProcessStartInfo(RootPath) { UseShellExecute = true });
    }

    private bool CanLink() =>
        SelectedInstance is not null &&
        SelectedMcpProject is not null &&
        SelectedMcpDatabase is not null &&
        !IsBusy;

    private async Task LinkSelectedAsync()
    {
        if (SelectedInstance is null)
            return;

        IsBusy = true;
        FollowUpText = string.Empty;
        try
        {
            var selection = BuildLinkSelection();
            var syncResult = await _syncService.LinkAndSyncInstanceAsync(
                SelectedInstance.InstanceId,
                selection,
                SelectedInstance.DefaultNewProjectName);

            await RefreshAsync();

            if (syncResult.Success)
            {
                var message = $"«{SelectedInstance?.DisplayLabel}»: {syncResult.Message}";
                UiStatusReporter.Report(_activityLog, "MCP", message, m => StatusMessage = m, isError: false);
                if (syncResult.FollowUpHints.Count > 0)
                {
                    FollowUpText = "Рекомендуемые действия config-mcp:\n" +
                                   string.Join("\n", syncResult.FollowUpHints.Select(h => h.DisplayText));
                }
            }
            else
            {
                var detail = BuildSyncErrorDetail(syncResult);
                UiStatusReporter.Report(
                    _activityLog,
                    "MCP",
                    $"Ошибка записи в config-mcp: {syncResult.Message}",
                    m => StatusMessage = m,
                    isError: true,
                    detail);
            }
        }
        catch (Exception ex)
        {
            UiStatusReporter.ReportException(_activityLog, "MCP", ex, m => StatusMessage = m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncSelectedAsync()
    {
        if (SelectedInstance is null)
            return;

        await SyncInstanceAsync(SelectedInstance.InstanceId);
    }

    private async Task SyncAllAsync()
    {
        IsBusy = true;
        FollowUpText = string.Empty;

        try
        {
            var result = await _syncService.SyncAllLinkedInstancesAsync();
            if (result.Success)
                UiStatusReporter.Report(_activityLog, "MCP", result.Message, m => StatusMessage = m, isError: false);
            else
                UiStatusReporter.Report(
                    _activityLog, "MCP", result.Message, m => StatusMessage = m, isError: true,
                    BuildSyncErrorDetail(result));

            if (result.FollowUpHints.Count > 0)
            {
                FollowUpText = "Рекомендуемые действия config-mcp:\n" +
                               string.Join("\n", result.FollowUpHints.Select(h => h.DisplayText));
            }
        }
        catch (Exception ex)
        {
            UiStatusReporter.ReportException(_activityLog, "MCP", ex, m => StatusMessage = m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncInstanceAsync(Guid instanceId)
    {
        IsBusy = true;
        FollowUpText = string.Empty;

        try
        {
            await _configurationService.GetOrCreateCurrentExportAsync(instanceId);
            var result = await _syncService.SyncInstanceAsync(instanceId);
            if (result.Success)
                UiStatusReporter.Report(_activityLog, "MCP", result.Message, m => StatusMessage = m, isError: false);
            else
                UiStatusReporter.Report(
                    _activityLog, "MCP", result.Message, m => StatusMessage = m, isError: true,
                    BuildSyncErrorDetail(result));

            if (result.FollowUpHints.Count > 0)
            {
                FollowUpText = "Рекомендуемые действия config-mcp:\n" +
                               string.Join("\n", result.FollowUpHints.Select(h => h.DisplayText));
            }
        }
        catch (Exception ex)
        {
            UiStatusReporter.ReportException(_activityLog, "MCP", ex, m => StatusMessage = m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenJournal()
    {
        _logsViewModel.ShowAppEventsTab();
        _navigationService.NavigateTo(_logsViewModel);
    }

    private static string BuildSyncErrorDetail(ConfigMcpSyncResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.RegistryPath))
            parts.Add($"Registry: {result.RegistryPath}");
        if (result.Errors.Count > 0)
            parts.Add(string.Join(Environment.NewLine, result.Errors));
        if (result.Warnings.Count > 0)
            parts.Add("Warnings: " + string.Join("; ", result.Warnings));
        parts.Add($"Changes: created={result.ChangesCreated}, updated={result.ChangesUpdated}, skipped={result.ChangesSkipped}");
        return string.Join(Environment.NewLine, parts);
    }
}

public sealed class ConfigMcpInstanceLinkItem : ObservableObject
{
    public Guid InstanceId { get; init; }
    public Guid InfobaseId { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string BaseName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string KindLabel { get; init; } = string.Empty;
    public Guid? ConfigMcpProjectId { get; set; }
    public Guid? ConfigMcpDatabaseId { get; set; }

    public string DisplayLabel => $"{ClientName} / {BaseName} / {DisplayName}";
    public string DefaultNewProjectName => $"{ClientName} / {BaseName}";
    public bool IsLinked => ConfigMcpProjectId is Guid id && id != Guid.Empty;

    private string _linkStatus = "не привязан";
    public string LinkStatus
    {
        get => _linkStatus;
        private set => SetProperty(ref _linkStatus, value);
    }

    public void RefreshLinkLabels(IEnumerable<McpProjectOption> projects)
    {
        if (!IsLinked)
        {
            LinkStatus = "не привязан";
            return;
        }

        var project = projects.FirstOrDefault(p => p.ProjectId == ConfigMcpProjectId && !p.IsCreateNew);
        if (project is null)
        {
            LinkStatus = $"только в Hub / {ConfigMcpProjectId}";
            return;
        }

        var db = project.Databases.FirstOrDefault(d =>
            Guid.TryParse(d.InfobaseId, out var id) && id == ConfigMcpDatabaseId);

        LinkStatus = db is not null
            ? $"{project.Name} / {db.Name}"
            : ConfigMcpDatabaseId is Guid dbId && dbId != Guid.Empty
                ? $"{project.Name} / нет в MCP ({dbId:N}…)"
                : $"{project.Name} / database не задана";
    }
}

public sealed class McpProjectOption
{
    public bool IsCreateNew { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Active { get; init; }
    public int DatabaseCount { get; init; }
    public List<ConfigMcpStatusDatabaseDto> Databases { get; init; } = [];

    public static McpProjectOption CreateNew() => new()
    {
        IsCreateNew = true,
        Name = "— Создать новый проект —"
    };
}

public sealed class McpDatabaseOption
{
    public bool IsCreateNew { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public Guid DatabaseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool SourcePathExists { get; init; }
    public bool IsOutdated { get; init; }

    public string DisplayLabel => IsCreateNew ? Name : $"{ProjectName} / {Name}";

    public string StatusLabel => IsCreateNew
        ? "новая запись в projects.json"
        : $"{Type}, path={(SourcePathExists ? "ok" : "missing")}, outdated={IsOutdated}";

    public static McpDatabaseOption CreateNew(McpProjectOption project) => new()
    {
        IsCreateNew = true,
        ProjectId = project.IsCreateNew ? Guid.Empty : project.ProjectId,
        ProjectName = project.IsCreateNew ? string.Empty : project.Name,
        Name = "— Создать новую database —"
    };
}
