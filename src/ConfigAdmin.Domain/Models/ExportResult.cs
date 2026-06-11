namespace ConfigAdmin.Domain.Models;

public sealed class ExportResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
    public List<string> ExportedExtensions { get; init; } = [];
    public List<ExportStepResult> Steps { get; init; } = [];
}
