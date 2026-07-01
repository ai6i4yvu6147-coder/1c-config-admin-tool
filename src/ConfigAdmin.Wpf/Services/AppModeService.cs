using System.Windows;
using ConfigAdmin.Application.RemoteSync;

using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Services;

public sealed class AppModeService
{
    private readonly HubRuntimeService _hubRuntimeService;
    private readonly SyncAgentConnectionService _syncAgentConnectionService;
    private readonly INavigationService _navigationService;

    public AppModeService(
        HubRuntimeService hubRuntimeService,
        SyncAgentConnectionService syncAgentConnectionService,
        INavigationService navigationService)
    {
        _hubRuntimeService = hubRuntimeService;
        _syncAgentConnectionService = syncAgentConnectionService;
        _navigationService = navigationService;
    }

    public async Task ResetModeAsync()
    {
        var confirm = System.Windows.MessageBox.Show(
            "Сбросить сохранённый режим и вернуться к выбору «Админка» / «Передатчик»?",
            "Сменить режим",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        await _hubRuntimeService.StopReceiverAsync();
        await _syncAgentConnectionService.ShutdownAsync();

        var settings = LocalAppSettings.Load();
        settings.Mode = null;
        settings.Save();

        _navigationService.SetRoot<HubModeSelectorViewModel>();
    }
}
