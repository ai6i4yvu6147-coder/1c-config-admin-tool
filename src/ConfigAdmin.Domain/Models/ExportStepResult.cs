namespace ConfigAdmin.Domain.Models;

public sealed class ExportStepResult
{
    public string StepName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputPath { get; init; }
    public TimeSpan Duration { get; init; }
    public string CommandMasked { get; init; } = string.Empty;
    public string? OutLogPath { get; init; }
    public string? DumpResultPath { get; init; }
    public string? OutLogExcerpt { get; init; }
}
