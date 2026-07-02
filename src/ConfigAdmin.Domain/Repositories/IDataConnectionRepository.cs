using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IDataConnectionRepository
{
    Task<DataConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DataConnection?> GetByInfobaseIdAsync(Guid infobaseId, CancellationToken ct = default);
    Task<IReadOnlyList<DataConnection>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DataConnection>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task SaveAsync(DataConnection connection, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
