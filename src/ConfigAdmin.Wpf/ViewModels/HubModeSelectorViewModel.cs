using System.Windows.Input;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class HubModeSelectorViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly HubRuntimeService _hubRuntimeService;
    private readonly VaultViewModel _vaultViewModel;
    private readonly SyncAgentViewModel _syncAgentViewModel;
    private bool _rememberMode;
    private string _hubListenUrl = "http://0.0.0.0:18443";
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public HubModeSelectorViewModel(
        INavigationService navigationService,
        HubRuntimeService hubRuntimeService,
        VaultViewModel vaultViewModel,
        SyncAgentViewModel syncAgentViewModel)
    {
        _navigationService = navigationService;
        _hubRuntimeService = hubRuntimeService;
        _vaultViewModel = vaultViewModel;
        _syncAgentViewModel = syncAgentViewModel;

        var settings = LocalAppSettings.Load();
        RememberMode = settings.Mode is not null;
        HubListenUrl = settings.HubListenUrl;

        SelectHubCommand = new RelayCommand(SelectHubAsync, () => !IsBusy);
        SelectAgentCommand = new RelayCommand(SelectAgentAsync, () => !IsBusy);
    }

    public bool RememberMode
    {
        get => _rememberMode;
        set => SetProperty(ref _rememberMode, value);
    }

    public string HubListenUrl
    {
        get => _hubListenUrl;
        set => SetProperty(ref _hubListenUrl, value);
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

    public RelayCommand SelectHubCommand { get; }
    public RelayCommand SelectAgentCommand { get; }

    private async Task SelectHubAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            _hubRuntimeService.ConfigureListenUrl(HubListenUrl);
            await _hubRuntimeService.StartReceiverAsync();

            SaveMode(AppRunMode.Hub);
            _navigationService.SetRoot(_vaultViewModel);
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

    private Task SelectAgentAsync()
    {
        SaveMode(AppRunMode.Agent);
        _navigationService.SetRoot(_syncAgentViewModel);
        return Task.CompletedTask;
    }

    private void SaveMode(AppRunMode mode)
    {
        if (!RememberMode)
            return;

        var settings = LocalAppSettings.Load();
        settings.Mode = mode;
        settings.HubListenUrl = HubListenUrl;
        settings.Save();
    }
}
