using System.Text.Json;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Integration.OneC;

namespace ConfigAdmin.Application.Services;

public sealed class ExportRunQueryService
{
    private static readonly JsonSerializerOptions MetaJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IExportRunRepository _exportRunRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IClientRepository _clientRepository;

    public ExportRunQueryService(
        IExportRunRepository exportRunRepository,
        IInfobaseRepository infobaseRepository,
        IClientRepository clientRepository)
    {
        _exportRunRepository = exportRunRepository;
        _infobaseRepository = infobaseRepository;
        _clientRepository = clientRepository;
    }

    public async Task<IReadOnlyList<ExportRunLog>> GetByBaseNameAsync(
        string baseName,
        int limit = 100,
        CancellationToken ct = default)
    {
        var profile = await _infobaseRepository.GetByNameAsync(baseName, ct)
            ?? throw new InvalidOperationException($"База '{baseName}' не найдена.");
        return await _exportRunRepository.GetByInfobaseIdAsync(profile.Id, limit, ct);
    }

    public Task<IReadOnlyList<ExportRunLog>> GetAllAsync(int limit = 100, CancellationToken ct = default) =>
        _exportRunRepository.GetAllAsync(limit, ct);

    public Task<IReadOnlyList<ExportRunLog>> GetByInfobaseIdAsync(
        Guid infobaseId,
        int limit = 100,
        CancellationToken ct = default) =>
        _exportRunRepository.GetByInfobaseIdAsync(infobaseId, limit, ct);

    public async Task<IReadOnlyList<ExportRunListItem>> GetListItemsAsync(
        Guid? infobaseId = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var runs = infobaseId is Guid id
            ? await _exportRunRepository.GetByInfobaseIdAsync(id, limit, ct)
            : await _exportRunRepository.GetAllAsync(limit, ct);

        var infobases = (await _infobaseRepository.GetAllAsync(ct)).ToDictionary(b => b.Id);
        var clients = (await _clientRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);

        return runs.Select(run =>
        {
            infobases.TryGetValue(run.InfobaseId, out var profile);
            ClientProfile? client = null;
            if (profile is not null)
                clients.TryGetValue(profile.ClientId, out client);

            var baseName = profile?.Name ?? "?";
            var clientName = client?.Name ?? "?";

            return new ExportRunListItem
            {
                Id = run.Id,
                InfobaseId = run.InfobaseId,
                BaseDisplayName = $"{clientName} / {baseName}",
                StartedAt = run.StartedAt,
                Success = run.Success,
                ExitCode = run.ExitCode,
                DurationMs = run.DurationMs,
                CommandMasked = run.CommandMasked,
                ErrorMessage = run.ErrorMessage,
                OutputPath = run.OutputPath,
                MetaJsonPath = run.MetaJsonPath
            };
        }).ToList();
    }

    public async Task<ExportRunMeta?> LoadMetaAsync(string? metaJsonPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(metaJsonPath) || !File.Exists(metaJsonPath))
            return null;

        await using var stream = File.OpenRead(metaJsonPath);
        var meta = await JsonSerializer.DeserializeAsync<ExportRunMeta>(stream, MetaJsonOptions, ct);
        if (meta is null)
            return null;

        EnrichStepsFromOutLogs(meta.Steps);
        return meta;
    }

    private static void EnrichStepsFromOutLogs(IEnumerable<ExportRunMetaStep> steps)
    {
        foreach (var step in steps)
        {
            if (step.Success)
                continue;

            if (string.IsNullOrWhiteSpace(step.OutLogExcerpt))
            {
                var outText = OneCOutLogReader.Truncate(OneCOutLogReader.ReadText(step.OutLogPath));
                if (!string.IsNullOrWhiteSpace(outText))
                    step.OutLogExcerpt = outText;
            }

            if (string.IsNullOrWhiteSpace(step.ErrorMessage))
                step.ErrorMessage = step.OutLogExcerpt ?? step.DisplayError;
        }
    }
}
