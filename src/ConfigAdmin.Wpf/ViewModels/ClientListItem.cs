namespace ConfigAdmin.Wpf.ViewModels;

public sealed class ClientListItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ExportRootPath { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
}
