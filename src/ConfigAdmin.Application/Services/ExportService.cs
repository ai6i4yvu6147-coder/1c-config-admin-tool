using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Services;

namespace ConfigAdmin.Application.Services;

public sealed class ExportService
{
    private readonly IExportOrchestrator _exportOrchestrator;
    private readonly IInfobaseRepository _infobaseRepository;

    public ExportService(IExportOrchestrator exportOrchestrator, IInfobaseRepository infobaseRepository)
    {
        _exportOrchestrator = exportOrchestrator;
        _infobaseRepository = infobaseRepository;
    }

    public Task<ExportResult> ExportByNameAsync(
        string baseName,
        ExportPlan? planOverride = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        return ExportInternalAsync(async () =>
        {
            var profile = await _infobaseRepository.GetByNameAsync(baseName, ct)
                ?? throw new InvalidOperationException($"База '{baseName}' не найдена.");
            return profile.Id;
        }, planOverride, progress, ct);
    }

    public Task<ExportResult> ExportByIdAsync(
        Guid infobaseId,
        ExportPlan? planOverride = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default) =>
        _exportOrchestrator.ExportBaseAsync(infobaseId, planOverride, progress, ct);

    public async Task<IReadOnlyList<ExportResult>> ExportAllAsync(
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var profiles = await _infobaseRepository.GetAllAsync(ct);
        var results = new List<ExportResult>();

        for (var i = 0; i < profiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new ExportProgress
            {
                Stage = "Выгрузка базы",
                Detail = profiles[i].Name,
                CompletedSteps = i,
                TotalSteps = profiles.Count
            });

            results.Add(await _exportOrchestrator.ExportBaseAsync(profiles[i].Id, progress: null, ct: ct));
        }

        return results;
    }

    private async Task<ExportResult> ExportInternalAsync(
        Func<Task<Guid>> resolveId,
        ExportPlan? planOverride,
        IProgress<ExportProgress>? progress,
        CancellationToken ct)
    {
        var id = await resolveId();
        return await _exportOrchestrator.ExportBaseAsync(id, planOverride, progress, ct);
    }
}
