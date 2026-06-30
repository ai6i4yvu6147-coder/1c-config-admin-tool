using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.RemoteSync;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class RemoteConfigurationExportService
{
    private readonly IOneCCliAdapter _cliAdapter;
    private readonly ILogger<RemoteConfigurationExportService> _logger;

    public RemoteConfigurationExportService(
        IOneCCliAdapter cliAdapter,
        ILogger<RemoteConfigurationExportService> logger)
    {
        _cliAdapter = cliAdapter;
        _logger = logger;
    }

    public async Task<RemoteExportResult> ExportInstanceAsync(
        ExportJobSpec spec,
        string password,
        string targetPath,
        IProgress<ExportDirectoryStats>? exportProgress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetPath);

        var connection = new ConnectionSettings
        {
            ConnectionType = spec.ConnectionType,
            ConnectionString = spec.ConnectionString,
            Username = spec.Username,
            Password = password
        };

        var outLogPath = Path.Combine(Path.GetDirectoryName(targetPath)!, "export.out.log");
        var dumpResultPath = Path.Combine(Path.GetDirectoryName(targetPath)!, "export.dumpresult");

        var request = new DumpConfigRequest
        {
            Connection = connection,
            OutputPath = targetPath,
            AllExtensions = false,
            ExtensionName = spec.Kind == ConfigurationKind.Extension ? spec.DesignerName : null,
            Format = spec.ExportFormat,
            OutLogPath = outLogPath,
            DumpResultPath = dumpResultPath
        };

        var command = new DesignerCommand
        {
            PlatformPath = spec.PlatformPath,
            Arguments = _cliAdapter.BuildDumpConfigCommand(request),
            Timeout = TimeSpan.FromHours(2)
        };

        _logger.LogInformation(
            "Starting remote export ({Kind}) {DisplayName} to {Path}",
            spec.Kind,
            spec.DisplayName,
            targetPath);

        Task? monitorTask = null;
        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (exportProgress is not null)
        {
            monitorTask = ExportDirectoryMonitor.MonitorAsync(
                targetPath,
                ExportDirectoryMonitor.DefaultInterval,
                exportProgress,
                monitorCts.Token);
        }

        ProcessResult result;
        try
        {
            result = await _cliAdapter.RunDesignerAsync(command, ct);
        }
        finally
        {
            monitorCts.Cancel();
            if (monitorTask is not null)
            {
                try
                {
                    await monitorTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        if (result.ExitCode != 0)
        {
            var error = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"1cv8 exit code {result.ExitCode}"
                : result.StandardError.Trim();
            return RemoteExportResult.Fail(error, result.ExitCode);
        }

        return RemoteExportResult.Ok(targetPath);
    }
}

public sealed class RemoteExportResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public int ExitCode { get; init; }

    public static RemoteExportResult Ok(string path) =>
        new() { Success = true, OutputPath = path };

    public static RemoteExportResult Fail(string message, int exitCode = -1) =>
        new() { Success = false, ErrorMessage = message, ExitCode = exitCode };
}
