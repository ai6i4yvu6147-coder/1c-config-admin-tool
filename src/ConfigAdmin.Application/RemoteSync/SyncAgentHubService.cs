using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncAgentHubService
{
    public const int DefaultPollIntervalMs = 10_000;
    public const string HubVersion = "1.0.0";
    private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(7);

    private readonly IRemoteNodeRepository _remoteNodeRepository;
    private readonly IAgentSessionRepository _agentSessionRepository;
    private readonly ISyncJobRepository _syncJobRepository;
    private readonly RemoteSyncOrchestrator _remoteSyncOrchestrator;
    private readonly PairingSecretService _pairingSecretService;
    private readonly SyncJobProgressStore _progressStore;
    private readonly ILogger<SyncAgentHubService> _logger;

    public SyncAgentHubService(
        IRemoteNodeRepository remoteNodeRepository,
        IAgentSessionRepository agentSessionRepository,
        ISyncJobRepository syncJobRepository,
        RemoteSyncOrchestrator remoteSyncOrchestrator,
        PairingSecretService pairingSecretService,
        SyncJobProgressStore progressStore,
        ILogger<SyncAgentHubService> logger)
    {
        _remoteNodeRepository = remoteNodeRepository;
        _agentSessionRepository = agentSessionRepository;
        _syncJobRepository = syncJobRepository;
        _remoteSyncOrchestrator = remoteSyncOrchestrator;
        _pairingSecretService = pairingSecretService;
        _progressStore = progressStore;
        _logger = logger;
    }

    public async Task<RegisterAgentResponse?> RegisterAsync(RegisterAgentRequest request, CancellationToken ct = default)
    {
        var node = await _remoteNodeRepository.GetByIdAsync(request.NodeId, ct);
        if (node is null || !node.Enabled)
            return null;

        if (!_pairingSecretService.Verify(request.PairingPassword, node.PairingSecretVerifier))
            return null;

        await _agentSessionRepository.DeleteExpiredAsync(DateTimeOffset.UtcNow, ct);
        await _agentSessionRepository.DeleteByNodeIdAsync(node.Id, ct);

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await _agentSessionRepository.SaveAsync(new Domain.Models.AgentSession
        {
            TokenHash = HashToken(token),
            NodeId = node.Id,
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenTtl)
        }, ct);

        await _remoteNodeRepository.TouchLastSeenAsync(
            node.Id,
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(request.AgentVersion) ? null : request.AgentVersion,
            ct);

        _logger.LogInformation(
            "Sync agent registered for node {NodeId} from {MachineName}",
            node.Id,
            request.MachineName);

        return new RegisterAgentResponse
        {
            AccessToken = token,
            PollIntervalMs = DefaultPollIntervalMs,
            HubVersion = HubVersion
        };
    }

    public async Task<bool> HeartbeatAsync(string accessToken, HeartbeatRequest request, CancellationToken ct = default)
    {
        var nodeId = await ValidateTokenAsync(accessToken, ct);
        if (nodeId is null || nodeId.Value != request.NodeId)
            return false;

        await _remoteNodeRepository.TouchLastSeenAsync(nodeId.Value, DateTimeOffset.UtcNow, null, ct);

        if (request.CurrentJobId is Guid jobId)
            await UpdateJobFromHeartbeatAsync(nodeId.Value, jobId, request, ct);

        return true;
    }

    private async Task UpdateJobFromHeartbeatAsync(
        Guid nodeId,
        Guid jobId,
        HeartbeatRequest request,
        CancellationToken ct)
    {
        var job = await _syncJobRepository.GetByIdAsync(jobId, ct);
        if (job is null || job.RemoteNodeId != nodeId)
            return;

        if (!string.IsNullOrWhiteSpace(request.ProgressMessage))
            _progressStore.Set(jobId, request.Status, request.ProgressMessage);

        var mapped = MapAgentStatus(request.Status);
        if (mapped is null || !ShouldAdvanceStatus(job.Status, mapped.Value))
            return;

        await _syncJobRepository.UpdateStatusAsync(jobId, mapped.Value, ct: ct);
        _logger.LogInformation(
            "Sync job {JobId} status → {Status} ({Progress})",
            jobId,
            mapped.Value,
            request.ProgressMessage ?? request.Status);
    }

    private static SyncJobStatus? MapAgentStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "exporting" => SyncJobStatus.Exporting,
            "uploading" => SyncJobStatus.Uploading,
            _ => null
        };

    private static bool ShouldAdvanceStatus(SyncJobStatus current, SyncJobStatus next) =>
        next switch
        {
            SyncJobStatus.Exporting => current is SyncJobStatus.Pending or SyncJobStatus.Claimed,
            SyncJobStatus.Uploading => current is SyncJobStatus.Pending
                or SyncJobStatus.Claimed
                or SyncJobStatus.Exporting,
            _ => false
        };

    public async Task<PollJobsResponse?> PollJobsAsync(string accessToken, Guid nodeId, CancellationToken ct = default)
    {
        var validatedNodeId = await ValidateTokenAsync(accessToken, ct);
        if (validatedNodeId is null || validatedNodeId.Value != nodeId)
            return null;

        await _remoteNodeRepository.TouchLastSeenAsync(nodeId, DateTimeOffset.UtcNow, null, ct);

        var claimed = await _syncJobRepository.ClaimPendingForNodeAsync(nodeId, ct);
        if (claimed is null)
            return new PollJobsResponse { Job = null };

        var dto = await _remoteSyncOrchestrator.BuildJobDtoAsync(claimed, accessToken, ct);
        if (dto is null)
        {
            await _syncJobRepository.UpdateStatusAsync(
                claimed.Id,
                Domain.Enums.SyncJobStatus.Failed,
                "Failed to build job payload.",
                ct);
            return new PollJobsResponse { Job = null };
        }

        return new PollJobsResponse { Job = dto };
    }

    public async Task<bool> FailJobAsync(string accessToken, Guid jobId, string errorMessage, CancellationToken ct = default)
    {
        var nodeId = await ValidateTokenAsync(accessToken, ct);
        if (nodeId is null)
            return false;

        var job = await _syncJobRepository.GetByIdAsync(jobId, ct);
        if (job is null || job.RemoteNodeId != nodeId.Value)
            return false;

        await _syncJobRepository.UpdateStatusAsync(jobId, Domain.Enums.SyncJobStatus.Failed, errorMessage, ct);
        _progressStore.Clear(jobId);
        return true;
    }

    public async Task<Guid?> ValidateTokenAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        await _agentSessionRepository.DeleteExpiredAsync(DateTimeOffset.UtcNow, ct);

        var session = await _agentSessionRepository.GetByTokenHashAsync(HashToken(accessToken), ct);
        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        return session.NodeId;
    }

    public static byte[] HashToken(string accessToken) =>
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(accessToken));
}
