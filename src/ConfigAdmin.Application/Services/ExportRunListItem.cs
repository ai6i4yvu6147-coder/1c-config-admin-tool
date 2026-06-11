namespace ConfigAdmin.Application.Services;

public sealed class ExportRunListItem
{
    public Guid Id { get; init; }
    public Guid InfobaseId { get; init; }
    public string BaseDisplayName { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public long DurationMs { get; init; }
    public string CommandMasked { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? OutputPath { get; init; }
    public string? MetaJsonPath { get; init; }

    public string CommandPreview
    {
        get
        {
            var line = CommandMasked.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return line ?? string.Empty;
        }
    }

    public string? RunArtifactsDirectory =>
        string.IsNullOrWhiteSpace(MetaJsonPath) ? null : Path.GetDirectoryName(MetaJsonPath);
}
