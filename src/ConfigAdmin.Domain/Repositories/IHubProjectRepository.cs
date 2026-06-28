using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IHubProjectRepository
{
    Task<IReadOnlyList<HubProjectProfile>> GetAllAsync(CancellationToken ct = default);
    Task<HubProjectProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(HubProjectProfile project, CancellationToken ct = default);
}
