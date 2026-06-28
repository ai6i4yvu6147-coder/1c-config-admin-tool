using System.Windows.Controls;
using ConfigAdmin.Wpf.ViewModels;
using ConfigAdmin.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigAdmin.Wpf.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<object> _backStack = new();
    private ContentControl? _host;
    private object? _current;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public event Action? NavigationChanged;

    public bool CanGoBack => _backStack.Count > 0;

    public void Attach(ContentControl host) => _host = host;

    public void SetRoot(object viewModel)
    {
        _backStack.Clear();
        Show(viewModel);
    }

    public void SetRoot<TViewModel>() where TViewModel : class
    {
        var vm = _serviceProvider.GetRequiredService<TViewModel>();
        if (vm is MainViewModel main)
            _ = main.RefreshOnNavigateAsync();
        if (vm is RemoteNodesViewModel remoteNodes)
            _ = remoteNodes.RefreshOnNavigateAsync();
        SetRoot(vm);
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var vm = _serviceProvider.GetRequiredService<TViewModel>();
        if (vm is MainViewModel main)
            _ = main.RefreshOnNavigateAsync();
        if (vm is RemoteNodesViewModel remoteNodes)
            _ = remoteNodes.RefreshOnNavigateAsync();
        NavigateTo(vm);
    }

    public void NavigateTo(object viewModel)
    {
        if (_current is not null)
            _backStack.Push(_current);

        Show(viewModel);
    }

    public void GoBack()
    {
        if (_backStack.Count == 0)
            return;

        Show(_backStack.Pop());
    }

    private void Show(object viewModel)
    {
        if (_host is null)
            throw new InvalidOperationException("Navigation host не подключён.");

        _current = viewModel;
        _host.Content = viewModel switch
        {
            VaultViewModel vm => new VaultView { DataContext = vm },
            MainViewModel vm => new MainView { DataContext = vm },
            ClientEditViewModel vm => new ClientEditView { DataContext = vm },
            BaseEditViewModel vm => new BaseEditView { DataContext = vm },
            ExportViewModel vm => new ExportView { DataContext = vm },
            ConfigMcpViewModel vm => new ConfigMcpView { DataContext = vm },
            LogsViewModel vm => new LogsView { DataContext = vm },
            HubModeSelectorViewModel vm => new HubModeSelectorView { DataContext = vm },
            SyncAgentViewModel vm => new SyncAgentView { DataContext = vm },
            RemoteNodesViewModel vm => new RemoteNodesView { DataContext = vm },
            RemoteNodeEditViewModel vm => new RemoteNodeEditView { DataContext = vm },
            _ => throw new NotSupportedException($"Неподдерживаемая ViewModel: {viewModel.GetType().Name}")
        };

        NavigationChanged?.Invoke();
    }
}
