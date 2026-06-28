using ConfigAdmin.Application.Hub;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.FileSystem;
using Moq;
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
    public async Task BuildForInfobaseAsync_UsesExportConfigurationPath()
    {
        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var client = new ClientProfile
        {
            Id = clientId,
            Name = "ClientA",
            ExportRootPath = @"D:\Exports"
        };

        var infobase = new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "BaseERP",
            PlatformPath = @"C:\Program Files\1cv8\8.3.27.1688\bin\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = @"srv\erp"
        };

        var clientRepo = new Mock<IClientRepository>();
        clientRepo.Setup(r => r.GetByIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        var builder = new ConfigMcpFragmentBuilder(new ExportPathBuilder(), clientRepo.Object);
        var fragment = await builder.BuildForInfobaseAsync(infobase, projectId, "ERP Project");

        var project = Assert.Single(fragment.RegistryFragment.Projects);
        Assert.Equal(projectId.ToString(), project.ProjectId);
        Assert.Equal(clientId.ToString(), project.ClientId);
        Assert.Equal("ERP Project", project.Name);

        var database = Assert.Single(project.Databases);
        Assert.Equal(infobaseId.ToString(), database.InfobaseId);
        Assert.Equal("directory", database.SourceKind);
        Assert.Equal("8.3.27.1688", database.PlatformVersion);
        Assert.EndsWith(
            Path.Combine("ClientA", "BaseERP", "Основная конфигурация"),
            database.SourcePath,
            StringComparison.OrdinalIgnoreCase);
    }
}
