using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IExportRunRepository
{
    Task SaveAsync(ExportRunLog run, CancellationToken ct = default);
    Task<IReadOnlyList<ExportRunLog>> GetByInfobaseIdAsync(Guid infobaseId, int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<ExportRunLog>> GetAllAsync(int limit = 100, CancellationToken ct = default);
}
