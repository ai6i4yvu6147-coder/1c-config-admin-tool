using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Domain.Models;

public sealed class ConfigurationTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ConfigurationKind Kind { get; set; } = ConfigurationKind.Extension;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
}
