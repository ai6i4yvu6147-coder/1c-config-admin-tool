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

    private static async Task<(SyncAgentHubService Service, Guid NodeId, string Password)> CreateServiceWithNodeAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sync-hub-test-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

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

        var service = new SyncAgentHubService(
            nodeRepo,
            new AgentSessionRepository(factory),
            pairing,
            NullLogger<SyncAgentHubService>.Instance);

        return (service, nodeId, password);
    }
}
