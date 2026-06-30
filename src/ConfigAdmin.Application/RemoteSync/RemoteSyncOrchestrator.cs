using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Application.Services;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class RemoteSyncOrchestrator
{
    private readonly ISyncJobRepository _syncJobRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IRemoteNodeRepository _remoteNodeRepository;
    private readonly IConfigurationInstanceRepository _instanceRepository;
    private readonly InfobaseConfigurationService _configurationService;
    private readonly ISecretVault _secretVault;
    private readonly SyncJobProgressStore _progressStore;

    public RemoteSyncOrchestrator(
        ISyncJobRepository syncJobRepository,
        IInfobaseRepository infobaseRepository,
        IRemoteNodeRepository remoteNodeRepository,
        IConfigurationInstanceRepository instanceRepository,
        InfobaseConfigurationService configurationService,
        ISecretVault secretVault,
        SyncJobProgressStore progressStore)
    {
        _syncJobRepository = syncJobRepository;
        _infobaseRepository = infobaseRepository;
        _remoteNodeRepository = remoteNodeRepository;
        _instanceRepository = instanceRepository;
        _configurationService = configurationService;
        _secretVault = secretVault;
        _progressStore = progressStore;
    }

    public async Task<IReadOnlyList<SyncJobProfile>> RequestSyncAsync(
        Guid infobaseId,
        bool syncMcpAfterComplete,
        CancellationToken ct = default)
    {
        if (!_secretVault.IsUnlocked)
            throw new InvalidOperationException("Разблокируйте vault перед синхронизацией с RDP.");

        var profile = await _infobaseRepository.GetByIdAsync(infobaseId, ct)
            ?? throw new InvalidOperationException("База не найдена.");

        if (profile.ExportLocation != ExportLocation.Remote)
            throw new InvalidOperationException("База не помечена как Remote.");

        if (profile.RemoteNodeId is not Guid nodeId)
            throw new InvalidOperationException("Не выбран RDP-узел.");

        var node = await _remoteNodeRepository.GetByIdAsync(nodeId, ct)
            ?? throw new InvalidOperationException("RDP-узел не найден.");

        if (!node.Enabled)
            throw new InvalidOperationException("RDP-узел отключён.");

        if (node.LastSeenAt is null ||
            DateTimeOffset.UtcNow - node.LastSeenAt.Value.ToUniversalTime() >
            TimeSpan.FromSeconds(SyncAgentHubService.DefaultPollIntervalMs * 2))
        {
            throw new InvalidOperationException(
                $"RDP-узел offline (last seen: {(node.LastSeenAt?.ToLocalTime().ToString("G") ?? "never")}).");
        }

        var active = await _syncJobRepository.GetActiveForInfobaseAsync(infobaseId, ct);
        if (active is not null)
            throw new InvalidOperationException("Синхронизация уже выполняется для этой базы.");

        var plan = await _configurationService.BuildExportPlanAsync(infobaseId, ct);
        if (!plan.HasWork)
            throw new InvalidOperationException("План выгрузки пуст — включите конфигурации на базе.");

        var jobs = new List<SyncJobProfile>();
        foreach (var instancePlan in plan.Instances)
        {
            var job = new SyncJobProfile
            {
                Id = Guid.NewGuid(),
                InfobaseId = infobaseId,
                RemoteNodeId = nodeId,
                ConfigurationInstanceId = instancePlan.InstanceId,
                Status = SyncJobStatus.Pending,
                RequestedAt = DateTimeOffset.UtcNow,
                SyncMcpAfterComplete = syncMcpAfterComplete
            };

            await _syncJobRepository.SaveAsync(job, ct);
            jobs.Add(job);
        }

        return jobs;
    }

    public async Task<SyncJobProfile?> GetJobAsync(Guid jobId, CancellationToken ct = default) =>
        await _syncJobRepository.GetByIdAsync(jobId, ct);

    public SyncJobProgressStore.JobProgressEntry? GetJobProgress(Guid jobId) =>
        _progressStore.Get(jobId);

    public async Task<SyncJobDto?> BuildJobDtoAsync(
        SyncJobProfile job,
        string accessToken,
        CancellationToken ct = default)
    {
        var profile = await _infobaseRepository.GetByIdAsync(job.InfobaseId, ct);
        if (profile is null)
            return null;

        if (job.ConfigurationInstanceId is not Guid instanceId)
            return null;

        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct);
        if (instance is null)
            return null;

        byte[]? encryptedPassword = null;
        if (profile.EncryptedPassword is { Length: > 0 })
        {
            var plain = _secretVault.Decrypt(profile.EncryptedPassword);
            encryptedPassword = JobCredentialsCipher.Encrypt(
                accessToken, job.Id, job.RemoteNodeId, plain);
        }

        return new SyncJobDto
        {
            JobId = job.Id,
            InfobaseId = job.InfobaseId,
            NodeId = job.RemoteNodeId,
            Action = "exportAndUpload",
            RemoteExportPath = profile.RemoteExportPath ?? string.Empty,
            Packaging = "zip",
            MaxChunkSizeBytes = SyncUploadSessionStore.DefaultChunkSizeBytes,
            Export = new ExportJobSpec
            {
                InstanceId = instance.Id,
                Kind = instance.Kind,
                DisplayName = instance.DisplayName,
                DesignerName = instance.DesignerName,
                PlatformPath = profile.PlatformPath,
                ConnectionType = profile.ConnectionType,
                ConnectionString = profile.ConnectionString,
                Username = profile.Username,
                ExportFormat = profile.ExportFormat
            },
            EncryptedConnectionPassword = encryptedPassword
        };
    }
}
