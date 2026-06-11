namespace ConfigAdmin.Domain.Models;

public sealed class ExportRunLog
{
    public Guid Id { get; set; }
    public Guid InfobaseId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public long DurationMs { get; set; }
    public string CommandMasked { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? OutputPath { get; set; }
    public string? MetaJsonPath { get; set; }
}
