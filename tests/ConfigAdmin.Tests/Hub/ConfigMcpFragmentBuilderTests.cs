using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.FileSystem;
using ConfigAdmin.Infrastructure.Repositories;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class ConfigMcpFragmentBuilderTests
{
    [Theory]
    [InlineData(@"C:\Program Files\1cv8\8.3.27.1688\bin\1cv8.exe", "8.3.27.1688")]
    [InlineData(@"D:\1C\8.3.24.1467\bin\1cv8.exe", "8.3.24.1467")]
    [InlineData(@"C:\no-version\1cv8.exe", "8.3.0.0")]
    public void ExtractPlatformVersion_ParsesVersionFromPath(string platformPath, string expected)
    {
        Assert.Equal(expected, ConfigMcpFragmentBuilder.ExtractPlatformVersion(platformPath));
    }

    [Fact]
    public async Task BuildForInstanceAsync_UsesExportIdWhenNoExplicitDatabaseLink()
    {
        var (builder, instanceId, projectId, exportId, clientId) = await CreateFixtureAsync();

        var fragment = await builder.BuildForInstanceAsync(instanceId, "ERP Project");

        var project = Assert.Single(fragment.RegistryFragment.Projects);
        Assert.Equal(projectId.ToString(), project.ProjectId);
        Assert.Equal(clientId.ToString(), project.ClientId);

        var database = Assert.Single(project.Databases);
        Assert.Equal(exportId.ToString(), database.InfobaseId);
        Assert.Equal("base", database.Type);
        Assert.EndsWith(
            Path.Combine("ClientA", "BaseERP", "Основная конфигурация"),
            database.SourcePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildForInstanceAsync_UsesExplicitDatabaseIdWhenLinked()
    {
        var existingDatabaseId = Guid.NewGuid();
        var (builder, instanceId, projectId, _, _) = await CreateFixtureAsync(existingDatabaseId);

        var fragment = await builder.BuildForInstanceAsync(instanceId, "ERP Project");

        var database = Assert.Single(fragment.RegistryFragment.Projects[0].Databases);
        Assert.Equal(existingDatabaseId.ToString(), database.InfobaseId);
    }

    [Fact]
    public async Task BuildForInstanceAsync_DatabaseNameUsesDisplayNameOnly()
    {
        var (builder, instanceId, _, _, _) = await CreateFixtureAsync();

        var fragment = await builder.BuildForInstanceAsync(instanceId, "ClientA / BaseERP");

        var database = Assert.Single(fragment.RegistryFragment.Projects[0].Databases);
        Assert.Equal("Основная конфигурация", database.Name);
    }

    private static async Task<(
        ConfigMcpFragmentBuilder Builder,
        Guid InstanceId,
        Guid ProjectId,
        Guid ExportId,
        Guid ClientId)> CreateFixtureAsync(Guid? explicitDatabaseId = null)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mcp-frag-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var exportId = Guid.NewGuid();

        var clientRepo = new ClientRepository(factory);
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "ClientA",
            ExportRootPath = @"D:\Exports"
        });

        var infobaseRepo = new InfobaseRepository(factory);
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "BaseERP",
            PlatformPath = @"C:\Program Files\1cv8\8.3.27.1688\bin\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = @"srv\erp"
        });

        var instanceRepo = new ConfigurationInstanceRepository(factory);
        await instanceRepo.SaveAsync(new ConfigurationInstance
        {
            Id = instanceId,
            InfobaseId = infobaseId,
            TemplateId = ConfigurationTemplateIds.SystemBaseTemplateId,
            Kind = ConfigurationKind.Base,
            DisplayName = "Основная конфигурация",
            ExportEnabled = true,
            SortOrder = 0,
            ConfigMcpProjectId = projectId,
            ConfigMcpDatabaseId = explicitDatabaseId
        });

        var exportRepo = new ConfigurationExportRepository(factory);
        await exportRepo.SaveAsync(new ConfigurationExport
        {
            Id = exportId,
            InstanceId = instanceId,
            IsCurrent = true
        });

        var configService = new InfobaseConfigurationService(
            new ConfigurationTemplateRepository(factory),
            instanceRepo,
            exportRepo,
            infobaseRepo);

        var builder = new ConfigMcpFragmentBuilder(
            new ExportPathBuilder(),
            clientRepo,
            infobaseRepo,
            instanceRepo,
            exportRepo,
            configService);

        return (builder, instanceId, projectId, exportId, clientId);
    }
}
