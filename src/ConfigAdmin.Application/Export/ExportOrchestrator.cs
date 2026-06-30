using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Infrastructure.FileSystem;
using ConfigAdmin.Integration.OneC;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConfigAdmin.Application.Export;

public sealed class ExportOrchestrator : IExportOrchestrator
{
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IExportRunRepository _exportRunRepository;
    private readonly IConfigurationExportRepository _exportRepository;
    private readonly InfobaseConfigurationService _configurationService;
    private readonly ISecretVault _secretVault;
    private readonly IOneCCliAdapter _cliAdapter;
    private readonly IExportPathBuilder _pathBuilder;
    private readonly IRunArtifactPathBuilder _runArtifactPathBuilder;
    private readonly AtomicDirectoryService _directoryService;
    private readonly ILogger<ExportOrchestrator> _logger;

    public ExportOrchestrator(
        IInfobaseRepository infobaseRepository,
        IClientRepository clientRepository,
        IExportRunRepository exportRunRepository,
        IConfigurationExportRepository exportRepository,
        InfobaseConfigurationService configurationService,
        ISecretVault secretVault,
        IOneCCliAdapter cliAdapter,
        IExportPathBuilder pathBuilder,
        IRunArtifactPathBuilder runArtifactPathBuilder,
        AtomicDirectoryService directoryService,
        ILogger<ExportOrchestrator> logger)
    {
        _infobaseRepository = infobaseRepository;
        _clientRepository = clientRepository;
        _exportRunRepository = exportRunRepository;
        _exportRepository = exportRepository;
        _configurationService = configurationService;
        _secretVault = secretVault;
        _cliAdapter = cliAdapter;
        _pathBuilder = pathBuilder;
        _runArtifactPathBuilder = runArtifactPathBuilder;
        _directoryService = directoryService;
        _logger = logger;
    }

