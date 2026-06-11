using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IInfobaseRepository
{
    Task<IReadOnlyList<InfobaseProfile>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InfobaseProfile>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task<InfobaseProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<InfobaseProfile?> GetByNameAsync(string name, CancellationToken ct = default);
    Task SaveAsync(InfobaseProfile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task UpdateLastExportAsync(Guid id, DateTimeOffset exportedAt, Enums.ExportStatus status, CancellationToken ct = default);
}
