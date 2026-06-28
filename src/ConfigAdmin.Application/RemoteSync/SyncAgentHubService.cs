using System.Security.Cryptography;
using System.Text;
using ConfigAdmin.Domain.Models;
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
    private readonly PairingSecretService _pairingSecretService;
    private readonly ILogger<SyncAgentHubService> _logger;

    public SyncAgentHubService(
        IRemoteNodeRepository remoteNodeRepository,
        IAgentSessionRepository agentSessionRepository,
        PairingSecretService pairingSecretService,
        ILogger<SyncAgentHubService> logger)
    {
        _remoteNodeRepository = remoteNodeRepository;
        _agentSessionRepository = agentSessionRepository;
        _pairingSecretService = pairingSecretService;
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

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await _agentSessionRepository.SaveAsync(new AgentSession
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
        return true;
    }

    public async Task<PollJobsResponse?> PollJobsAsync(string accessToken, Guid nodeId, CancellationToken ct = default)
    {
        var validatedNodeId = await ValidateTokenAsync(accessToken, ct);
        if (validatedNodeId is null || validatedNodeId.Value != nodeId)
            return null;

        await _remoteNodeRepository.TouchLastSeenAsync(nodeId, DateTimeOffset.UtcNow, null, ct);
        return new PollJobsResponse { Job = null };
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
        SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
}
