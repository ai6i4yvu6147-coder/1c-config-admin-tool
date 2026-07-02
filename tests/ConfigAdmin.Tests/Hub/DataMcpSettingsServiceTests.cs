using ConfigAdmin.Application;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class DataMcpSettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_WhenVaultLocked_Throws()
    {
        var provider = await CreateProviderAsync();
        var service = provider.GetRequiredService<DataMcpSettingsService>();
        var tool = await provider.GetRequiredService<ManagedToolRegistryService>().GetOrCreateDataMcpInstanceAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new DataMcpSettingsSaveRequest
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket"
        }));
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsAndEncryptsPassword()
    {
        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var service = provider.GetRequiredService<DataMcpSettingsService>();
        var settingsRepo = provider.GetRequiredService<IDataMcpSettingsRepository>();
        var tool = await provider.GetRequiredService<ManagedToolRegistryService>().GetOrCreateDataMcpInstanceAsync();

        await service.SaveAsync(new DataMcpSettingsSaveRequest
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "exchange1c",
            DefaultPrefix = "exchange",
            DmcpPassword = "dmcp-test-password"
        });

        var stored = await settingsRepo.GetByToolInstanceIdAsync(tool.Id);
        Assert.NotNull(stored);
        Assert.Equal("exchange1c", stored!.Bucket);
        Assert.NotNull(stored.EncryptedDmcpPassword);
        Assert.NotEmpty(stored.EncryptedDmcpPassword);
    }

    [Fact]
    public async Task SaveAsync_UpsertsAndRemovesConnections()
    {
        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var clientRepo = provider.GetRequiredService<IClientRepository>();
        var infobaseRepo = provider.GetRequiredService<IInfobaseRepository>();
        var connectionRepo = provider.GetRequiredService<IDataConnectionRepository>();
        var service = provider.GetRequiredService<DataMcpSettingsService>();
        var tool = await provider.GetRequiredService<ManagedToolRegistryService>().GetOrCreateDataMcpInstanceAsync();

        var clientId = Guid.NewGuid();
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "Client",
            ExportRootPath = @"D:\Exports"
        });

        var infobaseId = Guid.NewGuid();
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "Base",
            PlatformPath = @"C:\1cv8\8.3.27.1688\bin\1cv8.exe",
            ConnectionType = ConnectionType.File,
            ConnectionString = @"C:\base"
        });

        var connectionId = Guid.NewGuid();
        await service.SaveAsync(new DataMcpSettingsSaveRequest
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket",
            Connections =
            [
                new DataMcpConnectionItem
                {
                    InfobaseId = infobaseId,
                    ConnectionId = connectionId,
                    DatabaseId = "a1b2c3d4",
                    DisplayName = "Основная"
                }
            ]
        });

        var saved = await connectionRepo.GetByInfobaseIdAsync(infobaseId);
        Assert.NotNull(saved);
        Assert.Equal("a1b2c3d4", saved!.DatabaseId);

        await service.SaveAsync(new DataMcpSettingsSaveRequest
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket",
            Connections =
            [
                new DataMcpConnectionItem
                {
                    InfobaseId = infobaseId,
                    ConnectionId = connectionId,
                    DatabaseId = string.Empty,
                    DisplayName = "Основная"
                }
            ]
        });

        var removed = await connectionRepo.GetByInfobaseIdAsync(infobaseId);
        Assert.Null(removed);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsWithoutPortableWrite()
    {
        var portableDir = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(portableDir);

        var provider = await CreateProviderAsync();
        await InitializeVaultAsync(provider);

        var registry = provider.GetRequiredService<ManagedToolRegistryService>();
        var tool = await registry.GetOrCreateDataMcpInstanceAsync();
        await registry.SaveDataMcpRootPathAsync(portableDir);

        var service = provider.GetRequiredService<DataMcpSettingsService>();
        var sealedService = provider.GetRequiredService<DataMcpSealedCredentialsService>();

        await service.SaveAsync(new DataMcpSettingsSaveRequest
        {
            ToolInstanceId = tool.Id,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "exchange1c",
            DmcpPassword = "dmcp-test-password",
            AccessKeyId = "YCAJTESTKEY",
            SecretAccessKey = "YCOSECRETTEST"
        });

        Assert.False(sealedService.CredentialsFileExists(portableDir, "credentials.sealed.json"));
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
}
