using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Integration;

public interface IOneCCliAdapter
{
    string BuildConnectionArgs(ConnectionSettings settings);
    string BuildDumpConfigCommand(DumpConfigRequest request);
    string MaskPassword(string commandLine);
    Task<ProcessResult> RunDesignerAsync(DesignerCommand command, CancellationToken ct = default);
    Task<ProcessResult> TestConnectionAsync(InfobaseProfile profile, string? password, CancellationToken ct = default);
}

public sealed class DesignerCommand
{
    public string PlatformPath { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public TimeSpan? Timeout { get; init; }
}

public sealed class DumpConfigRequest
{
    public ConnectionSettings Connection { get; init; } = new();
    public string OutputPath { get; init; } = string.Empty;
    public bool AllExtensions { get; init; }
    public string? ExtensionName { get; init; }
    public ExportFormat Format { get; init; } = ExportFormat.Hierarchical;
    public string? OutLogPath { get; init; }
    public string? DumpResultPath { get; init; }
}

public sealed class ProcessResult
{
    public bool Success => ExitCode == 0;
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string CommandLine { get; init; } = string.Empty;
    public string CommandLineMasked { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
}
