using System.Collections.ObjectModel;
using System.Windows.Input;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class BaseEditViewModel : ObservableObject
{
    private readonly ProfileService _profileService;
    private readonly ConnectionTestService _connectionTestService;
    private readonly FileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly RemoteNodeService _remoteNodeService;
    private readonly IExportPathBuilder _exportPathBuilder;

    private Guid? _editingId;
    private string _selectedClient = string.Empty;
    private string _name = string.Empty;
    private string _platformPath = string.Empty;
    private bool _isServerConnection = true;
    private string _connectionString = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _exportConfiguration = true;
    private bool _exportAllExtensions;
    private string _selectedExtensionsText = string.Empty;
    private bool _isLocalExport = true;
    private Guid? _selectedRemoteNodeId;
    private string _remoteExportPath = string.Empty;
    private string _localTargetHint = string.Empty;
    private string _statusMessage = string.Empty;

    public BaseEditViewModel(
        ProfileService profileService,
        ConnectionTestService connectionTestService,
        FileDialogService fileDialogService,
        INavigationService navigationService,
        RemoteNodeService remoteNodeService,
        IExportPathBuilder exportPathBuilder)
    {
        _profileService = profileService;
        _connectionTestService = connectionTestService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _remoteNodeService = remoteNodeService;
        _exportPathBuilder = exportPathBuilder;

        Clients = new ObservableCollection<string>();
        RemoteNodes = new ObservableCollection<RemoteNodeOption>();

        SaveCommand = new RelayCommand(SaveAsync);
        TestConnectionCommand = new RelayCommand(TestConnectionAsync);
        BrowsePlatformCommand = new RelayCommand(BrowsePlatform);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
        _ = LoadClientsAsync();
    }

    public ObservableCollection<string> Clients { get; }
    public ObservableCollection<RemoteNodeOption> RemoteNodes { get; }

    public string SelectedClient
    {
        get => _selectedClient;
        set
        {
            SetProperty(ref _selectedClient, value);
            _ = LoadRemoteNodesForClientAsync();
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);
            UpdateLocalTargetHint();
        }
    }

    public string PlatformPath
    {
        get => _platformPath;
        set => SetProperty(ref _platformPath, value);
    }

    public bool IsServerConnection
    {
        get => _isServerConnection;
        set => SetProperty(ref _isServerConnection, value);
    }

    public string ConnectionString
    {
        get => _connectionString;
        set => SetProperty(ref _connectionString, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool ExportConfiguration
    {
        get => _exportConfiguration;
        set => SetProperty(ref _exportConfiguration, value);
    }

    public bool ExportAllExtensions
    {
        get => _exportAllExtensions;
        set
        {
            SetProperty(ref _exportAllExtensions, value);
            RaisePropertyChanged(nameof(IsSelectedExtensionsEnabled));
        }
    }

    public string SelectedExtensionsText
    {
        get => _selectedExtensionsText;
        set => SetProperty(ref _selectedExtensionsText, value);
    }

    public bool IsSelectedExtensionsEnabled => !ExportAllExtensions;

    public bool IsLocalExport
    {
        get => _isLocalExport;
        set
        {
            SetProperty(ref _isLocalExport, value);
            RaisePropertyChanged(nameof(IsRemoteExport));
        }
    }

    public bool IsRemoteExport
    {
        get => !IsLocalExport;
        set => IsLocalExport = !value;
    }

    public Guid? SelectedRemoteNodeId
    {
        get => _selectedRemoteNodeId;
        set => SetProperty(ref _selectedRemoteNodeId, value);
    }

    public string RemoteExportPath
    {
        get => _remoteExportPath;
        set => SetProperty(ref _remoteExportPath, value);
    }

    public string LocalTargetHint
    {
        get => _localTargetHint;
        set => SetProperty(ref _localTargetHint, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string Title => _editingId is null ? "Новая база" : "Редактирование базы";

    public RelayCommand SaveCommand { get; }
    public RelayCommand TestConnectionCommand { get; }
    public RelayCommand BrowsePlatformCommand { get; }
    public RelayCommand BackCommand { get; }

    public async void BeginCreate()
    {
        _editingId = null;
        ResetFields();
        await LoadClientsAsync();

        if (Clients.Count == 0)
            StatusMessage = "Сначала добавьте клиента (кнопка «Добавить клиента» на главном экране).";

        RaisePropertyChanged(nameof(Title));
    }

    public async void BeginEdit(Guid id)
    {
        _editingId = id;
        var profile = await _profileService.GetInfobaseByIdAsync(id);
        if (profile is null)
            return;

        await LoadClientsAsync();
        var clients = await _profileService.GetClientsAsync();
        var client = clients.FirstOrDefault(c => c.Id == profile.ClientId);

        SelectedClient = client?.Name ?? string.Empty;
        Name = profile.Name;
        PlatformPath = profile.PlatformPath;
        IsServerConnection = profile.ConnectionType == ConnectionType.Server;
        ConnectionString = profile.ConnectionString;
        Username = profile.Username ?? string.Empty;
        Password = string.Empty;
        ExportConfiguration = profile.ExportConfiguration;
        ExportAllExtensions = profile.ExportAllExtensions;
        SelectedExtensionsText = string.Join(Environment.NewLine, profile.SelectedExtensions);
        IsLocalExport = profile.ExportLocation == ExportLocation.Local;
        SelectedRemoteNodeId = profile.RemoteNodeId;
        RemoteExportPath = profile.RemoteExportPath ?? string.Empty;
        await LoadRemoteNodesForClientAsync();
        UpdateLocalTargetHint();
        StatusMessage = string.Empty;
        RaisePropertyChanged(nameof(Title));
    }

    private async Task LoadClientsAsync()
    {
        Clients.Clear();
        foreach (var client in await _profileService.GetClientsAsync())
            Clients.Add(client.Name);

        if (Clients.Count > 0 && string.IsNullOrWhiteSpace(SelectedClient))
            SelectedClient = Clients[0];
    }

    private async Task LoadRemoteNodesForClientAsync()
    {
        RemoteNodes.Clear();
        var clients = await _profileService.GetClientsAsync();
        var client = clients.FirstOrDefault(c => c.Name == SelectedClient);
        if (client is null)
            return;

        var nodes = await _remoteNodeService.GetAllAsync();
        foreach (var node in nodes.Where(n => n.ClientId == client.Id && n.Enabled))
            RemoteNodes.Add(new RemoteNodeOption(node.Id, node.Name));

        if (SelectedRemoteNodeId is null && RemoteNodes.Count > 0)
            SelectedRemoteNodeId = RemoteNodes[0].Id;
    }

    private async void UpdateLocalTargetHint()
    {
        var clients = await _profileService.GetClientsAsync();
        var client = clients.FirstOrDefault(c => c.Name == SelectedClient);
        if (client is null || string.IsNullOrWhiteSpace(Name))
        {
            LocalTargetHint = string.Empty;
            return;
        }

        LocalTargetHint = _exportPathBuilder.GetConfigurationPath(
            client.ExportRootPath, client.Name, Name);
    }

    private void ResetFields()
    {
        Name = string.Empty;
        PlatformPath = string.Empty;
        IsServerConnection = true;
        ConnectionString = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        ExportConfiguration = true;
        ExportAllExtensions = false;
        SelectedExtensionsText = string.Empty;
        IsLocalExport = true;
        SelectedRemoteNodeId = null;
        RemoteExportPath = string.Empty;
        LocalTargetHint = string.Empty;
        StatusMessage = string.Empty;
    }

    private void BrowsePlatform()
    {
        var path = _fileDialogService.PickExecutable(PlatformPath);
        if (!string.IsNullOrWhiteSpace(path))
            PlatformPath = path;
    }

    private List<string> ParseExtensions() =>
        SelectedExtensionsText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private bool ValidateExportSettings()
    {
        if (IsRemoteExport)
            return ExportConfiguration;

        if (ExportConfiguration || ExportAllExtensions)
            return true;

        return ParseExtensions().Count > 0;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (Clients.Count == 0)
            {
                StatusMessage = "Сначала добавьте клиента.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedClient) ||
                string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(PlatformPath) ||
                string.IsNullOrWhiteSpace(ConnectionString))
            {
                StatusMessage = "Заполните обязательные поля подключения.";
                return;
            }

            if (!ValidateExportSettings())
            {
                StatusMessage = IsRemoteExport
                    ? "Для Remote sync включите выгрузку основной конфигурации."
                    : "Укажите что выгружать: конфигурацию, все расширения или список расширений.";
                return;
            }

            if (IsRemoteExport && SelectedRemoteNodeId is null)
            {
                StatusMessage = "Выберите RDP-узел.";
                return;
            }

            await _profileService.AddOrUpdateInfobaseAsync(
                SelectedClient,
                Name,
                PlatformPath,
                IsServerConnection ? ConnectionType.Server : ConnectionType.File,
                ConnectionString,
                Username,
                string.IsNullOrWhiteSpace(Password) ? null : Password,
                ExportConfiguration,
                IsRemoteExport ? false : ExportAllExtensions,
                IsRemoteExport ? [] : ParseExtensions(),
                exportLocation: IsRemoteExport ? ExportLocation.Remote : ExportLocation.Local,
                remoteNodeId: IsRemoteExport ? SelectedRemoteNodeId : null,
                remoteExportPath: IsRemoteExport && !string.IsNullOrWhiteSpace(RemoteExportPath)
                    ? RemoteExportPath
                    : null);

            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedClient) ||
                string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(PlatformPath) ||
                string.IsNullOrWhiteSpace(ConnectionString))
            {
                StatusMessage = "Заполните поля подключения перед проверкой.";
                return;
            }

            var profile = await _profileService.AddOrUpdateInfobaseAsync(
                SelectedClient,
                Name,
                PlatformPath,
                IsServerConnection ? ConnectionType.Server : ConnectionType.File,
                ConnectionString,
                Username,
                string.IsNullOrWhiteSpace(Password) ? null : Password,
                ExportConfiguration,
                ExportAllExtensions,
                ParseExtensions());

            var result = await _connectionTestService.TestAsync(profile);
            StatusMessage = result.Success
                ? "Подключение успешно."
                : $"Ошибка подключения (код {result.ExitCode}): {result.StandardError}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public sealed record RemoteNodeOption(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }
}
