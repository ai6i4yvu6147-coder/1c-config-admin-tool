using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject, IRefreshOnNavigate
{
    private readonly ProfileService _profileService;
    private readonly InfobaseConfigurationService _configurationService;
    private readonly INavigationService _navigationService;
    private readonly VaultSessionService _vaultSessionService;
    private readonly VaultViewModel _vaultViewModel;
    private readonly ClientEditViewModel _clientEditViewModel;
    private readonly BaseEditViewModel _baseEditViewModel;
    private readonly ExportViewModel _exportViewModel;
    private readonly ConfigMcpViewModel _configMcpViewModel;
    private readonly RemoteNodesViewModel _remoteNodesViewModel;
    private readonly ConfigurationTemplatesViewModel _templatesViewModel;
    private readonly HubSettingsViewModel _hubSettingsViewModel;
    private readonly LogsViewModel _logsViewModel;
    private readonly ConfiguratorLaunchService _configuratorLaunchService;

    private ClientListItem? _selectedClient;
    private InfobaseListItem? _selectedBase;
    private string _statusMessage = string.Empty;

    public MainViewModel(
        ProfileService profileService,
        InfobaseConfigurationService configurationService,
        INavigationService navigationService,
        VaultSessionService vaultSessionService,
        VaultViewModel vaultViewModel,
        ClientEditViewModel clientEditViewModel,
        BaseEditViewModel baseEditViewModel,
        ExportViewModel exportViewModel,
        ConfigMcpViewModel configMcpViewModel,
        RemoteNodesViewModel remoteNodesViewModel,
        ConfigurationTemplatesViewModel templatesViewModel,
        HubSettingsViewModel hubSettingsViewModel,
        LogsViewModel logsViewModel,
        ConfiguratorLaunchService configuratorLaunchService)
    {
        _profileService = profileService;
        _configurationService = configurationService;
        _navigationService = navigationService;
        _vaultSessionService = vaultSessionService;
        _vaultViewModel = vaultViewModel;
        _clientEditViewModel = clientEditViewModel;
        _baseEditViewModel = baseEditViewModel;
        _exportViewModel = exportViewModel;
        _configMcpViewModel = configMcpViewModel;
        _remoteNodesViewModel = remoteNodesViewModel;
        _templatesViewModel = templatesViewModel;
        _hubSettingsViewModel = hubSettingsViewModel;
        _logsViewModel = logsViewModel;
        _configuratorLaunchService = configuratorLaunchService;

        Clients = new ObservableCollection<ClientListItem>();
        Bases = new ObservableCollection<InfobaseListItem>();
        RefreshCommand = new RelayCommand(RefreshAsync);
        AddClientCommand = new RelayCommand(AddClientAsync);
        EditClientCommand = new RelayCommand(EditClientAsync, () => SelectedClient is not null);
        AddBaseCommand = new RelayCommand(AddBaseAsync);
        EditBaseCommand = new RelayCommand(EditBaseAsync, () => SelectedBase is not null);
        ExportCommand = new RelayCommand(ExportAsync, () => SelectedBase is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => SelectedBase is not null);
        OpenLogsCommand = new RelayCommand(OpenLogs, () => SelectedBase is not null);
        OpenAllLogsCommand = new RelayCommand(OpenAllLogs);
        OpenMcpCommand = new RelayCommand(OpenMcp);
        OpenRemoteNodesCommand = new RelayCommand(OpenRemoteNodes);
        OpenTemplatesCommand = new RelayCommand(OpenTemplates);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenConfiguratorCommand = new RelayCommand(OpenConfiguratorAsync, () => SelectedBase is not null);
        LockCommand = new RelayCommand(Lock);

        _ = RefreshAsync();
    }

    public ObservableCollection<ClientListItem> Clients { get; }
    public ObservableCollection<InfobaseListItem> Bases { get; }

    public ClientListItem? SelectedClient
    {
        get => _selectedClient;
        set
        {
            SetProperty(ref _selectedClient, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public InfobaseListItem? SelectedBase
    {
        get => _selectedBase;
        set
        {
            SetProperty(ref _selectedBase, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddClientCommand { get; }
    public RelayCommand EditClientCommand { get; }
    public RelayCommand AddBaseCommand { get; }
    public RelayCommand EditBaseCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand OpenAllLogsCommand { get; }
    public RelayCommand OpenMcpCommand { get; }
    public RelayCommand OpenRemoteNodesCommand { get; }
    public RelayCommand OpenTemplatesCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenConfiguratorCommand { get; }
    public RelayCommand LockCommand { get; }

    public Task RefreshOnNavigateAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var clients = await _profileService.GetClientsAsync();
        var clientMap = clients.ToDictionary(c => c.Id, c => c);

        Clients.Clear();
        foreach (var client in clients.OrderBy(c => c.Name))
        {
            Clients.Add(new ClientListItem
            {
                Id = client.Id,
                Name = client.Name,
                ExportRootPath = client.ExportRootPath,
                Comment = client.Comment ?? string.Empty
            });
        }

        var bases = await _profileService.GetInfobasesAsync();
        var exportSummaries = await _configurationService.GetExportSummariesForAllBasesAsync();
        Bases.Clear();
        foreach (var profile in bases.OrderBy(b => b.Name))
        {
            clientMap.TryGetValue(profile.ClientId, out var client);
            exportSummaries.TryGetValue(profile.Id, out var summary);
            Bases.Add(new InfobaseListItem
            {
                Id = profile.Id,
                ClientName = client?.Name ?? "?",
                Name = profile.Name,
                ConnectionType = profile.ConnectionType,
                LastExportStatus = profile.LastExportStatus,
                LastExportAt = profile.LastExportAt,
                ExportSettingsSummary = summary ?? "не задано"
            });
        }

        if (Clients.Count == 0 && Bases.Count == 0)
            StatusMessage = "Добавьте клиента и базу для начала работы.";
        else
            StatusMessage = string.Empty;
    }

    private async Task AddClientAsync()
    {
        if (!await _clientEditViewModel.PrepareCreateAsync())
            return;

        _navigationService.NavigateTo(_clientEditViewModel);
    }

    private async Task EditClientAsync()
    {
        if (SelectedClient is null)
            return;

        if (!await _clientEditViewModel.PrepareEditAsync(SelectedClient.Name))
        {
            StatusMessage = "Клиент не найден.";
            return;
        }

        _navigationService.NavigateTo(_clientEditViewModel);
    }

    private async Task AddBaseAsync()
    {
        if (!await _baseEditViewModel.PrepareCreateAsync())
            return;

        _navigationService.NavigateTo(_baseEditViewModel);
    }

    private async Task EditBaseAsync()
    {
        if (SelectedBase is null)
            return;

        if (!await _baseEditViewModel.PrepareEditAsync(SelectedBase.Id))
        {
            StatusMessage = "База не найдена.";
            return;
        }

        _navigationService.NavigateTo(_baseEditViewModel);
    }

    private async Task ExportAsync()
    {
        if (SelectedBase is null)
            return;

        await _exportViewModel.BeginAsync(SelectedBase.Id, SelectedBase.DisplayName);
        _navigationService.NavigateTo(_exportViewModel);
    }

    private void OpenFolder()
    {
        if (SelectedBase is null)
            return;

        _ = OpenFolderAsync();
    }

    private async Task OpenFolderAsync()
    {
        var profile = await _profileService.GetInfobaseByIdAsync(SelectedBase!.Id);
        if (profile is null)
            return;

        var client = (await _profileService.GetClientsAsync())
            .FirstOrDefault(c => c.Id == profile.ClientId);
        if (client is null)
            return;

        var path = Path.Combine(client.ExportRootPath, client.Name, profile.Name);
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenLogs()
    {
        _logsViewModel.SetBaseFilter(SelectedBase!.Id, SelectedBase.DisplayName);
        _navigationService.NavigateTo(_logsViewModel);
    }

    private void OpenAllLogs()
    {
        _logsViewModel.ClearBaseFilter();
        _navigationService.NavigateTo(_logsViewModel);
    }

    private void OpenMcp()
    {
        _navigationService.NavigateTo(_configMcpViewModel);
    }

    private void OpenRemoteNodes()
    {
        _navigationService.NavigateTo(_remoteNodesViewModel);
    }

    private void OpenTemplates()
    {
        _navigationService.NavigateTo(_templatesViewModel);
    }

    private void OpenSettings()
    {
        _navigationService.NavigateTo(_hubSettingsViewModel);
    }

    private async Task OpenConfiguratorAsync()
    {
        if (SelectedBase is null)
            return;

        try
        {
            await _configuratorLaunchService.LaunchAsync(SelectedBase.Id);
            StatusMessage = "Конфигуратор 1С запущен.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void Lock()
    {
        _vaultViewModel.LockVault();
        _navigationService.SetRoot<VaultViewModel>();
    }
}
