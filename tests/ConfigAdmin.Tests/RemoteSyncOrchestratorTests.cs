using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Infrastructure.Security;
using Xunit;

namespace ConfigAdmin.Tests;

public class RemoteSyncOrchestratorTests
{
    [Fact]
    public async Task RequestSync_VaultLocked_Throws()
    {
        var (orchestrator, infobaseId, _) = await CreateOrchestratorAsync(vaultUnlocked: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.RequestSyncAsync(infobaseId, syncMcpAfterComplete: false));

        Assert.Contains("vault", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestSync_VaultUnlocked_CreatesPendingJob()
    {
        var (orchestrator, infobaseId, syncJobRepo) = await CreateOrchestratorAsync(vaultUnlocked: true);

        var job = await orchestrator.RequestSyncAsync(infobaseId, syncMcpAfterComplete: false);

        Assert.Equal(SyncJobStatus.Pending, job.Status);
        var stored = await syncJobRepo.GetByIdAsync(job.Id);
        Assert.NotNull(stored);
        Assert.Equal(SyncJobStatus.Pending, stored!.Status);
    }

    [Fact]
    public async Task BuildJobDto_WithUnlockedVault_ReturnsEncryptedPassword()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"orch-test-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var vault = new SecretVault(new VaultMetaRepository(factory));
        await vault.InitializeAsync("master");
        var encryptedBasePwd = vault.Encrypt("infobase-secret");

        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var clientRepo = new ClientRepository(factory);
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "C",
            ExportRootPath = "C:\\Exports"
        });

        var nodeRepo = new RemoteNodeRepository(factory);
        await nodeRepo.SaveAsync(new RemoteNodeProfile
        {
            Id = nodeId,
            ClientId = clientId,
            Name = "RDP",
            PairingSecretVerifier = new PairingSecretService().CreateVerifier("p"),
            Enabled = true,
            LastSeenAt = DateTimeOffset.UtcNow
        });

        var infobaseRepo = new InfobaseRepository(factory);
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "B",
            PlatformPath = "C:\\1cv8.exe",
            ConnectionType = ConnectionType.File,
            ConnectionString = "C:\\Base",
            ExportLocation = ExportLocation.Remote,
            RemoteNodeId = nodeId,
            EncryptedPassword = encryptedBasePwd,
            ExportConfiguration = true
        });

        var syncJobRepo = new SyncJobRepository(factory);
        var jobId = Guid.NewGuid();
        await syncJobRepo.SaveAsync(new SyncJobProfile
        {
            Id = jobId,
            InfobaseId = infobaseId,
            RemoteNodeId = nodeId,
            Status = SyncJobStatus.Pending,
            RequestedAt = DateTimeOffset.UtcNow
        });

        var orchestrator = new RemoteSyncOrchestrator(syncJobRepo, infobaseRepo, nodeRepo, vault, new SyncJobProgressStore());
        const string accessToken = "test-agent-token";
        var job = await syncJobRepo.GetByIdAsync(jobId);
        var dto = await orchestrator.BuildJobDtoAsync(job!, accessToken);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.EncryptedConnectionPassword);
        var plain = JobCredentialsCipher.Decrypt(
            accessToken, jobId, nodeId, dto.EncryptedConnectionPassword);
        Assert.Equal("infobase-secret", plain);
    }

    private static async Task<(RemoteSyncOrchestrator Orchestrator, Guid InfobaseId, SyncJobRepository SyncJobRepo)>
        CreateOrchestratorAsync(bool vaultUnlocked)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"orch-test-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var vault = new SecretVault(new VaultMetaRepository(factory));
        await vault.InitializeAsync("master");
        if (!vaultUnlocked)
            vault.Lock();
        else
            await vault.UnlockAsync("master");

        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var clientRepo = new ClientRepository(factory);
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "C",
            ExportRootPath = "C:\\Exports"
        });

        var nodeRepo = new RemoteNodeRepository(factory);
        await nodeRepo.SaveAsync(new RemoteNodeProfile
        {
            Id = nodeId,
            ClientId = clientId,
            Name = "RDP",
            PairingSecretVerifier = new PairingSecretService().CreateVerifier("p"),
            Enabled = true,
            LastSeenAt = DateTimeOffset.UtcNow
        });

        var infobaseRepo = new InfobaseRepository(factory);
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "B",
            PlatformPath = "C:\\1cv8.exe",
            ConnectionType = ConnectionType.File,
            ConnectionString = "C:\\Base",
            ExportLocation = ExportLocation.Remote,
            RemoteNodeId = nodeId,
            ExportConfiguration = true
        });

        var syncJobRepo = new SyncJobRepository(factory);
        var progressStore = new SyncJobProgressStore();
        var orchestrator = new RemoteSyncOrchestrator(syncJobRepo, infobaseRepo, nodeRepo, vault, progressStore);
        return (orchestrator, infobaseId, syncJobRepo);
    }
}
