using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IRemoteNodeRepository
{
    Task<IReadOnlyList<RemoteNodeProfile>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RemoteNodeProfile>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task<RemoteNodeProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(RemoteNodeProfile node, CancellationToken ct = default);
    Task TouchLastSeenAsync(Guid nodeId, DateTimeOffset seenAt, string? agentVersion, CancellationToken ct = default);
}
