using System.Text.Json;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Infrastructure.FileSystem;
using ConfigAdmin.Integration.OneC;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.Export;

public sealed class ExportOrchestrator : IExportOrchestrator
{
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IExportRunRepository _exportRunRepository;
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
        _secretVault = secretVault;
        _cliAdapter = cliAdapter;
        _pathBuilder = pathBuilder;
        _runArtifactPathBuilder = runArtifactPathBuilder;
        _directoryService = directoryService;
        _logger = logger;
    }

    public async Task<ExportResult> ExportBaseAsync(
        Guid infobaseId,
        ExportPlan? planOverride = null,
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

            var plan = planOverride ?? ExportPlan.FromProfile(profile);
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

            var totalSteps = CountSteps(plan);
            var completed = 0;
            var exportedExtensions = new List<string>();
            var hasFailure = false;
            var fatalFailure = false;

            if (plan.ExportConfiguration)
            {
                progress?.Report(new ExportProgress
                {
                    Stage = "Выгрузка основной конфигурации",
                    CompletedSteps = completed,
                    TotalSteps = totalSteps
                });

                var configTempPath = Path.Combine(tempPath, "configuration");
                Directory.CreateDirectory(configTempPath);

                var step = await RunDumpStepAsync(
                    profile,
                    connection,
                    configTempPath,
                    allExtensions: false,
                    extensionName: null,
                    plan.Format,
                    "configuration",
                    client.Name,
                    profile.Name,
                    runId,
                    ct);

                steps.Add(step);
                maskedCommands = AppendCommand(maskedCommands, step.CommandMasked);
                completed++;

                if (!step.Success)
                {
                    hasFailure = true;
                    fatalFailure = true;
                }
            }

            if (!fatalFailure && plan.ExportAllExtensions)
            {
                progress?.Report(new ExportProgress
                {
                    Stage = "Выгрузка всех расширений",
                    CompletedSteps = completed,
                    TotalSteps = totalSteps
                });

                var allExtensionsTempPath = Path.Combine(tempPath, "all-extensions");
                Directory.CreateDirectory(allExtensionsTempPath);

                var step = await RunDumpStepAsync(
                    profile,
                    connection,
                    allExtensionsTempPath,
                    allExtensions: true,
                    extensionName: null,
                    plan.Format,
                    "all-extensions",
                    client.Name,
                    profile.Name,
                    runId,
                    ct);

                steps.Add(step);
                maskedCommands = AppendCommand(maskedCommands, step.CommandMasked);
                completed++;

                if (!step.Success)
                    hasFailure = true;
                else
                    exportedExtensions.AddRange(DiscoverExtensions(allExtensionsTempPath));
            }
            else if (!fatalFailure && plan.SelectedExtensions.Count > 0)
            {
                foreach (var extensionName in plan.SelectedExtensions)
                {
                    ct.ThrowIfCancellationRequested();

                    progress?.Report(new ExportProgress
                    {
                        Stage = "Выгрузка расширения",
                        Detail = extensionName,
                        CompletedSteps = completed,
                        TotalSteps = totalSteps
                    });

                    var extensionTempPath = Path.Combine(tempPath, "extensions", extensionName);
                    Directory.CreateDirectory(extensionTempPath);

                    var step = await RunDumpStepAsync(
                        profile,
                        connection,
                        extensionTempPath,
                        allExtensions: false,
                        extensionName: extensionName,
                        plan.Format,
                        $"extension:{extensionName}",
                        client.Name,
                        profile.Name,
                        runId,
                        ct);

                    steps.Add(step);
                    maskedCommands = AppendCommand(maskedCommands, step.CommandMasked);
                    completed++;

                    if (step.Success)
                        exportedExtensions.Add(extensionName);
                    else
                        hasFailure = true;
                }
            }

            sw.Stop();

            if (fatalFailure)
            {
                var error = steps.LastOrDefault(s => !s.Success)?.ErrorMessage ?? "Ошибка выгрузки.";
                metaJsonPath = await WriteRunMetaAsync(
                    runId, profile, client, sw.ElapsedMilliseconds, steps, exportedExtensions, ct);
                await SaveRunLogAsync(
                    runId, infobaseId, startedAt, false, steps, maskedCommands, baseDir, error, sw.Elapsed, metaJsonPath, ct);
                await _infobaseRepository.UpdateLastExportAsync(infobaseId, DateTimeOffset.UtcNow, ExportStatus.Failed, ct);
                _directoryService.SafeDelete(tempPath);

                _logger.LogWarning(
                    "Выгрузка базы {BaseName} завершилась с ошибкой (run {RunId}): {Error}",
                    profile.Name,
                    runId,
                    error);

                return new ExportResult
                {
                    Success = false,
                    ExitCode = steps.LastOrDefault(s => !s.Success)?.ExitCode ?? -1,
                    OutputPath = baseDir,
                    ErrorMessage = error,
                    Duration = sw.Elapsed,
                    ExportedExtensions = exportedExtensions,
                    Steps = steps
                };
            }

            PublishExportResults(
                client.ExportRootPath,
                client.Name,
                profile.Name,
                tempPath,
                plan,
                steps,
                exportedExtensions);

            metaJsonPath = await WriteRunMetaAsync(
                runId, profile, client, sw.ElapsedMilliseconds, steps, exportedExtensions, ct);

            _directoryService.SafeDelete(tempPath);
            tempPath = null;

            var success = !hasFailure;
            var status = success
                ? ExportStatus.Success
                : exportedExtensions.Count > 0 || steps.Any(s => s.StepName == "configuration" && s.Success)
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

    private void PublishExportResults(
        string exportRoot,
        string clientName,
        string baseName,
        string tempPath,
        ExportPlan plan,
        IReadOnlyList<ExportStepResult> steps,
        IReadOnlyList<string> exportedExtensions)
    {
        if (plan.ExportConfiguration)
        {
            var configStep = steps.FirstOrDefault(s => s.StepName == "configuration");
            if (configStep?.Success == true)
            {
                var source = Path.Combine(tempPath, "configuration");
                var target = _pathBuilder.GetConfigurationPath(exportRoot, clientName, baseName);
                _directoryService.ReplaceDirectory(target, source);
            }
        }

        if (plan.ExportAllExtensions)
        {
            var allStep = steps.FirstOrDefault(s => s.StepName == "all-extensions");
            if (allStep?.Success == true)
            {
                var sourceRoot = Path.Combine(tempPath, "all-extensions");
                foreach (var extensionDir in Directory.GetDirectories(sourceRoot))
                {
                    var extensionName = Path.GetFileName(extensionDir);
                    var target = _pathBuilder.GetExtensionPath(exportRoot, clientName, baseName, extensionName);
                    _directoryService.ReplaceDirectory(target, extensionDir);
                }
            }
        }
        else
        {
            foreach (var extensionName in exportedExtensions)
            {
                var source = Path.Combine(tempPath, "extensions", extensionName);
                if (!Directory.Exists(source))
                    continue;

                var target = _pathBuilder.GetExtensionPath(exportRoot, clientName, baseName, extensionName);
                _directoryService.ReplaceDirectory(target, source);
            }
        }
    }

    private async Task<ExportStepResult> RunDumpStepAsync(
        InfobaseProfile profile,
        ConnectionSettings connection,
        string outputPath,
        bool allExtensions,
        string? extensionName,
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
            AllExtensions = allExtensions,
            ExtensionName = extensionName,
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

        _logger.LogInformation(
            "Шаг {StepName} завершён (run {RunId}, success={Success}, exit={ExitCode})",
            stepName,
            runId,
            success,
            exitCode);

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

    private static int CountSteps(ExportPlan plan)
    {
        var count = 0;
        if (plan.ExportConfiguration) count++;
        if (plan.ExportAllExtensions) count++;
        else count += plan.SelectedExtensions.Count;
        return Math.Max(count, 1);
    }

    private static List<string> DiscoverExtensions(string extensionsPath)
    {
        if (!Directory.Exists(extensionsPath))
            return [];

        return Directory.GetDirectories(extensionsPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();
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
        var metaJson = BuildMetaJson(profile, client, runId, durationMs, steps, exportedExtensions);
        await File.WriteAllTextAsync(metaPath, metaJson, ct);
        return metaPath;
    }

    private static string BuildMetaJson(
        InfobaseProfile profile,
        ClientProfile client,
        Guid runId,
        long durationMs,
        IReadOnlyList<ExportStepResult> steps,
        IReadOnlyList<string> exportedExtensions) =>
        JsonSerializer.Serialize(new
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
