using System.Windows;
using ConfigAdmin.Application;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Wpf.Services;
using ConfigAdmin.Wpf.ViewModels;
using ConfigAdmin.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConfigAdmin.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        LoggingSetup.Configure();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddConfigAdminApplication();
                services.AddSingleton<FileDialogService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<VaultViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<ClientEditViewModel>();
                services.AddSingleton<BaseEditViewModel>();
                services.AddSingleton<ExportViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var navigation = _host.Services.GetRequiredService<INavigationService>();
        navigation.Attach(mainWindow.RootContent);

        mainWindow.Show();
        navigation.SetRoot<VaultViewModel>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var vault = _host.Services.GetRequiredService<VaultViewModel>();
            vault.LockVault();
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
