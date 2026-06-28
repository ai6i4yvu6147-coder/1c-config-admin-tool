using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IAgentSessionRepository
{
    Task SaveAsync(AgentSession session, CancellationToken ct = default);
    Task<AgentSession?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task DeleteByNodeIdAsync(Guid nodeId, CancellationToken ct = default);
    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct = default);
}
