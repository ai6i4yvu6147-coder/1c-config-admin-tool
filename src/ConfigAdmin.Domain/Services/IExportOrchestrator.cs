using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Services;

public interface IExportOrchestrator
{
    Task<ExportResult> ExportBaseAsync(
        Guid infobaseId,
        InstanceExportPlan? planOverride = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default);
}
