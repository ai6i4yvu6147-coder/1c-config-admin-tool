namespace ConfigAdmin.Domain.Models;

public sealed class ExportProgress
{
    public string Stage { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
}
