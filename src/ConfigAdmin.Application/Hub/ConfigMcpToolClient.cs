using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Infrastructure.Hub;

namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpToolClient
{
    private readonly JsonCliRunner _cliRunner;
    private readonly ManagedToolRegistryService _registryService;

    public ConfigMcpToolClient(JsonCliRunner cliRunner, ManagedToolRegistryService registryService)
    {
        _cliRunner = cliRunner;
        _registryService = registryService;
    }

    public async Task<ConfigMcpInventoryResponse> GetInventoryAsync(CancellationToken ct = default)
    {
        var cliPath = await _registryService.ResolveConfigMcpCliPathAsync(ct);
        var root = (await _registryService.GetOrCreateConfigMcpInstanceAsync(ct)).RootPath;
        return await _cliRunner.RunAndDeserializeAsync<ConfigMcpInventoryResponse>(
            cliPath,
            BuildArgs(root, "inventory", "--json"),
            ct);
    }

    public async Task<ConfigMcpStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var cliPath = await _registryService.ResolveConfigMcpCliPathAsync(ct);
        var root = (await _registryService.GetOrCreateConfigMcpInstanceAsync(ct)).RootPath;
        return await _cliRunner.RunAndDeserializeAsync<ConfigMcpStatusResponse>(
            cliPath,
            BuildArgs(root, "status", "--json"),
            ct);
    }

    public async Task<(ConfigMcpApplyRegistryResponse Response, JsonCliResult Raw)> ApplyRegistryAsync(
        ConfigMcpRegistryFragmentDocument fragment,
        CancellationToken ct = default)
    {
        var cliPath = await _registryService.ResolveConfigMcpCliPathAsync(ct);
        var root = (await _registryService.GetOrCreateConfigMcpInstanceAsync(ct)).RootPath;
        var inputPath = await _cliRunner.WriteTempJsonAsync(fragment, ct);

        try
        {
            return await _cliRunner.RunAndDeserializeWithRawAsync<ConfigMcpApplyRegistryResponse>(
                cliPath,
                BuildArgs(root, "apply-registry", "--input", inputPath, "--json"),
                ct);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    public async Task<(ConfigMcpRebuildIndexResponse Response, JsonCliResult Raw)> RebuildIndexAsync(
        string databaseId,
        CancellationToken ct = default)
    {
        var cliPath = await _registryService.ResolveConfigMcpCliPathAsync(ct);
        var root = (await _registryService.GetOrCreateConfigMcpInstanceAsync(ct)).RootPath;
        return await _cliRunner.RunAndDeserializeWithRawAsync<ConfigMcpRebuildIndexResponse>(
            cliPath,
            BuildArgs(root, "rebuild-index", "--db-id", databaseId, "--json"),
            ct);
    }

    private static List<string> BuildArgs(string root, params string[] commandArgs)
    {
        var args = new List<string> { "--root", root };
        args.AddRange(commandArgs);
        return args;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }
}
