using System.Windows.Input;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class HubSettingsViewModel : BusyViewModelBase, IRefreshOnNavigate
{
    private readonly HubRuntimeService _hubRuntimeService;
    private readonly INavigationService _navigationService;
    private readonly AppModeService _appModeService;

    private string _hubListenUrl = "http://0.0.0.0:18443";
    private string _receiverStatus = string.Empty;

    public HubSettingsViewModel(
        HubRuntimeService hubRuntimeService,
        INavigationService navigationService,
        AppModeService appModeService)
    {
        _hubRuntimeService = hubRuntimeService;
        _navigationService = navigationService;
        _appModeService = appModeService;

        SaveCommand = new RelayCommand(SaveAsync, () => !IsBusy);
        ResetModeCommand = new RelayCommand(ResetModeAsync, () => !IsBusy);
    }

    public string HubListenUrl
    {
        get => _hubListenUrl;
        set => SetProperty(ref _hubListenUrl, value);
    }

    public string ReceiverStatus
    {
        get => _receiverStatus;
        set => SetProperty(ref _receiverStatus, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetModeCommand { get; }

    public Task RefreshOnNavigateAsync()
    {
        var settings = LocalAppSettings.Load();
        HubListenUrl = settings.HubListenUrl;
        UpdateReceiverStatus();
        StatusMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        var newUrl = HubListenUrl.Trim();
        if (string.IsNullOrWhiteSpace(newUrl))
        {
            StatusMessage = "Укажите адрес приёмника.";
            return;
        }

        var settings = LocalAppSettings.Load();
        var urlChanged = !string.Equals(settings.HubListenUrl, newUrl, StringComparison.OrdinalIgnoreCase);
        var previousUrl = settings.HubListenUrl;
        var wasRunning = _hubRuntimeService.IsReceiverRunning;

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            if (urlChanged && wasRunning)
                await _hubRuntimeService.StopReceiverAsync();

            _hubRuntimeService.ConfigureListenUrl(newUrl);

            if (urlChanged && wasRunning)
                await _hubRuntimeService.StartReceiverAsync();

            settings.HubListenUrl = newUrl;
            settings.Save();

            HubListenUrl = newUrl;
            UpdateReceiverStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;

            if (urlChanged && wasRunning)
            {
                try
                {
                    _hubRuntimeService.ConfigureListenUrl(previousUrl);
                    await _hubRuntimeService.StartReceiverAsync();
                }
                catch
                {
                    // ignored — user will need to fix manually
                }
            }
        }
        finally
        {
            IsBusy = false;
            UpdateReceiverStatus();
        }
    }

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

    private void UpdateReceiverStatus()
    {
        ReceiverStatus = _hubRuntimeService.IsReceiverRunning
            ? $"Слушает: {_hubRuntimeService.ListenUrl}"
            : "Остановлен";
    }
}
