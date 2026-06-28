using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface ISyncJobRepository
{
    Task SaveAsync(SyncJobProfile job, CancellationToken ct = default);
    Task<SyncJobProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SyncJobProfile?> GetActiveForInfobaseAsync(Guid infobaseId, CancellationToken ct = default);
    Task<SyncJobProfile?> ClaimPendingForNodeAsync(Guid nodeId, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, SyncJobStatus status, string? errorMessage = null, CancellationToken ct = default);
    Task UpdateProgressAsync(Guid id, long bytesTotal, long bytesReceived, CancellationToken ct = default);
    Task SetUploadSessionAsync(Guid jobId, string sessionId, CancellationToken ct = default);
}
