using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConfigAdmin.Application;
using ConfigAdmin.Application.RemoteSync;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigAdmin.Tests;

public class SyncUploadIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task UploadSession_ChunkAndComplete_AppliesToExportRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sync-upload-{Guid.NewGuid():N}");
        var exportRoot = Path.Combine(tempRoot, "exports");
        var syncRoot = Path.Combine(tempRoot, "sync");
        Directory.CreateDirectory(exportRoot);

        var dbPath = Path.Combine(tempRoot, "test.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfigAdminApplication(dbPath);
        services.AddSingleton(new SyncUploadSessionStore(syncRoot));

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var clientRepo = provider.GetRequiredService<IClientRepository>();
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "TestClient",
            ExportRootPath = exportRoot
        });

        var pairing = provider.GetRequiredService<PairingSecretService>();
        const string password = "pair-secret";
        var nodeRepo = provider.GetRequiredService<IRemoteNodeRepository>();
        await nodeRepo.SaveAsync(new RemoteNodeProfile
        {
            Id = nodeId,
            ClientId = clientId,
            Name = "RDP",
            PairingSecretVerifier = pairing.CreateVerifier(password),
            Enabled = true,
            LastSeenAt = DateTimeOffset.UtcNow
        });

        var infobaseRepo = provider.GetRequiredService<IInfobaseRepository>();
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "TestBase",
            PlatformPath = "C:\\Platform\\1cv8.exe",
            ConnectionType = ConnectionType.File,
            ConnectionString = "C:\\Bases\\Test",
            ExportLocation = ExportLocation.Remote,
            RemoteNodeId = nodeId,
            ExportConfiguration = true
        });

        var syncJobRepo = provider.GetRequiredService<ISyncJobRepository>();
        await syncJobRepo.SaveAsync(new SyncJobProfile
        {
            Id = jobId,
            InfobaseId = infobaseId,
            RemoteNodeId = nodeId,
            Status = SyncJobStatus.Claimed,
            RequestedAt = DateTimeOffset.UtcNow
        });

        var options = provider.GetRequiredService<SyncReceiverOptions>();
        var port = Random.Shared.Next(48000, 58000);
        options.ListenUrl = $"http://127.0.0.1:{port}";
        var host = provider.GetRequiredService<SyncReceiverHost>();
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
            var register = await registerResponse.Content.ReadFromJsonAsync<RegisterAgentResponse>(JsonOptions);
            Assert.NotNull(register);

            var zipBytes = CreateTestZip("hello.txt", "remote-sync-ok");
            var sha256 = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();
            const int chunkSize = 1024;

            using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync-upload/sessions")
            {
                Content = JsonContent.Create(new CreateUploadSessionRequest
                {
                    JobId = jobId,
                    FileName = "configuration.zip",
                    TotalBytes = zipBytes.Length,
                    Sha256 = sha256,
                    ChunkSizeBytes = chunkSize
                }, options: JsonOptions)
            };
            createRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", register!.AccessToken);

            var createResponse = await http.SendAsync(createRequest);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var session = await createResponse.Content.ReadFromJsonAsync<CreateUploadSessionResponse>(JsonOptions);
            Assert.NotNull(session);

            using var chunkRequest = new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/sync-upload/sessions/{session!.SessionId:D}/chunks/0")
            {
                Content = new ByteArrayContent(zipBytes)
            };
            chunkRequest.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            chunkRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", register.AccessToken);

            var chunkResponse = await http.SendAsync(chunkRequest);
            Assert.Equal(HttpStatusCode.OK, chunkResponse.StatusCode);

            using var completeRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/sync-upload/sessions/{session.SessionId:D}/complete");
            completeRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", register.AccessToken);

            var completeResponse = await http.SendAsync(completeRequest);
            Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
            var complete = await completeResponse.Content.ReadFromJsonAsync<CompleteUploadResponse>(JsonOptions);
            Assert.NotNull(complete);
            Assert.True(complete!.Success);
            Assert.Equal("Completed", complete.JobStatus);

            var appliedFile = Path.Combine(
                exportRoot,
                "TestClient",
                "TestBase",
                "Основная конфигурация",
                "hello.txt");
            Assert.True(File.Exists(appliedFile));
            Assert.Equal("remote-sync-ok", await File.ReadAllTextAsync(appliedFile, Encoding.UTF8));

            var job = await syncJobRepo.GetByIdAsync(jobId);
            Assert.Equal(SyncJobStatus.Completed, job!.Status);
        }
        finally
        {
            await host.StopAsync();
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    private static byte[] CreateTestZip(string entryName, string content)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        return ms.ToArray();
    }
}
