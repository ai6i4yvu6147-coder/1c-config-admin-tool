using System.Collections.ObjectModel;
using System.Windows.Input;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ConfigurationTemplatesViewModel : ObservableObject, IRefreshOnNavigate
{
    private readonly ConfigurationCatalogService _catalogService;
    private readonly INavigationService _navigationService;

    private ConfigurationTemplateListItem? _selectedTemplate;
    private string _editName = string.Empty;
    private string _editDescription = string.Empty;
    private string _statusMessage = string.Empty;

    public ConfigurationTemplatesViewModel(
        ConfigurationCatalogService catalogService,
        INavigationService navigationService)
    {
        _catalogService = catalogService;
        _navigationService = navigationService;

        Templates = new ObservableCollection<ConfigurationTemplateListItem>();

        RefreshCommand = new RelayCommand(RefreshAsync);
        AddCommand = new RelayCommand(BeginAdd);
        SaveCommand = new RelayCommand(SaveAsync);
        DeleteCommand = new RelayCommand(DeleteAsync, () => SelectedTemplate is { IsSystem: false });
    }

    public ObservableCollection<ConfigurationTemplateListItem> Templates { get; }

    public ConfigurationTemplateListItem? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            SetProperty(ref _selectedTemplate, value);
            if (value is not null)
            {
                EditName = value.Name;
                EditDescription = value.Description;
            }
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public string EditDescription
    {
        get => _editDescription;
        set => SetProperty(ref _editDescription, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public Task RefreshOnNavigateAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        Templates.Clear();
        foreach (var template in await _catalogService.GetTemplatesAsync())
        {
            Templates.Add(new ConfigurationTemplateListItem
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description ?? string.Empty,
                Kind = template.Kind,
                IsSystem = template.IsSystem
            });
        }
    }

    private void BeginAdd()
    {
        SelectedTemplate = null;
        EditName = string.Empty;
        EditDescription = string.Empty;
        StatusMessage = string.Empty;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EditName))
            {
                StatusMessage = "Укажите имя шаблона.";
                return;
            }

            var template = new ConfigurationTemplate
            {
                Id = SelectedTemplate?.Id ?? Guid.Empty,
                Name = EditName.Trim(),
                Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim(),
                Kind = ConfigurationKind.Extension
            };

            await _catalogService.SaveTemplateAsync(template);
            StatusMessage = "Сохранено.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedTemplate is null || SelectedTemplate.IsSystem)
            return;

        try
        {
            await _catalogService.DeleteTemplateAsync(SelectedTemplate.Id);
            StatusMessage = "Удалено.";
            BeginAdd();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

public sealed class ConfigurationTemplateListItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ConfigurationKind Kind { get; init; }
    public bool IsSystem { get; init; }
    public string KindLabel => Kind == ConfigurationKind.Base ? "основная" : "расширение";
}
