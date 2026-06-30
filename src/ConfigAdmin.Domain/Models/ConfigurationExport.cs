namespace ConfigAdmin.Domain.Models;

public sealed class ConfigurationExport
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public DateTimeOffset? ExportedAt { get; set; }
    public bool IsCurrent { get; set; } = true;
}
