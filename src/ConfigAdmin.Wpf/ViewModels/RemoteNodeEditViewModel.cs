using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class RemoteNodeEditViewModel : ObservableObject
{
    private readonly RemoteNodeService _remoteNodeService;
    private readonly ProfileService _profileService;
    private readonly INavigationService _navigationService;

    private Guid? _editingId;
    private Guid _selectedClientId;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _pairingPassword = string.Empty;
    private string _hubListenUrlHint = string.Empty;
    private bool _enabled = true;
    private string _statusMessage = string.Empty;
    private IReadOnlyList<ClientOption> _clients = [];

    public RemoteNodeEditViewModel(
        RemoteNodeService remoteNodeService,
        ProfileService profileService,
        INavigationService navigationService)
    {
        _remoteNodeService = remoteNodeService;
        _profileService = profileService;
        _navigationService = navigationService;

        SaveCommand = new RelayCommand(SaveAsync);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
    }

    public IReadOnlyList<ClientOption> Clients
    {
        get => _clients;
        private set => SetProperty(ref _clients, value);
    }

    public Guid SelectedClientId
    {
        get => _selectedClientId;
        set => SetProperty(ref _selectedClientId, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string PairingPassword
    {
        get => _pairingPassword;
        set => SetProperty(ref _pairingPassword, value);
    }

    public string HubListenUrlHint
    {
        get => _hubListenUrlHint;
        set => SetProperty(ref _hubListenUrlHint, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string Title => _editingId is null ? "Новый RDP-узел" : "Редактирование RDP-узла";
    public string NodeIdText => _editingId?.ToString() ?? "будет создан при сохранении";

    public RelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }

    public async Task PrepareCreateAsync(string hubListenUrl)
    {
        await LoadClientsAsync();
        _editingId = null;
        Name = string.Empty;
        Description = string.Empty;
        PairingPassword = string.Empty;
        HubListenUrlHint = ResolveHubUrlHint(hubListenUrl);
        Enabled = true;
        SelectedClientId = Clients.FirstOrDefault()?.Id ?? Guid.Empty;
        StatusMessage = Clients.Count == 0
            ? "Сначала создайте клиента на главном экране."
            : string.Empty;
        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(NodeIdText));
    }

    public async Task<bool> PrepareEditAsync(Guid nodeId, string hubListenUrl)
    {
        await LoadClientsAsync();
        var node = await _remoteNodeService.GetByIdAsync(nodeId);
        if (node is null)
            return false;

        _editingId = node.Id;
        Name = node.Name;
        Description = node.Description ?? string.Empty;
        PairingPassword = string.Empty;
        HubListenUrlHint = ResolveHubUrlHint(node.HubListenUrl ?? hubListenUrl);
        Enabled = node.Enabled;
        SelectedClientId = node.ClientId;
        StatusMessage = "Оставьте pairing-пароль пустым, чтобы не менять.";
        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(NodeIdText));
        return true;
    }

    private async Task LoadClientsAsync()
    {
        var clients = await _profileService.GetClientsAsync();
        Clients = clients.Select(c => new ClientOption(c.Id, c.Name)).ToList();
    }

    private async Task SaveAsync()
    {
        try
        {
            if (SelectedClientId == Guid.Empty || string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = "Укажите клиента и имя узла.";
                return;
            }

            if (_editingId is null && string.IsNullOrWhiteSpace(PairingPassword))
            {
                StatusMessage = "Задайте pairing-пароль для нового узла.";
                return;
            }

            await _remoteNodeService.CreateOrUpdateAsync(
                _editingId,
                SelectedClientId,
                Name,
                Description,
                string.IsNullOrWhiteSpace(PairingPassword) ? null : PairingPassword,
                HubListenUrlHint,
                Enabled);

            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private static string ResolveHubUrlHint(string listenUrl) =>
        SyncTunnelUrlStore.LoadSavedUrl() ?? BuildHint(listenUrl);

    private static string BuildHint(string listenUrl)
    {
        var url = listenUrl.Trim();
        if (url.Contains("0.0.0.0", StringComparison.Ordinal))
            return url.Replace("0.0.0.0", "<Tailscale-IP>", StringComparison.Ordinal);

        return url;
    }
}

public sealed record ClientOption(Guid Id, string Name);
