using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ConfigMcpViewModel : ObservableObject
{
    private readonly ConfigMcpSyncService _syncService;
    private readonly ProfileService _profileService;
    private readonly INavigationService _navigationService;

    private string _rootPath = string.Empty;
    private string _statusSummary = string.Empty;
    private string _mcpReadiness = string.Empty;
    private string _statusMessage = string.Empty;
    private string _followUpText = string.Empty;
    private bool _isBusy;
    private ConfigMcpBaseLinkItem? _selectedBase;
    private McpProjectOption? _selectedMcpProject;

    public ConfigMcpViewModel(
        ConfigMcpSyncService syncService,
        ProfileService profileService,
        INavigationService navigationService)
    {
        _syncService = syncService;
        _profileService = profileService;
        _navigationService = navigationService;

        ConfigAdminBases = new ObservableCollection<ConfigMcpBaseLinkItem>();
        McpProjects = new ObservableCollection<McpProjectOption>();

        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        SavePathCommand = new RelayCommand(SavePathAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
        OpenPortableCommand = new RelayCommand(OpenPortableFolder, () => Directory.Exists(RootPath));
        LinkCommand = new RelayCommand(LinkSelectedAsync, CanLink);
        SyncSelectedCommand = new RelayCommand(SyncSelectedAsync, () => SelectedBase?.IsLinked == true && !IsBusy);
        SyncAllCommand = new RelayCommand(SyncAllAsync, () => ConfigAdminBases.Any(b => b.IsLinked) && !IsBusy);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public ObservableCollection<ConfigMcpBaseLinkItem> ConfigAdminBases { get; }
    public ObservableCollection<McpProjectOption> McpProjects { get; }

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

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ConfigMcpBaseLinkItem? SelectedBase
    {
        get => _selectedBase;
        set
        {
            SetProperty(ref _selectedBase, value);
            SelectedMcpProject = value?.LinkedMcpProject;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public McpProjectOption? SelectedMcpProject
    {
        get => _selectedMcpProject;
        set
        {
            SetProperty(ref _selectedMcpProject, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SavePathCommand { get; }
    public RelayCommand OpenPortableCommand { get; }
    public RelayCommand LinkCommand { get; }
    public RelayCommand SyncSelectedCommand { get; }
    public RelayCommand SyncAllCommand { get; }
    public RelayCommand BackCommand { get; }

    public async Task InitializeAsync()
    {
        var tool = await _syncService.EnsureConfigMcpToolAsync();
        RootPath = tool.RootPath;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        FollowUpText = string.Empty;

        try
        {
            await LoadBasesAsync();

            var status = await _syncService.GetStatusAsync();
            StatusSummary = status.Summary ?? $"{status.Projects.Count} project(s)";
            McpReadiness = $"{status.Status} / {status.Readiness}";

            McpProjects.Clear();
            foreach (var project in status.Projects.OrderBy(p => p.Name))
            {
                McpProjects.Add(new McpProjectOption
                {
                    ProjectId = Guid.Parse(project.ProjectId),
                    Name = project.Name,
                    Active = project.Active,
                    DatabaseCount = project.Databases.Count
                });
            }

            foreach (var baseItem in ConfigAdminBases)
                baseItem.LinkedMcpProject = McpProjects.FirstOrDefault(p => p.ProjectId == baseItem.ConfigMcpProjectId);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            McpProjects.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBasesAsync()
    {
        var clients = await _profileService.GetClientsAsync();
        var clientMap = clients.ToDictionary(c => c.Id, c => c);
        var bases = await _profileService.GetInfobasesAsync();

        ConfigAdminBases.Clear();
        foreach (var profile in bases.OrderBy(b => b.Name))
        {
            clientMap.TryGetValue(profile.ClientId, out var client);
            ConfigAdminBases.Add(new ConfigMcpBaseLinkItem
            {
                InfobaseId = profile.Id,
                ClientName = client?.Name ?? "?",
                BaseName = profile.Name,
                ConfigMcpProjectId = profile.ConfigMcpProjectId
            });
        }
    }

    private async Task SavePathAsync()
    {
        IsBusy = true;
        try
        {
            await _syncService.SaveConfigMcpRootPathAsync(RootPath);
            StatusMessage = "Путь к portable сохранён.";
            await RefreshAsync();
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

    private void OpenPortableFolder()
    {
        if (!Directory.Exists(RootPath))
            return;

        Process.Start(new ProcessStartInfo(RootPath) { UseShellExecute = true });
    }

    private bool CanLink() =>
        SelectedBase is not null && SelectedMcpProject is not null && !IsBusy;

    private async Task LinkSelectedAsync()
    {
        if (SelectedBase is null || SelectedMcpProject is null)
            return;

        IsBusy = true;
        try
        {
            await _syncService.LinkInfobaseToMcpProjectAsync(
                SelectedBase.InfobaseId,
                SelectedMcpProject.ProjectId,
                SelectedMcpProject.Name);

            SelectedBase.ConfigMcpProjectId = SelectedMcpProject.ProjectId;
            SelectedBase.LinkedMcpProject = SelectedMcpProject;
            StatusMessage = $"База «{SelectedBase.DisplayName}» привязана к проекту «{SelectedMcpProject.Name}».";
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

    private async Task SyncSelectedAsync()
    {
        if (SelectedBase is null)
            return;

        await SyncInfobaseAsync(SelectedBase.InfobaseId);
    }

    private async Task SyncAllAsync()
    {
        IsBusy = true;
        FollowUpText = string.Empty;

        try
        {
            var linked = ConfigAdminBases.Where(b => b.IsLinked).ToList();
            var messages = new List<string>();
            var followUps = new List<string>();

            foreach (var item in linked)
            {
                var result = await _syncService.SyncInfobaseAsync(item.InfobaseId);
                messages.Add($"{item.DisplayName}: {(result.Success ? "OK" : result.Message)}");
                followUps.AddRange(result.FollowUpHints.Select(h => h.DisplayText));
            }

            StatusMessage = string.Join("; ", messages);
            FollowUpText = followUps.Count > 0
                ? "Рекомендуемые действия config-mcp:\n" + string.Join("\n", followUps.Distinct())
                : string.Empty;
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

    private async Task SyncInfobaseAsync(Guid infobaseId)
    {
        IsBusy = true;
        FollowUpText = string.Empty;

        try
        {
            var result = await _syncService.SyncInfobaseAsync(infobaseId);
            StatusMessage = result.Message;

            if (result.FollowUpHints.Count > 0)
            {
                FollowUpText = "Рекомендуемые действия config-mcp:\n" +
                               string.Join("\n", result.FollowUpHints.Select(h => h.DisplayText));
            }
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
}

public sealed class ConfigMcpBaseLinkItem : ObservableObject
{
    private McpProjectOption? _linkedMcpProject;

    public Guid InfobaseId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string BaseName { get; init; } = string.Empty;
    public Guid? ConfigMcpProjectId { get; set; }

    public string DisplayName => $"{ClientName} / {BaseName}";
    public bool IsLinked => ConfigMcpProjectId is Guid id && id != Guid.Empty;

    public string LinkStatus => IsLinked
        ? LinkedMcpProject?.Name ?? ConfigMcpProjectId?.ToString() ?? "?"
        : "не привязана";

    public McpProjectOption? LinkedMcpProject
    {
        get => _linkedMcpProject;
        set
        {
            SetProperty(ref _linkedMcpProject, value);
            RaisePropertyChanged(nameof(LinkStatus));
            RaisePropertyChanged(nameof(IsLinked));
        }
    }
}

public sealed class McpProjectOption
{
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Active { get; init; }
    public int DatabaseCount { get; init; }

    public string DisplayText => $"{Name} ({DatabaseCount} баз)";
}