    public async Task<ExportResult> ExportBaseAsync(
        Guid infobaseId,
        InstanceExportPlan? planOverride = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var steps = new List<ExportStepResult>();
        string? tempPath = null;
        string? baseDir = null;
        string maskedCommands = string.Empty;
        ClientProfile? client = null;
        InfobaseProfile? profile = null;
        string? metaJsonPath = null;

        try
        {
            profile = await _infobaseRepository.GetByIdAsync(infobaseId, ct)
                ?? throw new InvalidOperationException($"База {infobaseId} не найдена.");

            client = await _clientRepository.GetByIdAsync(profile.ClientId, ct)
                ?? throw new InvalidOperationException($"Клиент {profile.ClientId} не найден.");

            var plan = planOverride ?? await _configurationService.BuildExportPlanAsync(infobaseId, ct);
            if (!plan.HasWork)
                throw new InvalidOperationException("План выгрузки не содержит шагов.");

            Directory.CreateDirectory(_runArtifactPathBuilder.GetRunDirectory(client.Name, profile.Name, runId));
            _logger.LogInformation(
                "Старт выгрузки базы {BaseName} (клиент {ClientName}, run {RunId})",
                profile.Name,
                client.Name,
                runId);

            var password = profile.EncryptedPassword is { Length: > 0 }
                ? _secretVault.Decrypt(profile.EncryptedPassword)
                : null;

            var connection = new ConnectionSettings
            {
                ConnectionType = profile.ConnectionType,
                ConnectionString = profile.ConnectionString,
                Username = profile.Username,
                Password = password
            };

            tempPath = _pathBuilder.CreateTempExportDirectory(client.ExportRootPath, client.Name, profile.Name);
            baseDir = _pathBuilder.GetBaseExportDirectory(client.ExportRootPath, client.Name, profile.Name);

            var totalSteps = plan.Instances.Count;
            var completed = 0;
            var exportedExtensions = new List<string>();
            var hasFailure = false;

            foreach (var instancePlan in plan.Instances)
            {
                ct.ThrowIfCancellationRequested();

                var stage = instancePlan.Kind == ConfigurationKind.Base
                    ? "Выгрузка основной конфигурации"
                    : "Выгрузка расширения";

                progress?.Report(new ExportProgress
                {
                    Stage = stage,
                    Detail = instancePlan.DisplayName,
                    CompletedSteps = completed,
                    TotalSteps = totalSteps
                });

                var stepKey = instancePlan.Kind == ConfigurationKind.Base
                    ? "configuration"
                    : $"extension:{instancePlan.DesignerName}";

                var instanceTempPath = Path.Combine(tempPath, "instances", instancePlan.InstanceId.ToString("N"));
                Directory.CreateDirectory(instanceTempPath);

                var step = await RunDumpStepAsync(
                    profile,
                    connection,
                    instanceTempPath,
                    instancePlan,
                    plan.Format,
                    stepKey,
                    client.Name,
                    profile.Name,
                    runId,
                    ct);

                steps.Add(step);
                maskedCommands = AppendCommand(maskedCommands, step.CommandMasked);
                completed++;

                if (step.Success)
                {
                    if (instancePlan.Kind == ConfigurationKind.Extension && instancePlan.DesignerName is not null)
                        exportedExtensions.Add(instancePlan.DesignerName);

                    await PublishInstanceResultAsync(
                        client.ExportRootPath,
                        client.Name,
                        profile.Name,
                        instancePlan,
                        instanceTempPath,
                        ct);
                    await TouchExportRecordAsync(instancePlan.InstanceId, ct);
                }
                else
                {
                    hasFailure = true;
                }
            }

            sw.Stop();
            metaJsonPath = await WriteRunMetaAsync(
                runId, profile, client, sw.ElapsedMilliseconds, steps, exportedExtensions, ct);

            _directoryService.SafeDelete(tempPath);
            tempPath = null;

            var success = !hasFailure;
            var status = success
                ? ExportStatus.Success
                : steps.Any(s => s.Success)
                    ? ExportStatus.Partial
                    : ExportStatus.Failed;

            var runError = hasFailure ? BuildStepsErrorSummary(steps) : null;

            await SaveRunLogAsync(
                runId,
                infobaseId,
                startedAt,
                success,
                steps,
                maskedCommands,
                baseDir,
                runError,
                sw.Elapsed,
                metaJsonPath,
                ct);

            await _infobaseRepository.UpdateLastExportAsync(infobaseId, DateTimeOffset.UtcNow, status, ct);

            progress?.Report(new ExportProgress
            {
                Stage = "Готово",
                CompletedSteps = totalSteps,
                TotalSteps = totalSteps
            });

            _logger.LogInformation(
                "Выгрузка базы {BaseName} завершена (run {RunId}, success={Success}, duration={DurationMs}ms)",
                profile.Name,
                runId,
                success,
                sw.ElapsedMilliseconds);

            return new ExportResult
            {
                Success = success,
                ExitCode = steps.LastOrDefault()?.ExitCode ?? 0,
                OutputPath = baseDir,
                ErrorMessage = runError,
                Duration = sw.Elapsed,
                ExportedExtensions = exportedExtensions,
                Steps = steps
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Ошибка выгрузки базы {InfobaseId} (run {RunId})", infobaseId, runId);

            if (tempPath is not null)
                _directoryService.SafeDelete(tempPath);

            if (profile is not null && client is not null)
            {
                metaJsonPath = await WriteRunMetaAsync(
                    runId, profile, client, sw.ElapsedMilliseconds, steps, [], ct);
            }

            await SaveRunLogAsync(
                runId,
                infobaseId,
                startedAt,
                false,
                steps,
                maskedCommands,
                baseDir,
                ex.Message,
                sw.Elapsed,
                metaJsonPath,
                ct);

            await _infobaseRepository.UpdateLastExportAsync(infobaseId, DateTimeOffset.UtcNow, ExportStatus.Failed, ct);

            return new ExportResult
            {
                Success = false,
                ExitCode = -1,
                OutputPath = baseDir,
                ErrorMessage = ex.Message,
                Duration = sw.Elapsed,
                Steps = steps
            };
        }
    }

    private Task PublishInstanceResultAsync(
        string exportRoot,
        string clientName,
        string baseName,
        ExportInstancePlan instancePlan,
        string sourcePath,
        CancellationToken ct)
    {
        var target = instancePlan.Kind == ConfigurationKind.Base
            ? _pathBuilder.GetConfigurationPath(exportRoot, clientName, baseName)
            : _pathBuilder.GetExtensionPath(exportRoot, clientName, baseName, instancePlan.DesignerName!);

        _directoryService.ReplaceDirectory(target, sourcePath);
        return Task.CompletedTask;
    }

    private async Task TouchExportRecordAsync(Guid instanceId, CancellationToken ct)
    {
        await _exportRepository.MarkAllNotCurrentForInstanceAsync(instanceId, ct);
        await _exportRepository.SaveAsync(new ConfigurationExport
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            ExportedAt = DateTimeOffset.UtcNow,
            IsCurrent = true
        }, ct);
    }

