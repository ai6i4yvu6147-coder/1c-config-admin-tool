using ConfigAdmin.Application;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class InfobaseContextServiceTests
{
    [Fact]
    public async Task ResolveAsync_ByName_ReturnsRefsWithoutSecrets()
    {
        var provider = await CreateProviderAsync();
        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await SeedAsync(provider, clientId, infobaseId, projectId, instanceId, exportId, connectionId);

        var dataMcp = new Mock<IDataMcpToolClient>();
        dataMcp.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataMcpStatusResponse
            {
                Details = new DataMcpStatusDetailsDto
                {
                    CredentialsExists = true,
                    CredentialsResolvable = false
                }
            });

        var configMcp = new Mock<IConfigMcpToolClient>();
        configMcp.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigMcpStatusResponse
            {
                Projects =
                [
                    new ConfigMcpStatusProjectDto
                    {
                        ProjectId = projectId.ToString(),
                        Name = "Romashka / ERP Prod",
                        Databases =
                        [
                            new ConfigMcpStatusDatabaseDto
                            {
                                InfobaseId = exportId.ToString(),
                                Name = "Main configuration",
                                Type = "base"
                            }
                        ]
                    }
                ]
            });

        var service = CreateService(provider, configMcp.Object, dataMcp.Object);
        var context = await service.ResolveAsync(null, "ERP Prod");

        Assert.Equal(infobaseId.ToString(), context.InfobaseId);
        Assert.Equal("ERP Prod", context.InfobaseName);
        Assert.Equal("Romashka", context.ClientName);
        Assert.NotNull(context.ConfigMcp);
        Assert.Equal(projectId.ToString(), context.ConfigMcp!.ProjectId);
        Assert.Equal("Romashka / ERP Prod", context.ConfigMcp.ProjectFilter);
        Assert.Equal("Romashka / ERP Prod", context.ConfigMcp.ProjectName);
        Assert.Single(context.ConfigMcp.Instances);
        Assert.Equal(exportId.ToString(), context.ConfigMcp.Instances[0].DatabaseId);
        Assert.Equal("Main configuration", context.ConfigMcp.Instances[0].DisplayName);
        Assert.Equal("Main configuration", context.ConfigMcp.Instances[0].ExtensionFilter);
        Assert.Equal("base", context.ConfigMcp.Instances[0].Type);
        Assert.NotNull(context.DataMcp);
        Assert.Equal(connectionId.ToString(), context.DataMcp!.DataConnectionId);
        Assert.Equal("db-001", context.DataMcp.DatabaseId);
        Assert.True(context.DataMcp.Paired);
        Assert.Equal("locked", context.DataMcp.CredentialsState);
    }

    [Fact]
    public async Task ResolveAsync_WhenStatusUnavailable_ReturnsUnknownCredentialsState()
    {
        var provider = await CreateProviderAsync();
        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await SeedAsync(provider, clientId, infobaseId, projectId, instanceId, exportId, connectionId);

        var dataMcp = new Mock<IDataMcpToolClient>();
        dataMcp.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("portable missing"));

        var service = CreateService(provider, Mock.Of<IConfigMcpToolClient>(), dataMcp.Object);
        var context = await service.ResolveAsync(infobaseId.ToString(), null);

        Assert.Equal("unknown", context.DataMcp!.CredentialsState);
    }

    [Fact]
    public async Task ListInfobasesAsync_ReturnsClientNames()
    {
        var provider = await CreateProviderAsync();
        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        await SeedAsync(provider, clientId, infobaseId, projectId, instanceId, exportId, connectionId);

        var service = CreateService(provider, Mock.Of<IConfigMcpToolClient>(), Mock.Of<IDataMcpToolClient>());
        var items = await service.ListInfobasesAsync();

        Assert.Single(items);
        Assert.Equal("ERP Prod", items[0].Name);
        Assert.Equal("Romashka", items[0].ClientName);
    }

    [Fact]
    public async Task ResolveAsync_WhenLinkedInstanceHasNoDatabaseIdYet_ReturnsProjectWithEmptyInstances()
    {
        var provider = await CreateProviderAsync();
        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        await provider.GetRequiredService<IClientRepository>().SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "Romashka",
            ExportRootPath = @"D:\Exports"
        });

        await provider.GetRequiredService<IInfobaseRepository>().SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "ERP Prod",
            PlatformPath = @"C:\Program Files\1cv8\8.3.25.1394\bin\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = @"srv\erp"
        });

        await provider.GetRequiredService<IConfigurationInstanceRepository>().SaveAsync(new ConfigurationInstance
        {
            Id = instanceId,
            InfobaseId = infobaseId,
            Kind = ConfigurationKind.Base,
            DisplayName = "Main configuration",
            ConfigMcpProjectId = projectId,
            ExportEnabled = true
        });

        var service = CreateService(provider, Mock.Of<IConfigMcpToolClient>(), Mock.Of<IDataMcpToolClient>());
        var context = await service.ResolveAsync(infobaseId.ToString(), null);

        Assert.NotNull(context.ConfigMcp);
        Assert.Equal(projectId.ToString(), context.ConfigMcp!.ProjectId);
        Assert.Empty(context.ConfigMcp.Instances);
    }

    [Fact]
    public void BuildDefaultProjectFilter_ComposesClientAndInfobase()
    {
        Assert.Equal("Фитэра / Задачник", InfobaseContextService.BuildDefaultProjectFilter("Фитэра", "Задачник"));
    }

    private static InfobaseContextService CreateService(
        IServiceProvider provider,
        IConfigMcpToolClient configMcpToolClient,
        IDataMcpToolClient dataMcpToolClient) =>
        new(
            provider.GetRequiredService<IClientRepository>(),
            provider.GetRequiredService<IInfobaseRepository>(),
            provider.GetRequiredService<IConfigurationInstanceRepository>(),
            provider.GetRequiredService<IConfigurationExportRepository>(),
            provider.GetRequiredService<IDataConnectionRepository>(),
            configMcpToolClient,
            dataMcpToolClient);

    private static async Task SeedAsync(
        IServiceProvider provider,
        Guid clientId,
        Guid infobaseId,
        Guid projectId,
        Guid instanceId,
        Guid exportId,
        Guid connectionId)
    {
        await provider.GetRequiredService<IClientRepository>().SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "Romashka",
            ExportRootPath = @"D:\Exports"
        });

        await provider.GetRequiredService<IInfobaseRepository>().SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "ERP Prod",
            PlatformPath = @"C:\Program Files\1cv8\8.3.25.1394\bin\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = @"srv\erp"
        });

        await provider.GetRequiredService<IConfigurationInstanceRepository>().SaveAsync(new ConfigurationInstance
        {
            Id = instanceId,
            InfobaseId = infobaseId,
            Kind = ConfigurationKind.Base,
            DisplayName = "Main configuration",
            ConfigMcpProjectId = projectId,
            ExportEnabled = true
        });

        await provider.GetRequiredService<IConfigurationExportRepository>().SaveAsync(new ConfigurationExport
        {
            Id = exportId,
            InstanceId = instanceId,
            IsCurrent = true
        });

        await provider.GetRequiredService<IDataConnectionRepository>().SaveAsync(new DataConnection
        {
            Id = connectionId,
            InfobaseId = infobaseId,
            DatabaseId = "db-001",
            DisplayName = "ERP Prod"
        });
    }

    private static async Task<IServiceProvider> CreateProviderAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"configadmin-context-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddConfigAdminApplication(dbPath);
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        return provider;
    }
}
