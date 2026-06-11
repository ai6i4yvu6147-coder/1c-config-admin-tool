using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IClientRepository
{
    Task<IReadOnlyList<ClientProfile>> GetAllAsync(CancellationToken ct = default);
    Task<ClientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClientProfile?> GetByNameAsync(string name, CancellationToken ct = default);
    Task SaveAsync(ClientProfile client, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
