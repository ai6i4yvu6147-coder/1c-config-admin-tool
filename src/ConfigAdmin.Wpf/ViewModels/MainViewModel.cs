using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private readonly LogsViewModel _logsViewModel;
    private readonly ConfiguratorLaunchService _configuratorLaunchService;

    private ClientListItem? _selectedClient;
    private InfobaseListItem? _selectedBase;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

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
        _logsViewModel = logsViewModel;
        _configuratorLaunchService = configuratorLaunchService;

        Clients = new ObservableCollection<ClientListItem>();
        Bases = new ObservableCollection<InfobaseListItem>();
        RefreshCommand = new RelayCommand(RefreshAsync);
        AddClientCommand = new RelayCommand(AddClient);
        EditClientCommand = new RelayCommand(EditClient, () => SelectedClient is not null);
        AddBaseCommand = new RelayCommand(AddBase);
        EditBaseCommand = new RelayCommand(EditBase, () => SelectedBase is not null);
        ExportCommand = new RelayCommand(Export, () => SelectedBase is not null && !IsBusy);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => SelectedBase is not null);
        OpenLogsCommand = new RelayCommand(OpenLogs, () => SelectedBase is not null);
        OpenAllLogsCommand = new RelayCommand(OpenAllLogs);
        OpenMcpCommand = new RelayCommand(OpenMcp);
        OpenRemoteNodesCommand = new RelayCommand(OpenRemoteNodes);
        OpenTemplatesCommand = new RelayCommand(OpenTemplates);
        OpenConfiguratorCommand = new RelayCommand(OpenConfiguratorAsync, () => SelectedBase is not null);
        LockCommand = new RelayCommand(Lock);

        _ = RefreshAsync();
    }

    public ObservableCollection<ClientListItem> Clients { get; }
    public ObservableCollection<InfobaseListItem> Bases { get; }

    public ClientListItem? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
    }

    public InfobaseListItem? SelectedBase
    {
        get => _selectedBase;
        set => SetProperty(ref _selectedBase, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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
        Bases.Clear();
        foreach (var profile in bases.OrderBy(b => b.Name))
        {
            clientMap.TryGetValue(profile.ClientId, out var client);
            var instances = await _configurationService.GetInstancesAsync(profile.Id);
            Bases.Add(new InfobaseListItem
            {
                Id = profile.Id,
                ClientName = client?.Name ?? "?",
                Name = profile.Name,
                ConnectionType = profile.ConnectionType,
                LastExportStatus = profile.LastExportStatus,
                LastExportAt = profile.LastExportAt,
                ExportSettingsSummary = InfobaseConfigurationService.BuildExportSummary(instances)
            });
        }

        if (Clients.Count == 0 && Bases.Count == 0)
            StatusMessage = "Добавьте клиента и базу для начала работы.";
        else
            StatusMessage = string.Empty;
    }

    private void AddClient()
    {
        _clientEditViewModel.BeginCreate();
        _navigationService.NavigateTo(_clientEditViewModel);
    }

    private void EditClient()
    {
        if (SelectedClient is null)
            return;

        _clientEditViewModel.BeginEdit(SelectedClient.Name);
        _navigationService.NavigateTo(_clientEditViewModel);
    }

    private void AddBase()
    {
        _baseEditViewModel.BeginCreate();
        _navigationService.NavigateTo(_baseEditViewModel);
    }

    private void EditBase()
    {
        if (SelectedBase is null)
            return;

        _baseEditViewModel.BeginEdit(SelectedBase.Id);
        _navigationService.NavigateTo(_baseEditViewModel);
    }

    private void Export()
    {
        if (SelectedBase is null)
            return;

        _exportViewModel.Begin(SelectedBase.Id, SelectedBase.DisplayName);
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
        _vaultSessionService.Lock();
        _vaultViewModel.LockVault();
        _navigationService.SetRoot<VaultViewModel>();
    }
}
