using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Models;

public sealed class ExportInstancePlan
{
    public Guid InstanceId { get; init; }
    public ConfigurationKind Kind { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? DesignerName { get; init; }
}

public sealed class InstanceExportPlan
{
    public List<ExportInstancePlan> Instances { get; init; } = [];
    public ExportFormat Format { get; set; } = ExportFormat.Hierarchical;

    public bool HasWork => Instances.Count > 0;
}
