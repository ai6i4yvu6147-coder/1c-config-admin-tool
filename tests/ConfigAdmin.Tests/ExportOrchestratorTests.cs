using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Application.Export;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.FileSystem;
using ConfigAdmin.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConfigAdmin.Tests;

public class ExportOrchestratorTests
{
    [Fact]
    public async Task ExportBaseAsync_ConfigSuccess_PublishesToConfigurationFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        var clientId = Guid.NewGuid();
        var baseId = Guid.NewGuid();

        var client = new ClientProfile
        {
            Id = clientId,
            Name = "ClientA",
            ExportRootPath = root
        };

        var profile = new InfobaseProfile
        {
            Id = baseId,
            ClientId = clientId,
            Name = "BaseERP",
            PlatformPath = @"C:\Program Files\1cv8\8.3.24.0\bin\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = @"srv\erp",
            Username = "Admin"
        };

        var infobaseRepo = new Mock<IInfobaseRepository>();
        infobaseRepo.Setup(r => r.GetByIdAsync(baseId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        infobaseRepo.Setup(r => r.UpdateLastExportAsync(baseId, It.IsAny<DateTimeOffset>(), It.IsAny<ExportStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clientRepo = new Mock<IClientRepository>();
        clientRepo.Setup(r => r.GetByIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        var exportRunRepo = new Mock<IExportRunRepository>();
        exportRunRepo.Setup(r => r.SaveAsync(It.IsAny<ExportRunLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var exportRepo = new Mock<IConfigurationExportRepository>();
        exportRepo.Setup(r => r.MarkAllNotCurrentForInstanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        exportRepo.Setup(r => r.SaveAsync(It.IsAny<ConfigurationExport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vault = new Mock<ISecretVault>();

        var cli = new Mock<IOneCCliAdapter>();
        cli.Setup(a => a.BuildDumpConfigCommand(It.IsAny<DumpConfigRequest>()))
            .Returns((DumpConfigRequest req) => $@"DESIGNER /S ""srv\erp"" /DumpConfigToFiles ""{req.OutputPath}""");
        cli.Setup(a => a.RunDesignerAsync(It.IsAny<DesignerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DesignerCommand cmd, CancellationToken _) =>
            {
                var output = ExtractOutputPath(cmd.Arguments);
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "marker.txt"), "ok");
                return new ProcessResult
                {
                    ExitCode = 0,
                    CommandLineMasked = "masked",
                    Duration = TimeSpan.FromSeconds(1)
                };
            });

        var pathBuilder = new ExportPathBuilder();
        var runArtifactPathBuilder = new RunArtifactPathBuilder();
        var orchestrator = CreateOrchestrator(
            infobaseRepo.Object,
            clientRepo.Object,
            exportRunRepo.Object,
            exportRepo.Object,
            vault.Object,
            cli.Object,
            pathBuilder,
            runArtifactPathBuilder);

        var plan = new InstanceExportPlan
        {
            Instances =
            [
                new ExportInstancePlan
                {
                    InstanceId = Guid.NewGuid(),
                    Kind = ConfigurationKind.Base,
                    DisplayName = "Основная конфигурация"
                }
            ]
        };

        var result = await orchestrator.ExportBaseAsync(baseId, plan);

        Assert.True(result.Success);
        var configPath = pathBuilder.GetConfigurationPath(root, "ClientA", "BaseERP");
        Assert.True(File.Exists(Path.Combine(configPath, "marker.txt")));

        var runsRoot = Path.Combine(AppPaths.RunsDirectory, "ClientA", "BaseERP");
        Assert.True(Directory.Exists(runsRoot));
        Assert.Contains(Directory.GetDirectories(runsRoot), d => File.Exists(Path.Combine(d, "export-meta.json")));
    }

    [Fact]
    public async Task ExportBaseAsync_ConfigFailure_DoesNotReplaceConfigurationFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        var clientId = Guid.NewGuid();
        var baseId = Guid.NewGuid();
        var pathBuilder = new ExportPathBuilder();
        var configPath = pathBuilder.GetConfigurationPath(root, "ClientA", "BaseERP");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(Path.Combine(configPath, "keep.txt"), "old");

        var client = new ClientProfile { Id = clientId, Name = "ClientA", ExportRootPath = root };
        var profile = new InfobaseProfile
        {
            Id = baseId,
            ClientId = clientId,
            Name = "BaseERP",
            PlatformPath = @"C:\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = "srv\\erp"
        };

        var infobaseRepo = new Mock<IInfobaseRepository>();
        infobaseRepo.Setup(r => r.GetByIdAsync(baseId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        infobaseRepo.Setup(r => r.UpdateLastExportAsync(baseId, It.IsAny<DateTimeOffset>(), It.IsAny<ExportStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clientRepo = new Mock<IClientRepository>();
        clientRepo.Setup(r => r.GetByIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        var exportRunRepo = new Mock<IExportRunRepository>();
        exportRunRepo.Setup(r => r.SaveAsync(It.IsAny<ExportRunLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var exportRepo = new Mock<IConfigurationExportRepository>();

        var cli = new Mock<IOneCCliAdapter>();
        cli.Setup(a => a.BuildDumpConfigCommand(It.IsAny<DumpConfigRequest>())).Returns("DESIGNER");
        cli.Setup(a => a.RunDesignerAsync(It.IsAny<DesignerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                StandardError = "fail",
                CommandLineMasked = "masked",
                Duration = TimeSpan.FromSeconds(1)
            });

        var orchestrator = CreateOrchestrator(
            infobaseRepo.Object,
            clientRepo.Object,
            exportRunRepo.Object,
            exportRepo.Object,
            new Mock<ISecretVault>().Object,
            cli.Object,
            pathBuilder,
            new RunArtifactPathBuilder());

        var plan = new InstanceExportPlan
        {
            Instances =
            [
                new ExportInstancePlan
                {
                    InstanceId = Guid.NewGuid(),
                    Kind = ConfigurationKind.Base,
                    DisplayName = "Основная конфигурация"
                }
            ]
        };

        var result = await orchestrator.ExportBaseAsync(baseId, plan);

        Assert.False(result.Success);
        Assert.True(File.Exists(Path.Combine(configPath, "keep.txt")));
    }

    [Fact]
    public async Task ExportBaseAsync_ExtensionOnly_PublishesDirectlyUnderBase()
    {
        var root = Path.Combine(Path.GetTempPath(), "configadmin-tests", Guid.NewGuid().ToString("N"));
        var clientId = Guid.NewGuid();
        var baseId = Guid.NewGuid();
        var pathBuilder = new ExportPathBuilder();
        var configPath = pathBuilder.GetConfigurationPath(root, "ClientA", "BaseERP");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(Path.Combine(configPath, "keep.txt"), "old-config");

        var client = new ClientProfile { Id = clientId, Name = "ClientA", ExportRootPath = root };
        var profile = new InfobaseProfile
        {
            Id = baseId,
            ClientId = clientId,
            Name = "BaseERP",
            PlatformPath = @"C:\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = "srv\\erp"
        };

        var infobaseRepo = new Mock<IInfobaseRepository>();
        infobaseRepo.Setup(r => r.GetByIdAsync(baseId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        infobaseRepo.Setup(r => r.UpdateLastExportAsync(baseId, It.IsAny<DateTimeOffset>(), It.IsAny<ExportStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clientRepo = new Mock<IClientRepository>();
        clientRepo.Setup(r => r.GetByIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        var exportRunRepo = new Mock<IExportRunRepository>();
        exportRunRepo.Setup(r => r.SaveAsync(It.IsAny<ExportRunLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var exportRepo = new Mock<IConfigurationExportRepository>();
        exportRepo.Setup(r => r.MarkAllNotCurrentForInstanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        exportRepo.Setup(r => r.SaveAsync(It.IsAny<ConfigurationExport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cli = new Mock<IOneCCliAdapter>();
        cli.Setup(a => a.BuildDumpConfigCommand(It.IsAny<DumpConfigRequest>()))
            .Returns((DumpConfigRequest req) => $"/DumpConfigToFiles \"{req.OutputPath}\" -Extension");
        cli.Setup(a => a.RunDesignerAsync(It.IsAny<DesignerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DesignerCommand cmd, CancellationToken _) =>
            {
                var output = ExtractOutputPath(cmd.Arguments);
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "ext.txt"), "ok");
                return new ProcessResult { ExitCode = 0, CommandLineMasked = "masked", Duration = TimeSpan.FromSeconds(1) };
            });

        var orchestrator = CreateOrchestrator(
            infobaseRepo.Object,
            clientRepo.Object,
            exportRunRepo.Object,
            exportRepo.Object,
            new Mock<ISecretVault>().Object,
            cli.Object,
            pathBuilder,
            new RunArtifactPathBuilder());

        var plan = new InstanceExportPlan
        {
            Instances =
            [
                new ExportInstancePlan
                {
                    InstanceId = Guid.NewGuid(),
                    Kind = ConfigurationKind.Extension,
                    DisplayName = "MyExt",
                    DesignerName = "MyExt"
                }
            ]
        };

        var result = await orchestrator.ExportBaseAsync(baseId, plan);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(configPath, "keep.txt")));
        Assert.True(File.Exists(pathBuilder.GetExtensionPath(root, "ClientA", "BaseERP", "MyExt") + "\\ext.txt"));
    }

    private static ExportOrchestrator CreateOrchestrator(
        IInfobaseRepository infobaseRepo,
        IClientRepository clientRepo,
        IExportRunRepository exportRunRepo,
        IConfigurationExportRepository exportRepo,
        ISecretVault vault,
        IOneCCliAdapter cli,
        IExportPathBuilder pathBuilder,
        IRunArtifactPathBuilder runArtifactPathBuilder)
    {
        var templateRepo = new Mock<IConfigurationTemplateRepository>();
        templateRepo.Setup(r => r.GetSystemBaseTemplateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationTemplate
            {
                Id = ConfigurationTemplateIds.SystemBaseTemplateId,
                Name = "Основная конфигурация",
                Kind = ConfigurationKind.Base,
                IsSystem = true
            });
        var instanceRepo = new Mock<IConfigurationInstanceRepository>();
        var configService = new InfobaseConfigurationService(
            templateRepo.Object,
            instanceRepo.Object,
            exportRepo,
            infobaseRepo);

        return new ExportOrchestrator(
            infobaseRepo,
            clientRepo,
            exportRunRepo,
            exportRepo,
            configService,
            vault,
            cli,
            pathBuilder,
            runArtifactPathBuilder,
            new AtomicDirectoryService(),
            NullLogger<ExportOrchestrator>.Instance);
    }

    private static string ExtractOutputPath(string arguments)
    {
        const string marker = "/DumpConfigToFiles \"";
        var start = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            throw new InvalidOperationException("Output path not found.");

        start += marker.Length;
        var end = arguments.IndexOf('"', start);
        return arguments[start..end];
    }
}
