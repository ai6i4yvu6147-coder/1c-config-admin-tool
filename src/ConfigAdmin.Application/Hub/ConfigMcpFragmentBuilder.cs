using System.Text.RegularExpressions;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Services;

namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpFragmentBuilder
{
    private static readonly Regex PlatformVersionRegex = new(
        @"\d+\.\d+\.\d+\.\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IExportPathBuilder _exportPathBuilder;
    private readonly IClientRepository _clientRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IConfigurationInstanceRepository _instanceRepository;
    private readonly IConfigurationExportRepository _exportRepository;
    private readonly InfobaseConfigurationService _configurationService;

    public ConfigMcpFragmentBuilder(
        IExportPathBuilder exportPathBuilder,
        IClientRepository clientRepository,
        IInfobaseRepository infobaseRepository,
        IConfigurationInstanceRepository instanceRepository,
        IConfigurationExportRepository exportRepository,
        InfobaseConfigurationService configurationService)
    {
        _exportPathBuilder = exportPathBuilder;
        _clientRepository = clientRepository;
        _infobaseRepository = infobaseRepository;
        _instanceRepository = instanceRepository;
        _exportRepository = exportRepository;
        _configurationService = configurationService;
    }

    public async Task<ConfigMcpRegistryFragmentDocument> BuildForInstanceAsync(
        Guid instanceId,
        string projectName,
        CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct)
            ?? throw new InvalidOperationException("Экземпляр конфигурации не найден.");

        if (instance.ConfigMcpProjectId is not Guid projectId || projectId == Guid.Empty)
            throw new InvalidOperationException("Экземпляр не привязан к проекту config-mcp.");

        var export = await _exportRepository.GetCurrentByInstanceIdAsync(instanceId, ct)
            ?? await _configurationService.GetOrCreateCurrentExportAsync(instanceId, ct);

        var database = await BuildDatabaseDtoAsync(instance, export, ct);
        var infobase = await _infobaseRepository.GetByIdAsync(instance.InfobaseId, ct)
            ?? throw new InvalidOperationException("Инфобаза не найдена.");
        var client = await _clientRepository.GetByIdAsync(infobase.ClientId, ct)
            ?? throw new InvalidOperationException("Клиент базы не найден.");

        return BuildFragment(projectId, client, projectName, [database]);
    }

    public async Task<ConfigMcpRegistryFragmentDocument> BuildForInstancesAsync(
        IReadOnlyList<Guid> instanceIds,
        IReadOnlyDictionary<Guid, string> projectNames,
        CancellationToken ct = default)
    {
        if (instanceIds.Count == 0)
            throw new InvalidOperationException("Нет экземпляров для синхронизации.");

        var instances = new List<ConfigurationInstance>();
        foreach (var id in instanceIds)
        {
            var instance = await _instanceRepository.GetByIdAsync(id, ct)
                ?? throw new InvalidOperationException($"Экземпляр {id} не найден.");
            instances.Add(instance);
        }

        var byProject = instances
            .Where(i => i.ConfigMcpProjectId is Guid pid && pid != Guid.Empty)
            .GroupBy(i => i.ConfigMcpProjectId!.Value)
            .ToList();

        if (byProject.Count == 0)
            throw new InvalidOperationException("Нет привязанных экземпляров.");

        var projects = new List<ConfigMcpRegistryProjectDto>();
        foreach (var group in byProject)
        {
            var projectId = group.Key;
            var projectName = projectNames.TryGetValue(projectId, out var name) ? name : projectId.ToString();
            var databases = new List<ConfigMcpRegistryDatabaseDto>();

            ClientProfile? client = null;
            foreach (var instance in group)
            {
                var export = await _exportRepository.GetCurrentByInstanceIdAsync(instance.Id, ct)
                    ?? await _configurationService.GetOrCreateCurrentExportAsync(instance.Id, ct);

                databases.Add(await BuildDatabaseDtoAsync(instance, export, ct));

                if (client is null)
                {
                    var infobase = await _infobaseRepository.GetByIdAsync(instance.InfobaseId, ct)
                        ?? throw new InvalidOperationException("Инфобаза не найдена.");
                    client = await _clientRepository.GetByIdAsync(infobase.ClientId, ct)
                        ?? throw new InvalidOperationException("Клиент не найден.");
                }
            }

            projects.Add(new ConfigMcpRegistryProjectDto
            {
                ProjectId = projectId.ToString(),
                ClientId = client!.Id.ToString(),
                Name = projectName,
                Active = true,
                Databases = databases
            });
        }

        return new ConfigMcpRegistryFragmentDocument
        {
            ExportedAt = DateTimeOffset.UtcNow.ToString("O"),
            RegistryFragment = new ConfigMcpRegistryFragment { Projects = projects }
        };
    }

    private async Task<ConfigMcpRegistryDatabaseDto> BuildDatabaseDtoAsync(
        ConfigurationInstance instance,
        ConfigurationExport export,
        CancellationToken ct)
    {
        var infobase = await _infobaseRepository.GetByIdAsync(instance.InfobaseId, ct)
            ?? throw new InvalidOperationException("Инфобаза не найдена.");
        var client = await _clientRepository.GetByIdAsync(infobase.ClientId, ct)
            ?? throw new InvalidOperationException("Клиент базы не найден.");

        var sourcePath = instance.Kind == ConfigurationKind.Base
            ? _exportPathBuilder.GetConfigurationPath(client.ExportRootPath, client.Name, infobase.Name)
            : _exportPathBuilder.GetExtensionPath(
                client.ExportRootPath, client.Name, infobase.Name, instance.DesignerName!);

        var registryId = instance.ConfigMcpDatabaseId ?? export.Id;

        return new ConfigMcpRegistryDatabaseDto
        {
            InfobaseId = registryId.ToString(),
            Name = instance.DisplayName.Trim(),
            Type = instance.Kind == ConfigurationKind.Base ? "base" : "extension",
            SourcePath = sourcePath,
            SourceKind = "directory",
            PlatformVersion = ExtractPlatformVersion(infobase.PlatformPath)
        };
    }

    private static ConfigMcpRegistryFragmentDocument BuildFragment(
        Guid configMcpProjectId,
        ClientProfile client,
        string projectName,
        IReadOnlyList<ConfigMcpRegistryDatabaseDto> databases) =>
        new()
        {
            ExportedAt = DateTimeOffset.UtcNow.ToString("O"),
            RegistryFragment = new ConfigMcpRegistryFragment
            {
                Projects =
                [
                    new ConfigMcpRegistryProjectDto
                    {
                        ProjectId = configMcpProjectId.ToString(),
                        ClientId = client.Id.ToString(),
                        Name = projectName,
                        Active = true,
                        Databases = databases.ToList()
                    }
                ]
            }
        };

    public static string ExtractPlatformVersion(string platformPath)
    {
        var match = PlatformVersionRegex.Match(platformPath);
        return match.Success ? match.Value : "8.3.0.0";
    }
}
