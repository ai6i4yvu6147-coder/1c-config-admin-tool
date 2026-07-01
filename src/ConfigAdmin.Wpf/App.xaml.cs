using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ConfigAdmin.Application;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Wpf.Services;
using ConfigAdmin.Wpf.ViewModels;
using ConfigAdmin.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ConfigAdmin.Wpf;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(8);

    private IHost? _host;
    private int _shutdownState;
    private bool _forceExit;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        Exit += OnApplicationExit;

        LoggingSetup.Configure();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddConfigAdminApplication();
                services.AddSingleton<UiActivityLog>();
                services.AddSingleton<FileDialogService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<VaultViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<ClientEditViewModel>();
                services.AddSingleton<BaseEditViewModel>();
                services.AddSingleton<ExportViewModel>();
                services.AddSingleton<ConfigMcpViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<HubModeSelectorViewModel>();
                services.AddSingleton<HubSettingsViewModel>();
                services.AddSingleton<AppModeService>();
                services.AddSingleton<SyncAgentViewModel>();
                services.AddSingleton<RemoteNodesViewModel>();
                services.AddSingleton<RemoteNodeEditViewModel>();
                services.AddSingleton<ConfigurationTemplatesViewModel>();
                services.AddSingleton<AgentSettingsStore>();
                services.AddSingleton<HubRuntimeService>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var navigation = _host.Services.GetRequiredService<INavigationService>();
        navigation.Attach(mainWindow.RootContent);

        mainWindow.Show();
        await NavigateInitialAsync(_host.Services, navigation);
    }

    private static async Task NavigateInitialAsync(IServiceProvider services, INavigationService navigation)
    {
        var settings = LocalAppSettings.Load();
        if (settings.Mode is null)
        {
            navigation.SetRoot<HubModeSelectorViewModel>();
            return;
        }

        if (settings.Mode == AppRunMode.Agent)
        {
            navigation.SetRoot<SyncAgentViewModel>();
            return;
        }

        var hubRuntime = services.GetRequiredService<HubRuntimeService>();
        hubRuntime.ConfigureListenUrl(settings.HubListenUrl);
        await hubRuntime.StartReceiverAsync();
        navigation.SetRoot<VaultViewModel>();
    }

    private void OnApplicationExit(object sender, ExitEventArgs e) => EnsureShutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        EnsureShutdown();
        base.OnExit(e);
    }

    private void EnsureShutdown()
    {
        if (Interlocked.CompareExchange(ref _shutdownState, 1, 0) != 0)
            return;

        try
        {
            RunShutdownCore();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при завершении приложения");
        }
        finally
        {
            Interlocked.Exchange(ref _shutdownState, 2);
            Log.CloseAndFlush();
            if (_forceExit)
                Environment.Exit(0);
        }
    }

    private void RunShutdownCore()
    {
        if (_host is null)
            return;

        var host = _host;
        _host = null;

        var shutdownTask = Task.Run(() => ShutdownHostAsync(host));
        try
        {
            if (!shutdownTask.Wait(ShutdownTimeout + TimeSpan.FromSeconds(2)))
            {
                Log.Warning("Завершение приложения превысило таймаут {Timeout}", ShutdownTimeout);
                _forceExit = true;
            }
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            Log.Warning("Таймаут graceful shutdown, принудительное завершение фоновых задач");
            _forceExit = true;
        }

        try
        {
            host.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при Dispose host");
        }
    }

    private static async Task ShutdownHostAsync(IHost host)
    {
        using var shutdownCts = new CancellationTokenSource(ShutdownTimeout);

        try
        {
            var vault = host.Services.GetRequiredService<VaultViewModel>();
            vault.LockVault();

            var agentConnection = host.Services.GetRequiredService<SyncAgentConnectionService>();
            await agentConnection.ShutdownAsync(shutdownCts.Token);

            var hubRuntime = host.Services.GetRequiredService<HubRuntimeService>();
            await hubRuntime.StopReceiverAsync(shutdownCts.Token);

            await host.StopAsync(shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Таймаут graceful shutdown, принудительное завершение фоновых задач");
            throw;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Необработанное исключение UI-потока");
        System.Windows.MessageBox.Show(
            $"Неожиданная ошибка: {e.Exception.Message}",
            "ConfigAdmin",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}

