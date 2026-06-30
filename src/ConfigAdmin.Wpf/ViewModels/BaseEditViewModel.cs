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
    private readonly ConfigurationCatalogService _catalogService;
    private readonly InfobaseConfigurationService _configurationService;
    private readonly IExportPathBuilder _exportPathBuilder;

    private Guid? _editingId;
    private string _selectedClient = string.Empty;
    private string _name = string.Empty;
    private string _platformPath = string.Empty;
    private bool _isServerConnection = true;
    private string _connectionString = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isLocalExport = true;
    private Guid? _selectedRemoteNodeId;
    private string _remoteExportPath = string.Empty;
    private string _localTargetHint = string.Empty;
    private string _statusMessage = string.Empty;
    private Guid? _selectedTemplateToAdd;
    private bool _suppressClientChange;
    private int _beginSequence;

    public BaseEditViewModel(
        ProfileService profileService,
        ConnectionTestService connectionTestService,
        FileDialogService fileDialogService,
        INavigationService navigationService,
        RemoteNodeService remoteNodeService,
        ConfigurationCatalogService catalogService,
        InfobaseConfigurationService configurationService,
        IExportPathBuilder exportPathBuilder)
    {
        _profileService = profileService;
        _connectionTestService = connectionTestService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _remoteNodeService = remoteNodeService;
        _catalogService = catalogService;
        _configurationService = configurationService;
        _exportPathBuilder = exportPathBuilder;

        Clients = new ObservableCollection<string>();
        RemoteNodes = new ObservableCollection<RemoteNodeOption>();
        Instances = new ObservableCollection<InstanceEditItem>();
        AvailableTemplates = new ObservableCollection<ConfigurationTemplateListItem>();

        SaveCommand = new RelayCommand(SaveAsync);
        TestConnectionCommand = new RelayCommand(TestConnectionAsync);
        BrowsePlatformCommand = new RelayCommand(BrowsePlatform);
        BackCommand = new RelayCommand(() => _navigationService.GoBack());
        AddTemplateInstanceCommand = new RelayCommand(AddTemplateInstance);
        AddLocalInstanceCommand = new RelayCommand(AddLocalInstance);
        RemoveInstanceCommand = new RelayCommand(RemoveSelectedInstance, () => SelectedInstance is { IsBase: false });
        _ = LoadClientsAsync();
    }

    public ObservableCollection<string> Clients { get; }
    public ObservableCollection<RemoteNodeOption> RemoteNodes { get; }
    public ObservableCollection<InstanceEditItem> Instances { get; }
    public ObservableCollection<ConfigurationTemplateListItem> AvailableTemplates { get; }

    public InstanceEditItem? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            SetProperty(ref _selectedInstance, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private InstanceEditItem? _selectedInstance;

    public Guid? SelectedTemplateToAdd
    {
        get => _selectedTemplateToAdd;
        set => SetProperty(ref _selectedTemplateToAdd, value);
    }

    public string SelectedClient
    {
        get => _selectedClient;
        set
        {
            SetProperty(ref _selectedClient, value);
            if (!_suppressClientChange)
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
    public RelayCommand AddTemplateInstanceCommand { get; }
    public RelayCommand AddLocalInstanceCommand { get; }
    public RelayCommand RemoveInstanceCommand { get; }

    public async void BeginCreate()
    {
        var seq = Interlocked.Increment(ref _beginSequence);
        try
        {
            _editingId = null;
            ResetFields();
            await LoadClientsAsync();
            if (seq != _beginSequence)
                return;

            await LoadTemplatesAsync();
            if (seq != _beginSequence)
                return;

            Instances.Clear();

            if (Clients.Count == 0)
                StatusMessage = "Сначала добавьте клиента (кнопка «Добавить клиента» на главном экране).";

            RaisePropertyChanged(nameof(Title));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async void BeginEdit(Guid id)
    {
        var seq = Interlocked.Increment(ref _beginSequence);
        try
        {
            _editingId = id;
            var profile = await _profileService.GetInfobaseByIdAsync(id);
            if (profile is null)
            {
                StatusMessage = "База не найдена.";
                return;
            }

            if (seq != _beginSequence)
                return;

            await LoadClientsAsync();
            await LoadTemplatesAsync();
            if (seq != _beginSequence)
                return;

            var clients = await _profileService.GetClientsAsync();
            var client = clients.FirstOrDefault(c => c.Id == profile.ClientId);

            _suppressClientChange = true;
            SelectedClient = client?.Name ?? string.Empty;
            _suppressClientChange = false;

            Name = profile.Name;
            PlatformPath = profile.PlatformPath;
            IsServerConnection = profile.ConnectionType == ConnectionType.Server;
            ConnectionString = profile.ConnectionString;
            Username = profile.Username ?? string.Empty;
            Password = string.Empty;
            IsLocalExport = profile.ExportLocation == ExportLocation.Local;
            SelectedRemoteNodeId = profile.RemoteNodeId;
            RemoteExportPath = profile.RemoteExportPath ?? string.Empty;
            await LoadRemoteNodesForClientAsync();
            if (seq != _beginSequence)
                return;

            await LoadInstancesAsync(id);
            UpdateLocalTargetHint();
            StatusMessage = string.Empty;
            RaisePropertyChanged(nameof(Title));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadInstancesAsync(Guid infobaseId)
    {
        Instances.Clear();
        var instances = await _configurationService.GetInstancesAsync(infobaseId);
        foreach (var instance in instances)
        {
            Instances.Add(new InstanceEditItem
            {
                Id = instance.Id,
                TemplateId = instance.TemplateId,
                Kind = instance.Kind,
                DisplayName = instance.DisplayName,
                DesignerName = instance.DesignerName ?? string.Empty,
                ExportEnabled = instance.ExportEnabled
            });
        }
    }

    private async Task LoadTemplatesAsync()
    {
        AvailableTemplates.Clear();
        foreach (var template in await _catalogService.GetTemplatesAsync())
        {
            if (template.Kind == ConfigurationKind.Base)
                continue;

            AvailableTemplates.Add(new ConfigurationTemplateListItem
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description ?? string.Empty,
                Kind = template.Kind,
                IsSystem = template.IsSystem
            });
        }

        SelectedTemplateToAdd = AvailableTemplates.FirstOrDefault()?.Id;
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
        IsLocalExport = true;
        SelectedRemoteNodeId = null;
        RemoteExportPath = string.Empty;
        LocalTargetHint = string.Empty;
        StatusMessage = string.Empty;
        Instances.Clear();
    }

    private void BrowsePlatform()
    {
        var path = _fileDialogService.PickExecutable(PlatformPath);
        if (!string.IsNullOrWhiteSpace(path))
            PlatformPath = path;
    }

    private void AddTemplateInstance()
    {
        if (SelectedTemplateToAdd is not Guid templateId)
        {
            StatusMessage = "Выберите шаблон.";
            return;
        }

        var template = AvailableTemplates.FirstOrDefault(t => t.Id == templateId);
        if (template is null)
            return;

        if (Instances.Any(i => i.TemplateId == templateId))
        {
            StatusMessage = "Этот шаблон уже добавлен к базе.";
            return;
        }

        Instances.Add(new InstanceEditItem
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            Kind = ConfigurationKind.Extension,
            DisplayName = template.Name,
            ExportEnabled = true
        });
        StatusMessage = string.Empty;
    }

    private void AddLocalInstance()
    {
        Instances.Add(new InstanceEditItem
        {
            Id = Guid.NewGuid(),
            Kind = ConfigurationKind.Extension,
            ExportEnabled = true
        });
    }

    private void RemoveSelectedInstance()
    {
        if (SelectedInstance is null || SelectedInstance.IsBase)
            return;

        Instances.Remove(SelectedInstance);
        SelectedInstance = null;
    }

    private bool ValidateInstances()
    {
        if (!Instances.Any(i => i.ExportEnabled))
            return false;

        foreach (var item in Instances.Where(i => i.Kind == ConfigurationKind.Extension))
        {
            if (string.IsNullOrWhiteSpace(item.DesignerName))
                return false;
            if (item.IsLocal && string.IsNullOrWhiteSpace(item.DisplayName))
                return false;
        }

        return true;
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

            if (IsRemoteExport && SelectedRemoteNodeId is null)
            {
                StatusMessage = "Выберите RDP-узел.";
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
                exportConfiguration: true,
                exportAllExtensions: false,
                selectedExtensions: [],
                exportLocation: IsRemoteExport ? ExportLocation.Remote : ExportLocation.Local,
                remoteNodeId: IsRemoteExport ? SelectedRemoteNodeId : null,
                remoteExportPath: IsRemoteExport && !string.IsNullOrWhiteSpace(RemoteExportPath)
                    ? RemoteExportPath
                    : null,
                infobaseId: _editingId);

            if (Instances.Count == 0)
                await LoadInstancesAsync(profile.Id);

            if (!ValidateInstances())
            {
                StatusMessage = "Включите хотя бы одну конфигурацию и заполните имена расширений.";
                return;
            }

            var existingInstances = await _configurationService.GetInstancesAsync(profile.Id);
            var mcpById = existingInstances.ToDictionary(i => i.Id);

            var sort = 0;
            var toSave = Instances.Select(item =>
            {
                mcpById.TryGetValue(item.Id, out var existing);
                return new ConfigurationInstance
                {
                    Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                    InfobaseId = profile.Id,
                    TemplateId = item.TemplateId,
                    Kind = item.Kind,
                    DisplayName = item.IsBase ? item.DisplayName : item.DisplayName.Trim(),
                    DesignerName = item.IsBase ? null : item.DesignerName.Trim(),
                    ExportEnabled = item.ExportEnabled,
                    SortOrder = item.IsBase ? 0 : ++sort,
                    ConfigMcpProjectId = existing?.ConfigMcpProjectId,
                    ConfigMcpDatabaseId = existing?.ConfigMcpDatabaseId
                };
            }).ToList();

            await _configurationService.SaveInstancesAsync(profile.Id, toSave);
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
                infobaseId: _editingId);

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
