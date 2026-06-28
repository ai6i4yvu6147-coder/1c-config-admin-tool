using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Hub;

namespace ConfigAdmin.Application.Hub;

public sealed class ManagedToolRegistryService
{
    public const string DefaultConfigMcpRootPath = @"C:\1c_config_mcp_server_Portable";

    private readonly IToolInstanceRepository _toolInstanceRepository;
    private readonly ModuleManifestReader _manifestReader;

    public ManagedToolRegistryService(
        IToolInstanceRepository toolInstanceRepository,
        ModuleManifestReader manifestReader)
    {
        _toolInstanceRepository = toolInstanceRepository;
        _manifestReader = manifestReader;
    }

    public async Task<ToolInstanceProfile> GetOrCreateConfigMcpInstanceAsync(CancellationToken ct = default)
    {
        var existing = await _toolInstanceRepository.GetByModuleIdAsync(HubModuleIds.ConfigMcp, ct);
        if (existing is not null)
            return existing;

        var seeded = new ToolInstanceProfile
        {
            Id = Guid.NewGuid(),
            ModuleId = HubModuleIds.ConfigMcp,
            RootPath = DefaultConfigMcpRootPath,
            Enabled = true
        };
        await _toolInstanceRepository.SaveAsync(seeded, ct);
        return seeded;
    }

    public Task SaveConfigMcpRootPathAsync(string rootPath, CancellationToken ct = default) =>
        SaveRootPathAsync(HubModuleIds.ConfigMcp, rootPath, ct);

    public async Task SaveRootPathAsync(string moduleId, string rootPath, CancellationToken ct = default)
    {
        var instance = await _toolInstanceRepository.GetByModuleIdAsync(moduleId, ct)
                       ?? new ToolInstanceProfile
                       {
                           Id = Guid.NewGuid(),
                           ModuleId = moduleId,
                           Enabled = true
                       };

        instance.RootPath = rootPath.Trim();
        await _toolInstanceRepository.SaveAsync(instance, ct);
    }

    public async Task<string> ResolveConfigMcpCliPathAsync(CancellationToken ct = default)
    {
        var instance = await GetOrCreateConfigMcpInstanceAsync(ct);
        return ResolveCliPath(instance.RootPath);
    }

    public string ResolveCliPath(string rootPath) => _manifestReader.ResolveCliPath(rootPath);
}
