using System.Text.RegularExpressions;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Infrastructure.Hub;

namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpFragmentBuilder
{
    private static readonly Regex PlatformVersionRegex = new(
        @"\d+\.\d+\.\d+\.\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IExportPathBuilder _exportPathBuilder;
    private readonly IClientRepository _clientRepository;

    public ConfigMcpFragmentBuilder(
        IExportPathBuilder exportPathBuilder,
        IClientRepository clientRepository)
    {
        _exportPathBuilder = exportPathBuilder;
        _clientRepository = clientRepository;
    }

    public async Task<ConfigMcpRegistryFragmentDocument> BuildForInfobaseAsync(
        InfobaseProfile infobase,
        Guid configMcpProjectId,
        string projectName,
        CancellationToken ct = default)
    {
        if (configMcpProjectId == Guid.Empty)
            throw new InvalidOperationException("Не указан config-mcp projectId.");

        var client = await _clientRepository.GetByIdAsync(infobase.ClientId, ct)
            ?? throw new InvalidOperationException("Клиент базы не найден.");

        var sourcePath = _exportPathBuilder.GetConfigurationPath(
            client.ExportRootPath,
            client.Name,
            infobase.Name);

        return new ConfigMcpRegistryFragmentDocument
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
                        Databases =
                        [
                            new ConfigMcpRegistryDatabaseDto
                            {
                                InfobaseId = infobase.Id.ToString(),
                                Name = infobase.Name,
                                Type = "base",
                                SourcePath = sourcePath,
                                SourceKind = "directory",
                                PlatformVersion = ExtractPlatformVersion(infobase.PlatformPath)
                            }
                        ]
                    }
                ]
            }
        };
    }

    public static string ExtractPlatformVersion(string platformPath)
    {
        var match = PlatformVersionRegex.Match(platformPath);
        return match.Success ? match.Value : "8.3.0.0";
    }
}
