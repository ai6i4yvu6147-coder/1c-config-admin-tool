namespace ConfigAdmin.Domain.Models;

public sealed class ClientProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string ExportRootPath { get; set; } = string.Empty;
}
