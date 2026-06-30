using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
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

    private static string CreateTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "configadmin.db");
    }
}
