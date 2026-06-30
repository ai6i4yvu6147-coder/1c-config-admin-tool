using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConfigAdmin.Tests;

public class SyncAgentHubServiceTests
{
    [Fact]
    public async Task Register_WithValidPairing_ReturnsToken()
    {
        var (service, nodeId, password) = await CreateServiceWithNodeAsync();

        var response = await service.RegisterAsync(new RegisterAgentRequest
        {
            NodeId = nodeId,
            PairingPassword = password,
            AgentVersion = "test",
            MachineName = "TEST"
        });

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response!.AccessToken));
    }

    [Fact]
    public async Task Register_WithWrongPassword_ReturnsNull()
    {
        var (service, nodeId, _) = await CreateServiceWithNodeAsync();

        var response = await service.RegisterAsync(new RegisterAgentRequest
        {
            NodeId = nodeId,
            PairingPassword = "wrong",
            AgentVersion = "test",
            MachineName = "TEST"
        });

        Assert.Null(response);
    }

    [Fact]
    public async Task Heartbeat_WithInvalidToken_ReturnsFalse()
    {
        var (service, nodeId, _) = await CreateServiceWithNodeAsync();

        var ok = await service.HeartbeatAsync("invalid-token", new HeartbeatRequest
        {
            NodeId = nodeId,
            Status = "idle"
        });

        Assert.False(ok);
    }

    [Fact]
    public async Task PollJobs_WithValidToken_ReturnsNullJob()
    {
        var (service, nodeId, password) = await CreateServiceWithNodeAsync();
        var register = await service.RegisterAsync(new RegisterAgentRequest
        {
            NodeId = nodeId,
            PairingPassword = password,
            AgentVersion = "test",
            MachineName = "TEST"
        });

        var response = await service.PollJobsAsync(register!.AccessToken, nodeId);

        Assert.NotNull(response);
        Assert.Null(response!.Job);
    }

    [Fact]
    public async Task Heartbeat_WithExportingStatus_UpdatesJobAndProgress()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sync-hub-hb-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var (service, nodeId, password) = await CreateServiceWithNodeAsync(factory);
        var register = await service.RegisterAsync(new RegisterAgentRequest
        {
            NodeId = nodeId,
            PairingPassword = password,
            AgentVersion = "test",
            MachineName = "TEST"
        });
        Assert.NotNull(register);

        var jobId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var clientRepo = new ClientRepository(factory);
        await clientRepo.SaveAsync(new ConfigAdmin.Domain.Models.ClientProfile
        {
            Id = clientId,
            Name = "C",
            ExportRootPath = "C:\\Exports"
        });
        var infobaseRepo = new InfobaseRepository(factory);
        await infobaseRepo.SaveAsync(new ConfigAdmin.Domain.Models.InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "B",
            PlatformPath = "C:\\1cv8.exe",
            ConnectionType = ConfigAdmin.Domain.Enums.ConnectionType.File,
            ConnectionString = "C:\\Base",
            ExportLocation = ConfigAdmin.Domain.Enums.ExportLocation.Remote,
            RemoteNodeId = nodeId,
            ExportConfiguration = true
        });

        var syncJobRepo = new SyncJobRepository(factory);
        await syncJobRepo.SaveAsync(new ConfigAdmin.Domain.Models.SyncJobProfile
        {
            Id = jobId,
            InfobaseId = infobaseId,
            RemoteNodeId = nodeId,
            Status = ConfigAdmin.Domain.Enums.SyncJobStatus.Claimed,
            RequestedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        });

        var ok = await service.HeartbeatAsync(register!.AccessToken, new HeartbeatRequest
        {
            NodeId = nodeId,
            Status = "exporting",
            CurrentJobId = jobId,
            ProgressMessage = "Выгрузка конфигурации 1С (DumpConfigToFiles)…"
        });
        Assert.True(ok);

        var job = await syncJobRepo.GetByIdAsync(jobId);
        Assert.Equal(ConfigAdmin.Domain.Enums.SyncJobStatus.Exporting, job!.Status);
    }

    private static async Task<(SyncAgentHubService Service, Guid NodeId, string Password)> CreateServiceWithNodeAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sync-hub-test-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();
        return await CreateServiceWithNodeAsync(factory);
    }

    private static async Task<(SyncAgentHubService Service, Guid NodeId, string Password)> CreateServiceWithNodeAsync(
        SqliteConnectionFactory factory)
    {
        var clientRepo = new ClientRepository(factory);
        var clientId = Guid.NewGuid();
        await clientRepo.SaveAsync(new ConfigAdmin.Domain.Models.ClientProfile
        {
            Id = clientId,
            Name = "TestClient",
            ExportRootPath = "C:\\Exports"
        });

        var pairing = new PairingSecretService();
        const string password = "pair-123";
        var nodeRepo = new RemoteNodeRepository(factory);
        var nodeId = Guid.NewGuid();
        await nodeRepo.SaveAsync(new ConfigAdmin.Domain.Models.RemoteNodeProfile
        {
            Id = nodeId,
            ClientId = clientId,
            Name = "RDP-1",
            PairingSecretVerifier = pairing.CreateVerifier(password),
            Enabled = true
        });

        var progressStore = new SyncJobProgressStore();
        var instanceRepo = new ConfigurationInstanceRepository(factory);
        var infobaseRepo = new InfobaseRepository(factory);
        var configService = new ConfigAdmin.Application.Services.InfobaseConfigurationService(
            new ConfigurationTemplateRepository(factory),
            instanceRepo,
            new ConfigurationExportRepository(factory),
            infobaseRepo);
        var orchestrator = new RemoteSyncOrchestrator(
            new SyncJobRepository(factory),
            infobaseRepo,
            nodeRepo,
            instanceRepo,
            configService,
            new SecretVault(new VaultMetaRepository(factory)),
            progressStore);

        var service = new SyncAgentHubService(
            nodeRepo,
            new AgentSessionRepository(factory),
            new SyncJobRepository(factory),
            orchestrator,
            pairing,
            progressStore,
            NullLogger<SyncAgentHubService>.Instance);

        return (service, nodeId, password);
    }
}
