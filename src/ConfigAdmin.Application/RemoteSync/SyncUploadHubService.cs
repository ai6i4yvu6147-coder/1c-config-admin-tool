using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncUploadHubService
{
    private readonly SyncUploadSessionStore _sessionStore;
    private readonly ISyncJobRepository _syncJobRepository;
    private readonly SyncAgentHubService _agentHubService;

    public SyncUploadHubService(
        SyncUploadSessionStore sessionStore,
        ISyncJobRepository syncJobRepository,
        SyncAgentHubService agentHubService)
    {
        _sessionStore = sessionStore;
        _syncJobRepository = syncJobRepository;
        _agentHubService = agentHubService;
    }

    public async Task<CreateUploadSessionResponse?> CreateSessionAsync(
        string accessToken,
        CreateUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var nodeId = await _agentHubService.ValidateTokenAsync(accessToken, ct);
        if (nodeId is null)
            return null;

        var job = await _syncJobRepository.GetByIdAsync(request.JobId, ct);
        if (job is null || job.RemoteNodeId != nodeId.Value)
            return null;

        _sessionStore.CleanupExpired();

        var sessionId = Guid.NewGuid();
        var chunkSize = request.ChunkSizeBytes > 0
            ? request.ChunkSizeBytes
            : SyncUploadSessionStore.DefaultChunkSizeBytes;

        _sessionStore.CreateSession(
            sessionId,
            job.Id,
            nodeId.Value,
            request.FileName,
            request.TotalBytes,
            request.Sha256,
            chunkSize);

        await _syncJobRepository.UpdateProgressAsync(job.Id, request.TotalBytes, 0, ct);
        await _syncJobRepository.SetUploadSessionAsync(job.Id, sessionId.ToString("N"), ct);

        return new CreateUploadSessionResponse
        {
            SessionId = sessionId,
            ChunkSizeBytes = chunkSize,
            AcceptedChunks = []
        };
    }

    public async Task<ChunkUploadResponse?> PutChunkAsync(
        string accessToken,
        Guid sessionId,
        int chunkIndex,
        Stream body,
        CancellationToken ct = default)
    {
        if (await _agentHubService.ValidateTokenAsync(accessToken, ct) is null)
            return null;

        var meta = _sessionStore.GetSession(sessionId);
        if (meta is null)
            return null;

        using var ms = new MemoryStream();
        await body.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        meta = _sessionStore.SaveChunk(sessionId, chunkIndex, data);
        await _syncJobRepository.UpdateProgressAsync(meta.JobId, meta.TotalBytes, meta.SessionReceivedBytes, ct);

        return new ChunkUploadResponse
        {
            ChunkIndex = chunkIndex,
            ReceivedBytes = data.Length,
            SessionReceivedBytes = meta.SessionReceivedBytes
        };
    }

    public async Task<UploadSessionStateResponse?> GetSessionAsync(
        string accessToken,
        Guid sessionId,
        CancellationToken ct = default)
    {
        if (await _agentHubService.ValidateTokenAsync(accessToken, ct) is null)
            return null;

        var meta = _sessionStore.GetSession(sessionId);
        if (meta is null)
            return null;

        return new UploadSessionStateResponse
        {
            SessionId = meta.SessionId,
            TotalBytes = meta.TotalBytes,
            Sha256 = meta.Sha256,
            ChunkSizeBytes = meta.ChunkSizeBytes,
            ReceivedChunkIndexes = meta.ReceivedChunkIndexes.OrderBy(i => i).ToArray(),
            SessionReceivedBytes = meta.SessionReceivedBytes,
            ExpiresAt = meta.ExpiresAt
        };
    }
}
