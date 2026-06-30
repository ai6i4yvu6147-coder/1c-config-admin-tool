using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class InstanceEditItem : ObservableObject
{
    private string _displayName = string.Empty;
    private string _designerName = string.Empty;
    private bool _exportEnabled = true;
    private Guid? _templateId;

    public Guid Id { get; set; }
    public ConfigurationKind Kind { get; set; }

    public Guid? TemplateId
    {
        get => _templateId;
        set
        {
            SetProperty(ref _templateId, value);
            RaisePropertyChanged(nameof(IsLocal));
            RaisePropertyChanged(nameof(IsTemplateLinked));
            RaisePropertyChanged(nameof(ShowDesignerName));
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string DesignerName
    {
        get => _designerName;
        set => SetProperty(ref _designerName, value);
    }

    public bool ExportEnabled
    {
        get => _exportEnabled;
        set => SetProperty(ref _exportEnabled, value);
    }

    public bool IsBase => Kind == ConfigurationKind.Base;
    public bool IsLocal => !IsBase && TemplateId is null;
    public bool IsTemplateLinked => !IsBase && TemplateId is not null;
    public bool ShowDesignerName => !IsBase;
    public bool ShowDisplayName => IsLocal;
}
