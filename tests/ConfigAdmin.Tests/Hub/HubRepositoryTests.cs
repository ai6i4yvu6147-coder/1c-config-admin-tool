using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class HubRepositoryTests
{
    [Fact]
    public async Task ToolInstanceRepository_SavesAndLoadsByModuleId()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var repo = provider.GetRequiredService<IToolInstanceRepository>();

        var instance = new ToolInstanceProfile
        {
            Id = Guid.NewGuid(),
            ModuleId = "1c-config-mcp",
            RootPath = @"C:\1c_config_mcp_server_Portable",
            Enabled = true
        };

        await repo.SaveAsync(instance);

        var loaded = await repo.GetByModuleIdAsync("1c-config-mcp");
        Assert.NotNull(loaded);
        Assert.Equal(instance.RootPath, loaded!.RootPath);
        Assert.True(loaded.Enabled);
    }

    [Fact]
    public async Task InfobaseRepository_PersistsMcpLinkFields()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var clientRepo = provider.GetRequiredService<IClientRepository>();
        var projectRepo = provider.GetRequiredService<IHubProjectRepository>();
        var infobaseRepo = provider.GetRequiredService<IInfobaseRepository>();

        var clientId = Guid.NewGuid();
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "Client",
            ExportRootPath = @"D:\Exports"
        });

        var projectId = Guid.NewGuid();
        var mcpProjectId = Guid.NewGuid();
        await projectRepo.SaveAsync(new HubProjectProfile
        {
            Id = projectId,
            ClientId = clientId,
            Name = "ERP",
            Active = true
        });

        var infobaseId = Guid.NewGuid();
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "Base",
            PlatformPath = @"C:\1cv8\8.3.27.1688\bin\1cv8.exe",
            ConnectionType = Domain.Enums.ConnectionType.File,
            ConnectionString = @"C:\base",
            ProjectId = projectId,
            ConfigMcpProjectId = mcpProjectId
        });

        var loaded = await infobaseRepo.GetByIdAsync(infobaseId);
        Assert.NotNull(loaded);
        Assert.Equal(projectId, loaded!.ProjectId);
        Assert.Equal(mcpProjectId, loaded.ConfigMcpProjectId);
    }

    [Fact]
    public async Task ConfigurationInstanceRepository_PersistsMcpLinkFields()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var clientRepo = provider.GetRequiredService<IClientRepository>();
        var infobaseRepo = provider.GetRequiredService<IInfobaseRepository>();
        var instanceRepo = provider.GetRequiredService<IConfigurationInstanceRepository>();

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
            ConnectionType = Domain.Enums.ConnectionType.File,
            ConnectionString = @"C:\base"
        });

        var instanceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();
        await instanceRepo.SaveAsync(new ConfigurationInstance
        {
            Id = instanceId,
            InfobaseId = infobaseId,
            TemplateId = Domain.ConfigurationTemplateIds.SystemBaseTemplateId,
            Kind = Domain.Enums.ConfigurationKind.Base,
            DisplayName = "Основная",
            ExportEnabled = true,
            SortOrder = 0,
            ConfigMcpProjectId = projectId,
            ConfigMcpDatabaseId = databaseId
        });

        var loaded = await instanceRepo.GetByIdAsync(instanceId);
        Assert.NotNull(loaded);
        Assert.Equal(projectId, loaded!.ConfigMcpProjectId);
        Assert.Equal(databaseId, loaded.ConfigMcpDatabaseId);
    }

    [Fact]
    public async Task DatabaseInitializer_CreatesDataMcpTables()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
        await connection.OpenAsync();

        var settingsColumns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('data_mcp_settings') ORDER BY name")).ToList();
        Assert.Contains("tool_instance_id", settingsColumns);
        Assert.Contains("endpoint", settingsColumns);
        Assert.Contains("region", settingsColumns);
        Assert.Contains("bucket", settingsColumns);
        Assert.Contains("default_prefix", settingsColumns);
        Assert.Contains("sealed_secrets_path", settingsColumns);
        Assert.Contains("encrypted_dmcp_password", settingsColumns);

        var connectionColumns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('data_connections') ORDER BY name")).ToList();
        Assert.Contains("id", connectionColumns);
        Assert.Contains("infobase_id", connectionColumns);
        Assert.Contains("databaseid", connectionColumns);
        Assert.Contains("display_name", connectionColumns);
    }

    [Fact]
    public async Task DataMcpSettingsRepository_SavesAndLoads()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var toolRepo = provider.GetRequiredService<IToolInstanceRepository>();
        var settingsRepo = provider.GetRequiredService<IDataMcpSettingsRepository>();

        var toolInstanceId = Guid.NewGuid();
        await toolRepo.SaveAsync(new ToolInstanceProfile
        {
            Id = toolInstanceId,
            ModuleId = HubModuleIds.DataMcp,
            RootPath = @"C:\projects\1c-data-mcp_Portable",
            Enabled = true
        });

        var encryptedPassword = new byte[] { 1, 2, 3, 4, 5 };
        var settings = new DataMcpSettings
        {
            ToolInstanceId = toolInstanceId,
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "exchange1c",
            DefaultPrefix = "exchange",
            SealedSecretsPath = "credentials.sealed.json",
            EncryptedDmcpPassword = encryptedPassword
        };

        await settingsRepo.SaveAsync(settings);

        var byToolInstance = await settingsRepo.GetByToolInstanceIdAsync(toolInstanceId);
        Assert.NotNull(byToolInstance);
        Assert.Equal(settings.Endpoint, byToolInstance!.Endpoint);
        Assert.Equal(settings.Region, byToolInstance.Region);
        Assert.Equal(settings.Bucket, byToolInstance.Bucket);
        Assert.Equal(settings.DefaultPrefix, byToolInstance.DefaultPrefix);
        Assert.Equal(settings.SealedSecretsPath, byToolInstance.SealedSecretsPath);
        Assert.Equal(encryptedPassword, byToolInstance.EncryptedDmcpPassword);

        var byModule = await settingsRepo.GetByModuleIdAsync(HubModuleIds.DataMcp);
        Assert.NotNull(byModule);
        Assert.Equal(toolInstanceId, byModule!.ToolInstanceId);
    }

    [Fact]
    public async Task DataConnectionRepository_SavesAndLoadsByInfobase()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddInfrastructure(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var clientRepo = provider.GetRequiredService<IClientRepository>();
        var infobaseRepo = provider.GetRequiredService<IInfobaseRepository>();
        var connectionRepo = provider.GetRequiredService<IDataConnectionRepository>();

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
        var connection = new DataConnection
        {
            Id = connectionId,
            InfobaseId = infobaseId,
            DatabaseId = "a1b2c3d4",
            DisplayName = "Основная"
        };

        await connectionRepo.SaveAsync(connection);

        var loadedById = await connectionRepo.GetByIdAsync(connectionId);
        Assert.NotNull(loadedById);
        Assert.Equal(infobaseId, loadedById!.InfobaseId);
        Assert.Equal("a1b2c3d4", loadedById.DatabaseId);
        Assert.Equal("Основная", loadedById.DisplayName);

        var loadedByInfobase = await connectionRepo.GetByInfobaseIdAsync(infobaseId);
        Assert.NotNull(loadedByInfobase);
        Assert.Equal(connectionId, loadedByInfobase!.Id);

        var byClient = await connectionRepo.GetByClientIdAsync(clientId);
        Assert.Single(byClient);
        Assert.Equal(connectionId, byClient[0].Id);

        var duplicate = new DataConnection
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobaseId,
            DatabaseId = "deadbeef",
            DisplayName = "Duplicate"
        };

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => connectionRepo.SaveAsync(duplicate));
    }

    private static string CreateTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "configadmin.db");
    }
}
