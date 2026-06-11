namespace ConfigAdmin.Application.Services;

public sealed class ExportRunMeta
{
    public Guid RunId { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
    public string InfobaseName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public List<ExportRunMetaStep> Steps { get; set; } = [];
    public List<string> ExportedExtensions { get; set; } = [];
}

public sealed class ExportRunMetaStep
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public double DurationMs { get; set; }
    public string? CommandMasked { get; set; }
    public string? OutLogPath { get; set; }
    public string? DumpResultPath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutLogExcerpt { get; set; }

    public string DisplayError =>
        !string.IsNullOrWhiteSpace(ErrorMessage) ? ErrorMessage
        : !string.IsNullOrWhiteSpace(OutLogExcerpt) ? OutLogExcerpt
        : string.Empty;
}
