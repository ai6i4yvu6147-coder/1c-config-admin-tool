using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.FileSystem;
using ConfigAdmin.Infrastructure.Hub;
using ConfigAdmin.Infrastructure.Repositories;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class ConfigMcpSyncServiceTests
{
    [Fact]
    public void CreateServiceAsync_DoesNotUseDefaultPortableRoot()
    {
        Assert.NotEqual(
            ManagedToolRegistryService.DefaultConfigMcpRootPath,
            Path.GetFullPath(CreateTempPortableRoot()));
    }

    [Fact]
    public async Task LinkInstanceAsync_NewProject_AssignsProjectIdToInstance()
    {
        var (service, instanceId, instanceRepo, _, _) = await CreateServiceAsync();

        await service.LinkInstanceAsync(instanceId, new ConfigMcpLinkRequest
        {
            Mode = ConfigMcpLinkMode.NewProject,
            ProjectName = "New MCP Project"
        });

        var instance = await instanceRepo.GetByIdAsync(instanceId);
        Assert.NotNull(instance);
        Assert.NotNull(instance.ConfigMcpProjectId);
        Assert.NotEqual(Guid.Empty, instance.ConfigMcpProjectId);
        Assert.Null(instance.ConfigMcpDatabaseId);
    }

    [Fact]
    public async Task LinkInstanceAsync_ExistingDatabase_StoresIds()
    {
        var (service, instanceId, instanceRepo, _, _) = await CreateServiceAsync();
        var projectId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();

        await service.LinkInstanceAsync(instanceId, new ConfigMcpLinkRequest
        {
            Mode = ConfigMcpLinkMode.ExistingDatabase,
            ProjectId = projectId,
            ProjectName = "Existing",
            DatabaseId = databaseId
        });

        var instance = await instanceRepo.GetByIdAsync(instanceId);
        Assert.Equal(projectId, instance!.ConfigMcpProjectId);
        Assert.Equal(databaseId, instance.ConfigMcpDatabaseId);
    }

    [Fact]
    public void BuildLinkRequest_NewProject_UsesDefaultName()
    {
        var request = ConfigMcpSyncService.BuildLinkRequest(
            new ConfigMcpLinkSelection { CreateNewProject = true, CreateNewDatabase = true },
            "Client A");

        Assert.Equal(ConfigMcpLinkMode.NewProject, request.Mode);
        Assert.Equal("Client A", request.ProjectName);
    }

    [Fact]
    public void BuildLinkRequest_ExistingDatabase_RequiresIds()
    {
        var projectId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();
        var request = ConfigMcpSyncService.BuildLinkRequest(
            new ConfigMcpLinkSelection
            {
                CreateNewProject = false,
                CreateNewDatabase = false,
                ProjectId = projectId,
                ProjectName = "P",
                DatabaseId = databaseId
            },
            "Client");

        Assert.Equal(ConfigMcpLinkMode.ExistingDatabase, request.Mode);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal(databaseId, request.DatabaseId);
    }

    [Fact]
    public async Task SyncInstanceAsync_WithoutPriorExport_CreatesExportRecord()
    {
        var (service, instanceId, _, _, exportRepo) = await CreateServiceAsync(withExport: false);
        var projectId = Guid.NewGuid();

        await service.LinkInstanceAsync(instanceId, new ConfigMcpLinkRequest
        {
            Mode = ConfigMcpLinkMode.NewDatabaseInProject,
            ProjectId = projectId,
            ProjectName = "Client / Base"
        });

        _ = await service.SyncInstanceAsync(instanceId);

        var export = await exportRepo.GetCurrentByInstanceIdAsync(instanceId);
        Assert.NotNull(export);
    }

    [Fact]
    public async Task MigrateLegacyInfobaseLinks_CopiesProjectToBaseInstance()
    {
        var (service, instanceId, instanceRepo, infobaseRepo, _) = await CreateServiceAsync();
        var legacyProjectId = Guid.NewGuid();
        var infobase = await infobaseRepo.GetByIdAsync(
            (await instanceRepo.GetByIdAsync(instanceId))!.InfobaseId);
        infobase!.ConfigMcpProjectId = legacyProjectId;
        await infobaseRepo.SaveAsync(infobase);

        await service.MigrateLegacyInfobaseLinksAsync();

        var instance = await instanceRepo.GetByIdAsync(instanceId);
        Assert.Equal(legacyProjectId, instance!.ConfigMcpProjectId);
    }

    [Fact]
    public void BuildLinkRequest_NewProject_RejectsTooShortName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigMcpSyncService.BuildLinkRequest(
                new ConfigMcpLinkSelection { CreateNewProject = true, CreateNewDatabase = true },
                "Р"));

        Assert.Contains("короткое", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsLinkRegistrySatisfied_SkippedCountsAsSuccessForNewDatabase()
    {
        var result = new ConfigMcpSyncResult
        {
            Success = true,
            ChangesSkipped = 1
        };

        Assert.True(ConfigMcpSyncService.IsLinkRegistrySatisfied(
            ConfigMcpLinkMode.NewDatabaseInProject, result));
    }

    [Fact]
    public void IsLinkRegistrySatisfied_NoChanges_IsFailure()
    {
        var result = new ConfigMcpSyncResult { Success = true };

        Assert.False(ConfigMcpSyncService.IsLinkRegistrySatisfied(
            ConfigMcpLinkMode.NewDatabaseInProject, result));
    }

    [Fact]
    public void IsLinkRegistrySatisfied_SkippedWithPathWarning_IsSuccessWhenCreatedViaFallback()
    {
        var result = new ConfigMcpSyncResult
        {
            Success = true,
            ChangesCreated = 1,
            ChangesSkipped = 1,
            Warnings = ["sourcePath directory not found: C:\\missing"]
        };

        Assert.True(ConfigMcpSyncService.IsLinkRegistrySatisfied(
            ConfigMcpLinkMode.NewDatabaseInProject, result));
    }

    [Fact]
    public void ShouldTryPlannedPathMerge_WhenCliCreatedProjectShell_StillMerges()
    {
        var response = new ConfigMcpApplyRegistryResponse
        {
            Warnings = ["infobaseId abc: sourcePath directory not found: C:\\missing"]
        };
        var result = new ConfigMcpSyncResult
        {
            Success = true,
            ChangesCreated = 1,
            ChangesSkipped = 1
        };

        Assert.True(ConfigMcpSyncService.ShouldTryPlannedPathMerge(response, result));
    }

    [Fact]
    public void CollectRebuildDatabaseIds_UsesFollowUpOperations()
    {
        var databaseId = Guid.NewGuid().ToString();
        var response = new ConfigMcpApplyRegistryResponse
        {
            PostApplyActions = new ConfigMcpPostApplyActionsDto
            {
                FollowUpOperations =
                [
                    new ConfigMcpFollowUpOperationDto
                    {
                        Command = "rebuild-index",
                        Args = new Dictionary<string, string> { ["db-id"] = databaseId }
                    }
                ]
            }
        };

        var fragment = new ConfigMcpRegistryFragmentDocument
        {
            RegistryFragment = new ConfigMcpRegistryFragment { Projects = [] }
        };

        var ids = ConfigMcpSyncService.CollectRebuildDatabaseIds(response, fragment);

        Assert.Single(ids);
        Assert.Equal(databaseId, ids[0]);
    }

    [Fact]
    public void CollectRebuildDatabaseIds_FallsBackToExistingSourcePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcp-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var databaseId = Guid.NewGuid().ToString();
            var fragment = new ConfigMcpRegistryFragmentDocument
            {
                RegistryFragment = new ConfigMcpRegistryFragment
                {
                    Projects =
                    [
                        new ConfigMcpRegistryProjectDto
                        {
                            Databases =
                            [
                                new ConfigMcpRegistryDatabaseDto
                                {
                                    InfobaseId = databaseId,
                                    SourcePath = tempDir
                                }
                            ]
                        }
                    ]
                }
            };

            var ids = ConfigMcpSyncService.CollectRebuildDatabaseIds(
                new ConfigMcpApplyRegistryResponse(),
                fragment);

            Assert.Single(ids);
            Assert.Equal(databaseId, ids[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectsJsonMerger_TryRemoveProjects_RemovesTrackedIds()
    {
        var keepId = Guid.NewGuid().ToString();
        var removeId = Guid.NewGuid().ToString();
        var path = Path.Combine(Path.GetTempPath(), $"projects-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            $$"""
            {
              "projects": [
                { "id": "{{removeId}}", "name": "A", "active": true, "databases": [] },
                { "id": "{{keepId}}", "name": "B", "active": true, "databases": [] }
              ]
            }
            """);

        try
        {
            var merger = new ConfigMcpProjectsJsonMerger();
            Assert.True(merger.TryRemoveProjects(path, [removeId], out var removed, out var error), error);
            Assert.Equal(1, removed);

            var remaining = ConfigMcpProjectsJsonMerger.LoadProjectIds(path);
            Assert.Single(remaining);
            Assert.Equal(keepId, remaining[0], StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempPortableRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mcp-portable-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "projects.json"),
            """{"projects":[]}""",
            System.Text.Encoding.UTF8);
        return root;
    }

    private static async Task<(
        ConfigMcpSyncService Service,
        Guid InstanceId,
        ConfigurationInstanceRepository InstanceRepo,
        InfobaseRepository InfobaseRepo,
        ConfigurationExportRepository ExportRepo)> CreateServiceAsync(bool withExport = true)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mcp-sync-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var clientId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        var clientRepo = new ClientRepository(factory);
        await clientRepo.SaveAsync(new ClientProfile
        {
            Id = clientId,
            Name = "Client",
            ExportRootPath = @"D:\Exports"
        });

        var infobaseRepo = new InfobaseRepository(factory);
        await infobaseRepo.SaveAsync(new InfobaseProfile
        {
            Id = infobaseId,
            ClientId = clientId,
            Name = "Base",
            PlatformPath = @"C:\1cv8\8.3.27.1688\bin\1cv8.exe",
            ConnectionType = ConnectionType.Server,
            ConnectionString = "srv"
        });

        var instanceRepo = new ConfigurationInstanceRepository(factory);
        await instanceRepo.SaveAsync(new ConfigurationInstance
        {
            Id = instanceId,
            InfobaseId = infobaseId,
            TemplateId = ConfigurationTemplateIds.SystemBaseTemplateId,
            Kind = ConfigurationKind.Base,
            DisplayName = "Основная",
            ExportEnabled = true,
            SortOrder = 0
        });

        var exportRepo = new ConfigurationExportRepository(factory);
        if (withExport)
        {
            await exportRepo.SaveAsync(new ConfigurationExport
            {
                Id = Guid.NewGuid(),
                InstanceId = instanceId,
                IsCurrent = true
            });
        }

        var hubProjectRepo = new HubProjectRepository(factory);
        var configService = new InfobaseConfigurationService(
            new ConfigurationTemplateRepository(factory),
            instanceRepo,
            exportRepo,
            infobaseRepo);

        var fragmentBuilder = new ConfigMcpFragmentBuilder(
            new ExportPathBuilder(),
            clientRepo,
            infobaseRepo,
            instanceRepo,
            exportRepo,
            configService);

        var toolRepo = new ToolInstanceRepository(factory);
        var registryService = new ManagedToolRegistryService(toolRepo, new ModuleManifestReader());
        await registryService.SaveConfigMcpRootPathAsync(CreateTempPortableRoot());

        var service = new ConfigMcpSyncService(
            infobaseRepo,
            clientRepo,
            hubProjectRepo,
            instanceRepo,
            fragmentBuilder,
            new FakeConfigMcpToolClient(),
            registryService,
            configService,
            new ConfigMcpProjectsJsonMerger(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigMcpSyncService>.Instance);

        return (service, instanceId, instanceRepo, infobaseRepo, exportRepo);
    }

    private sealed class FakeConfigMcpToolClient : IConfigMcpToolClient
    {
        public Task<ConfigMcpInventoryResponse> GetInventoryAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConfigMcpInventoryResponse());

        public Task<ConfigMcpStatusResponse> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConfigMcpStatusResponse());

        public Task<(ConfigMcpApplyRegistryResponse Response, JsonCliResult Raw)> ApplyRegistryAsync(
            ConfigMcpRegistryFragmentDocument fragment,
            CancellationToken ct = default) =>
            Task.FromResult((new ConfigMcpApplyRegistryResponse
            {
                Success = true,
                Changes = new ConfigMcpApplyChangesDto
                {
                    Created = 0,
                    Updated = 0,
                    Skipped = 1
                },
                Warnings =
                [
                    "infobaseId test: sourcePath directory not found: C:\\missing"
                ]
            }, new JsonCliResult { ExitCode = 0 }));

        public Task<(ConfigMcpRebuildIndexResponse Response, JsonCliResult Raw)> RebuildIndexAsync(
            string databaseId,
            CancellationToken ct = default) =>
            Task.FromResult((new ConfigMcpRebuildIndexResponse { Success = true }, new JsonCliResult { ExitCode = 0 }));
    }
}
