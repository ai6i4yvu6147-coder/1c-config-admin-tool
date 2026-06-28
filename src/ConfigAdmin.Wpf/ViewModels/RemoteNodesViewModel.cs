using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class RemoteNodeListItem
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string NodeIdText => Id.ToString();
    public DateTimeOffset? LastSeenAt { get; init; }
    public string LastSeenText => LastSeenAt?.ToLocalTime().ToString("G") ?? "—";
    public bool Enabled { get; init; }
    public string OnlineStatus { get; init; } = "offline";

    public static string BuildOnlineStatus(DateTimeOffset? lastSeenAt)
    {
        if (lastSeenAt is null)
            return "offline";

        var age = DateTimeOffset.UtcNow - lastSeenAt.Value.ToUniversalTime();
        return age <= TimeSpan.FromSeconds(SyncAgentHubService.DefaultPollIntervalMs * 2)
            ? "online"
            : "offline";
    }
}

public sealed class RemoteNodesViewModel : ObservableObject
{
    private readonly RemoteNodeService _remoteNodeService;
    private readonly ProfileService _profileService;
    private readonly HubRuntimeService _hubRuntimeService;
    private readonly INavigationService _navigationService;
    private readonly RemoteNodeEditViewModel _editViewModel;

    private RemoteNodeListItem? _selectedNode;
    private string _statusMessage = string.Empty;
    private string _receiverStatus = string.Empty;
    private bool _isBusy;

    public RemoteNodesViewModel(
        RemoteNodeService remoteNodeService,
        ProfileService profileService,
        HubRuntimeService hubRuntimeService,
        INavigationService navigationService,
        RemoteNodeEditViewModel editViewModel)
    {
        _remoteNodeService = remoteNodeService;
        _profileService = profileService;
        _hubRuntimeService = hubRuntimeService;
        _navigationService = navigationService;
        _editViewModel = editViewModel;

        Nodes = new ObservableCollection<RemoteNodeListItem>();

        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        AddCommand = new RelayCommand(AddNodeAsync);
        EditCommand = new RelayCommand(EditNodeAsync, () => SelectedNode is not null);
        CopyNodeIdCommand = new RelayCommand(CopyNodeId, () => SelectedNode is not null);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public ObservableCollection<RemoteNodeListItem> Nodes { get; }

    public RemoteNodeListItem? SelectedNode
    {
        get => _selectedNode;
        set
        {
            SetProperty(ref _selectedNode, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ReceiverStatus
    {
        get => _receiverStatus;
        set => SetProperty(ref _receiverStatus, value);
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

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand CopyNodeIdCommand { get; }
    public RelayCommand BackCommand { get; }

    public Task RefreshOnNavigateAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            var clients = await _profileService.GetClientsAsync();
            var clientMap = clients.ToDictionary(c => c.Id, c => c.Name);
            var nodes = await _remoteNodeService.GetAllAsync();

            Nodes.Clear();
            foreach (var node in nodes.OrderBy(n => n.Name))
            {
                clientMap.TryGetValue(node.ClientId, out var clientName);
                Nodes.Add(new RemoteNodeListItem
                {
                    Id = node.Id,
                    ClientName = clientName ?? "?",
                    Name = node.Name,
                    LastSeenAt = node.LastSeenAt,
                    Enabled = node.Enabled,
                    OnlineStatus = RemoteNodeListItem.BuildOnlineStatus(node.LastSeenAt)
                });
            }

            ReceiverStatus = _hubRuntimeService.IsReceiverRunning
                ? $"Receiver: {_hubRuntimeService.ListenUrl}"
                : "Receiver: не запущен";
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

    private async Task AddNodeAsync()
    {
        try
        {
            await _editViewModel.PrepareCreateAsync(_hubRuntimeService.ListenUrl);
            _navigationService.NavigateTo(_editViewModel);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task EditNodeAsync()
    {
        if (SelectedNode is null)
            return;

        try
        {
            if (!await _editViewModel.PrepareEditAsync(SelectedNode.Id, _hubRuntimeService.ListenUrl))
            {
                StatusMessage = "Узел не найден.";
                return;
            }

            _navigationService.NavigateTo(_editViewModel);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void CopyNodeId()
    {
        if (SelectedNode is null)
            return;

        System.Windows.Clipboard.SetText(SelectedNode.NodeIdText);
        StatusMessage = "Node ID скопирован.";
    }
}
