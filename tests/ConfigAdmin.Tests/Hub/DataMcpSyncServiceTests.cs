using ConfigAdmin.Application;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Hub;
using ConfigAdmin.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class DataMcpSyncServiceTests
{
    [Fact]
    public async Task SyncPortableAsync_WhenPortableMissing_SkipsWithSuccess()
    {
        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var registry = provider.GetRequiredService<ManagedToolRegistryService>();
        var portableDir = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        await registry.SaveDataMcpRootPathAsync(portableDir);

        var service = CreateSyncService(provider, new FakeDataMcpToolClient());
        var result = await service.SyncPortableAsync(new DataMcpPortableSyncOptions());

        Assert.True(result.Success);
        Assert.True(result.Skipped);
    }

    [Fact]
    public async Task SyncPortableAsync_RunsApplyRegistryAndValidate()
    {
        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var portableDir = CreatePortableDir();
        var registry = provider.GetRequiredService<ManagedToolRegistryService>();
        await registry.SaveDataMcpRootPathAsync(portableDir);

        var tool = await registry.GetOrCreateDataMcpInstanceAsync();
        var settingsRepo = provider.GetRequiredService<IDataMcpSettingsRepository>();
        await settingsRepo.SaveAsync(new DataMcpSettings
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket-1",
            DefaultPrefix = "exchange",
            SealedSecretsPath = "credentials.sealed.json"
        }, CancellationToken.None);

        var fakeClient = new FakeDataMcpToolClient();
        var service = CreateSyncService(provider, fakeClient);
        var result = await service.SyncPortableAsync(new DataMcpPortableSyncOptions());

        Assert.True(result.Success);
        Assert.True(result.RegistryApplied);
        Assert.True(result.ConfigValid);
        Assert.Equal(1, fakeClient.ApplyRegistryCallCount);
        Assert.Equal(1, fakeClient.ValidateConfigCallCount);
        Assert.Equal(0, fakeClient.ApplySecretsCallCount);
    }

    [Fact]
    public async Task SyncPortableAsync_WithS3Keys_RunsApplySecretsFirst()
    {
        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var portableDir = CreatePortableDir();
        var registry = provider.GetRequiredService<ManagedToolRegistryService>();
        await registry.SaveDataMcpRootPathAsync(portableDir);

        var tool = await registry.GetOrCreateDataMcpInstanceAsync();
        var settingsRepo = provider.GetRequiredService<IDataMcpSettingsRepository>();
        var vault = provider.GetRequiredService<ISecretVault>();
        await settingsRepo.SaveAsync(new DataMcpSettings
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket-1",
            DefaultPrefix = "exchange",
            SealedSecretsPath = "credentials.sealed.json",
            EncryptedDmcpPassword = vault.Encrypt("dmcp-test-password")
        }, CancellationToken.None);

        var fakeClient = new FakeDataMcpToolClient();
        var service = CreateSyncService(provider, fakeClient);
        var result = await service.SyncPortableAsync(new DataMcpPortableSyncOptions
        {
            AccessKeyId = "YCAJTESTKEY",
            SecretAccessKey = "YCOSECRETTEST"
        });

        Assert.True(result.Success);
        Assert.True(result.SecretsApplied);
        Assert.Equal("C:\\portable\\credentials.sealed.json", result.CredentialsPath);
        Assert.Equal(1, fakeClient.ApplySecretsCallCount);
        Assert.Equal(1, fakeClient.ApplyRegistryCallCount);
    }

    [Fact]
    public async Task SyncPortableAsync_WhenApplyRegistryFails_ReturnsError()
    {
        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var portableDir = CreatePortableDir();
        var registry = provider.GetRequiredService<ManagedToolRegistryService>();
        await registry.SaveDataMcpRootPathAsync(portableDir);

        var tool = await registry.GetOrCreateDataMcpInstanceAsync();
        var settingsRepo = provider.GetRequiredService<IDataMcpSettingsRepository>();
        await settingsRepo.SaveAsync(new DataMcpSettings
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket-1"
        }, CancellationToken.None);

        var fakeClient = new FakeDataMcpToolClient { RegistrySuccess = false };
        var service = CreateSyncService(provider, fakeClient);
        var result = await service.SyncPortableAsync(new DataMcpPortableSyncOptions());

        Assert.False(result.Success);
        Assert.Contains("apply-registry", result.Message);
    }

    private static DataMcpSyncService CreateSyncService(ServiceProvider provider, IDataMcpToolClient toolClient) =>
        new(
            provider.GetRequiredService<ManagedToolRegistryService>(),
            provider.GetRequiredService<IDataMcpSettingsRepository>(),
            provider.GetRequiredService<IDataConnectionRepository>(),
            provider.GetRequiredService<DataMcpFragmentBuilder>(),
            toolClient,
            provider.GetRequiredService<ISecretVault>(),
            NullLogger<DataMcpSyncService>.Instance);

    private static string CreatePortableDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<ServiceProvider> CreateProviderAsync()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddConfigAdminApplication(dbPath);
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        return provider;
    }

    private static async Task InitializeVaultAsync(ServiceProvider provider)
    {
        var vault = provider.GetRequiredService<ISecretVault>();
        await vault.InitializeAsync("master-password");
        await provider.GetRequiredService<VaultSessionService>().UnlockAsync("master-password");
    }

    private static string CreateTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "configadmin.db");
    }

    private sealed class FakeDataMcpToolClient : IDataMcpToolClient
    {
        public int ApplySecretsCallCount { get; private set; }
        public int ApplyRegistryCallCount { get; private set; }
        public int ValidateConfigCallCount { get; private set; }
        public bool RegistrySuccess { get; init; } = true;

        public Task<DataMcpInventoryResponse> GetInventoryAsync(CancellationToken ct = default) =>
            Task.FromResult(new DataMcpInventoryResponse());

        public Task<DataMcpStatusResponse> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new DataMcpStatusResponse());

        public Task<(DataMcpApplySecretsResponse Response, JsonCliResult Raw)> ApplySecretsAsync(
            DataMcpApplySecretsInput input,
            string dmcpPassword,
            CancellationToken ct = default)
        {
            ApplySecretsCallCount++;
            return Task.FromResult((
                new DataMcpApplySecretsResponse
                {
                    Success = true,
                    CredentialsPath = @"C:\portable\credentials.sealed.json"
                },
                new JsonCliResult { ExitCode = 0 }));
        }

        public Task<(ConfigMcpApplyRegistryResponse Response, JsonCliResult Raw)> ApplyRegistryAsync(
            DataMcpRegistryFragmentDocument fragment,
            CancellationToken ct = default)
        {
            ApplyRegistryCallCount++;
            return Task.FromResult((
                new ConfigMcpApplyRegistryResponse
                {
                    Success = RegistrySuccess,
                    Errors = RegistrySuccess ? [] : ["registry failed"]
                },
                new JsonCliResult { ExitCode = RegistrySuccess ? 0 : 1 }));
        }

        public Task<(DataMcpValidateConfigResponse Response, JsonCliResult Raw)> ValidateConfigAsync(
            string? dmcpPassword = null,
            CancellationToken ct = default)
        {
            ValidateConfigCallCount++;
            return Task.FromResult((
                new DataMcpValidateConfigResponse { Valid = true },
                new JsonCliResult { ExitCode = 0 }));
        }
    }
}