    private async Task<ExportStepResult> RunDumpStepAsync(
        InfobaseProfile profile,
        ConnectionSettings connection,
        string outputPath,
        ExportInstancePlan instancePlan,
        ExportFormat format,
        string stepName,
        string clientName,
        string baseName,
        Guid runId,
        CancellationToken ct)
    {
        var outLogPath = _runArtifactPathBuilder.GetOutLogPath(clientName, baseName, runId, stepName);
        var dumpResultPath = _runArtifactPathBuilder.GetDumpResultPath(clientName, baseName, runId, stepName);
        Directory.CreateDirectory(Path.GetDirectoryName(outLogPath)!);

        var request = new DumpConfigRequest
        {
            Connection = connection,
            OutputPath = outputPath,
            AllExtensions = false,
            ExtensionName = instancePlan.Kind == ConfigurationKind.Extension
                ? instancePlan.DesignerName
                : null,
            Format = format,
            OutLogPath = outLogPath,
            DumpResultPath = dumpResultPath
        };

        var arguments = _cliAdapter.BuildDumpConfigCommand(request);
        _logger.LogInformation(
            "Шаг {StepName} для базы {BaseName} (run {RunId})",
            stepName,
            baseName,
            runId);

        var result = await _cliAdapter.RunDesignerAsync(new DesignerCommand
        {
            PlatformPath = profile.PlatformPath,
            Arguments = arguments
        }, ct);

        var exitCode = DumpResultReader.ResolveExitCode(result, dumpResultPath);
        var success = exitCode == 0;

        if (!success && !File.Exists(dumpResultPath) && !result.TimedOut)
        {
            _logger.LogWarning(
                "Файл DumpResult не создан для шага {StepName} (run {RunId}), используется код процесса {ExitCode}",
                stepName,
                runId,
                result.ExitCode);
        }

        var error = OneCOutLogReader.ResolveErrorMessage(
            success,
            result.TimedOut,
            exitCode,
            result.StandardError,
            outLogPath);

        var outLogExcerpt = success ? null : OneCOutLogReader.Truncate(OneCOutLogReader.ReadText(outLogPath));

        return new ExportStepResult
        {
            StepName = stepName,
            Success = success,
            ExitCode = exitCode,
            ErrorMessage = error,
            OutputPath = outputPath,
            Duration = result.Duration,
            CommandMasked = result.CommandLineMasked,
            OutLogPath = outLogPath,
            DumpResultPath = dumpResultPath,
            OutLogExcerpt = outLogExcerpt
        };
    }

    private static string AppendCommand(string existing, string command) =>
        string.IsNullOrWhiteSpace(existing) ? command : $"{existing}{Environment.NewLine}{command}";

    private static string BuildStepsErrorSummary(IReadOnlyList<ExportStepResult> steps)
    {
        var failed = steps.Where(s => !s.Success).ToList();
        if (failed.Count == 0)
            return "Часть шагов завершилась с ошибкой.";

        return string.Join(
            Environment.NewLine,
            failed.Select(s => $"{s.StepName}: {s.ErrorMessage ?? "ошибка"}"));
    }

    private async Task<string> WriteRunMetaAsync(
        Guid runId,
        InfobaseProfile profile,
        ClientProfile client,
        long durationMs,
        IReadOnlyList<ExportStepResult> steps,
        IReadOnlyList<string> exportedExtensions,
        CancellationToken ct)
    {
        var metaPath = _runArtifactPathBuilder.GetMetaJsonPath(client.Name, profile.Name, runId);
        var metaJson = JsonSerializer.Serialize(new
        {
            RunId = runId,
            ExportedAt = DateTimeOffset.UtcNow,
            PlatformPath = profile.PlatformPath,
            InfobaseName = profile.Name,
            ClientName = client.Name,
            DurationMs = durationMs,
            Steps = steps.Select(s => new
            {
                s.StepName,
                s.Success,
                s.ExitCode,
                DurationMs = s.Duration.TotalMilliseconds,
                s.CommandMasked,
                s.OutLogPath,
                s.DumpResultPath,
                s.OutLogExcerpt,
                s.ErrorMessage
            }),
            ExportedExtensions = exportedExtensions
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metaPath, metaJson, ct);
        return metaPath;
    }

    private async Task SaveRunLogAsync(
        Guid runId,
        Guid infobaseId,
        DateTimeOffset startedAt,
        bool success,
        IReadOnlyList<ExportStepResult> steps,
        string commandMasked,
        string? outputPath,
        string? errorMessage,
        TimeSpan duration,
        string? metaJsonPath,
        CancellationToken ct)
    {
        var run = new ExportRunLog
        {
            Id = runId,
            InfobaseId = infobaseId,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Success = success,
            ExitCode = steps.LastOrDefault()?.ExitCode ?? (success ? 0 : -1),
            DurationMs = (long)duration.TotalMilliseconds,
            CommandMasked = commandMasked,
            ErrorMessage = errorMessage,
            OutputPath = outputPath,
            MetaJsonPath = metaJsonPath
        };

        await _exportRunRepository.SaveAsync(run, ct);
    }
}
