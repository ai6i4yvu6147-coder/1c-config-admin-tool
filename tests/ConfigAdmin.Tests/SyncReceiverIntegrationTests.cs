using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConfigAdmin.Application;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ConfigAdmin.Tests;

public class SyncReceiverIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Register_Heartbeat_PollJobs_EndToEnd()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sync-recv-test-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfigAdminApplication(dbPath);

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var clientRepo = provider.GetRequiredService<IClientRepository>();
        var clientId = Guid.NewGuid();
        await clientRepo.SaveAsync(new ConfigAdmin.Domain.Models.ClientProfile
        {
            Id = clientId,
            Name = "Client",
            ExportRootPath = "C:\\Exports"
        });

        var pairing = provider.GetRequiredService<PairingSecretService>();
        const string password = "pair-secret";
        var nodeId = Guid.NewGuid();
        var nodeRepo = provider.GetRequiredService<IRemoteNodeRepository>();
        await nodeRepo.SaveAsync(new ConfigAdmin.Domain.Models.RemoteNodeProfile
        {
            Id = nodeId,
            ClientId = clientId,
            Name = "RDP",
            PairingSecretVerifier = pairing.CreateVerifier(password),
            Enabled = true
        });

        var options = provider.GetRequiredService<SyncReceiverOptions>();
        options.ListenUrl = "http://127.0.0.1:0";
        var host = provider.GetRequiredService<SyncReceiverHost>();

        // Bind ephemeral port via 0 - actually UseSetting with port 0 may not work.
        // Use fixed localhost port with retry - use random high port
        var port = Random.Shared.Next(48000, 58000);
        options.ListenUrl = $"http://127.0.0.1:{port}";
        await host.StartAsync();

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var registerResponse = await http.PostAsJsonAsync("/api/sync-agent/register", new RegisterAgentRequest
            {
                NodeId = nodeId,
                PairingPassword = password,
                AgentVersion = "test",
                MachineName = "LOCAL"
            }, JsonOptions);

            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
            var register = await registerResponse.Content.ReadFromJsonAsync<RegisterAgentResponse>(JsonOptions);
            Assert.NotNull(register);
            Assert.False(string.IsNullOrWhiteSpace(register!.AccessToken));

            using var heartbeatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync-agent/heartbeat")
            {
                Content = JsonContent.Create(new HeartbeatRequest
                {
                    NodeId = nodeId,
                    Status = "idle"
                }, options: JsonOptions)
            };
            heartbeatRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", register.AccessToken);

            var heartbeatResponse = await http.SendAsync(heartbeatRequest);
            Assert.Equal(HttpStatusCode.OK, heartbeatResponse.StatusCode);

            using var jobsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/sync-agent/jobs?nodeId={nodeId:D}");
            jobsRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", register.AccessToken);

            var jobsResponse = await http.SendAsync(jobsRequest);
            Assert.Equal(HttpStatusCode.OK, jobsResponse.StatusCode);
            var jobs = await jobsResponse.Content.ReadFromJsonAsync<PollJobsResponse>(JsonOptions);
            Assert.NotNull(jobs);
            Assert.Null(jobs!.Job);

            var updatedNode = await nodeRepo.GetByIdAsync(nodeId);
            Assert.NotNull(updatedNode?.LastSeenAt);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
