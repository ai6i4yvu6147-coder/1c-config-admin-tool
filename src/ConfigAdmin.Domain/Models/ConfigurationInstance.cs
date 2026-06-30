using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Domain.Models;

public sealed class ConfigurationInstance
{
    public Guid Id { get; set; }
    public Guid InfobaseId { get; set; }
    public Guid? TemplateId { get; set; }
    public ConfigurationKind Kind { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? DesignerName { get; set; }
    public bool ExportEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid? ConfigMcpProjectId { get; set; }
    public Guid? ConfigMcpDatabaseId { get; set; }

    public bool IsMcpLinked => ConfigMcpProjectId is Guid id && id != Guid.Empty;
}
