using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ClientEditViewModel : ObservableObject
{
    private readonly ProfileService _profileService;
    private readonly FileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;

    private Guid? _editingId;
    private string _name = string.Empty;
    private string _exportRootPath = string.Empty;
    private string _comment = string.Empty;
    private string _statusMessage = string.Empty;

    public ClientEditViewModel(
        ProfileService profileService,
        FileDialogService fileDialogService,
        INavigationService navigationService)
    {
        _profileService = profileService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;

        SaveCommand = new RelayCommand(SaveAsync);
        BrowseExportRootCommand = new RelayCommand(BrowseExportRoot);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ExportRootPath
    {
        get => _exportRootPath;
        set => SetProperty(ref _exportRootPath, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string Title => _editingId is null ? "Новый клиент" : "Редактирование клиента";

    public RelayCommand SaveCommand { get; }
    public RelayCommand BrowseExportRootCommand { get; }

    public Task<bool> PrepareCreateAsync()
    {
        _editingId = null;
        Name = string.Empty;
        ExportRootPath = string.Empty;
        Comment = string.Empty;
        StatusMessage = string.Empty;
        RaisePropertyChanged(nameof(Title));
        return Task.FromResult(true);
    }

    public async Task<bool> PrepareEditAsync(string clientName)
    {
        var client = await _profileService.GetClientByNameAsync(clientName);
        if (client is null)
            return false;

        _editingId = client.Id;
        Name = client.Name;
        ExportRootPath = client.ExportRootPath;
        Comment = client.Comment ?? string.Empty;
        StatusMessage = string.Empty;
        RaisePropertyChanged(nameof(Title));
        return true;
    }

    private void BrowseExportRoot()
    {
        var path = _fileDialogService.PickFolder(ExportRootPath);
        if (!string.IsNullOrWhiteSpace(path))
            ExportRootPath = path;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ExportRootPath))
            {
                StatusMessage = "Укажите имя клиента и каталог выгрузки.";
                return;
            }

            await _profileService.AddOrUpdateClientAsync(Name, ExportRootPath, Comment, _editingId);
            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
