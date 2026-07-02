using System.Windows;
using System.Windows.Controls;
using ConfigAdmin.Wpf.ViewModels;
using ConfigAdmin.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConfigAdmin.Wpf.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, Func<object, UIElement>> _viewFactories;
    private readonly Stack<object> _backStack = new();
    private ContentControl? _host;
    private object? _current;
    private BaseEditView? _baseEditView;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _viewFactories = BuildViewFactories();
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
        SetRoot(vm);
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var vm = _serviceProvider.GetRequiredService<TViewModel>();
        NavigateTo(vm);
    }

    public void NavigateTo(object viewModel)
    {
        if (_current is not null && !ReferenceEquals(_current, viewModel))
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

        DetachSingletonViewBindings();

        _current = viewModel;
        var viewType = viewModel.GetType();
        if (!_viewFactories.TryGetValue(viewType, out var factory))
            throw new NotSupportedException($"Неподдерживаемая ViewModel: {viewType.Name}");

        _host.Content = factory(viewModel);

        if (viewModel is IRefreshOnNavigate refreshable)
            _ = RunRefreshAsync(refreshable);

        NavigationChanged?.Invoke();
    }

    private static async Task RunRefreshAsync(IRefreshOnNavigate refreshable)
    {
        try
        {
            await refreshable.RefreshOnNavigateAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обновления экрана {Screen}", refreshable.GetType().Name);
        }
    }

    private void DetachSingletonViewBindings()
    {
        if (_host?.Content is BaseEditView baseEdit)
        {
            baseEdit.ResetPasswordField();
            baseEdit.DataContext = null;
        }
    }

    private BaseEditView GetBaseEditView(BaseEditViewModel vm)
    {
        _baseEditView ??= new BaseEditView();
        _baseEditView.DataContext = vm;
        return _baseEditView;
    }

    private Dictionary<Type, Func<object, UIElement>> BuildViewFactories() =>
        new()
        {
            [typeof(VaultViewModel)] = vm => new VaultView { DataContext = vm },
            [typeof(MainViewModel)] = vm => new MainView { DataContext = vm },
            [typeof(ClientEditViewModel)] = vm => new ClientEditView { DataContext = vm },
            [typeof(BaseEditViewModel)] = vm => GetBaseEditView((BaseEditViewModel)vm),
            [typeof(ExportViewModel)] = vm => new ExportView { DataContext = vm },
            [typeof(ConfigMcpViewModel)] = vm => new ConfigMcpView { DataContext = vm },
            [typeof(McpHubViewModel)] = vm => new McpHubView { DataContext = vm },
            [typeof(LogsViewModel)] = vm => new LogsView { DataContext = vm },
            [typeof(HubModeSelectorViewModel)] = vm => new HubModeSelectorView { DataContext = vm },
            [typeof(SyncAgentViewModel)] = vm => new SyncAgentView { DataContext = vm },
            [typeof(RemoteNodesViewModel)] = vm => new RemoteNodesView { DataContext = vm },
            [typeof(RemoteNodeEditViewModel)] = vm => new RemoteNodeEditView { DataContext = vm },
            [typeof(ConfigurationTemplatesViewModel)] = vm => new ConfigurationTemplatesView { DataContext = vm },
            [typeof(HubSettingsViewModel)] = vm => new HubSettingsView { DataContext = vm },
        };
}
