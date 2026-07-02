using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Infrastructure.Hub;

namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpToolClient : IDataMcpToolClient
{
    private readonly JsonCliRunner _cliRunner;
    private readonly ManagedToolRegistryService _registryService;

    public DataMcpToolClient(JsonCliRunner cliRunner, ManagedToolRegistryService registryService)
    {
        _cliRunner = cliRunner;
        _registryService = registryService;
    }

    public async Task<DataMcpInventoryResponse> GetInventoryAsync(CancellationToken ct = default)
    {
        var (cliPath, root) = await ResolveCliAsync(ct);
        return await _cliRunner.RunAndDeserializeAsync<DataMcpInventoryResponse>(
            cliPath,
            BuildArgs(root, "inventory", "--json"),
            ct);
    }

    public async Task<DataMcpStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var (cliPath, root) = await ResolveCliAsync(ct);
        return await _cliRunner.RunAndDeserializeAsync<DataMcpStatusResponse>(
            cliPath,
            BuildArgs(root, "status", "--json"),
            ct);
    }

    public async Task<(DataMcpApplySecretsResponse Response, JsonCliResult Raw)> ApplySecretsAsync(
        DataMcpApplySecretsInput input,
        string dmcpPassword,
        CancellationToken ct = default)
    {
        var (cliPath, root) = await ResolveCliAsync(ct);
        var inputPath = await _cliRunner.WriteTempJsonAsync(input, ct);

        try
        {
            return await _cliRunner.RunAndDeserializeWithRawAsync<DataMcpApplySecretsResponse>(
                cliPath,
                BuildArgs(root, "apply-secrets", "--input", inputPath, "--json"),
                BuildDmcpPasswordEnvironment(dmcpPassword),
                ct);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    public async Task<(ConfigMcpApplyRegistryResponse Response, JsonCliResult Raw)> ApplyRegistryAsync(
        DataMcpRegistryFragmentDocument fragment,
        CancellationToken ct = default)
    {
        var (cliPath, root) = await ResolveCliAsync(ct);
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

    public async Task<(DataMcpValidateConfigResponse Response, JsonCliResult Raw)> ValidateConfigAsync(
        string? dmcpPassword = null,
        CancellationToken ct = default)
    {
        var (cliPath, root) = await ResolveCliAsync(ct);
        return await _cliRunner.RunAndDeserializeWithRawAsync<DataMcpValidateConfigResponse>(
            cliPath,
            BuildArgs(root, "validate-config", "--json"),
            BuildDmcpPasswordEnvironment(dmcpPassword),
            ct);
    }

    private async Task<(string CliPath, string Root)> ResolveCliAsync(CancellationToken ct)
    {
        var cliPath = await _registryService.ResolveDataMcpCliPathAsync(ct);
        var root = (await _registryService.GetOrCreateDataMcpInstanceAsync(ct)).RootPath;
        return (cliPath, root);
    }

    private static IReadOnlyDictionary<string, string>? BuildDmcpPasswordEnvironment(string? dmcpPassword) =>
        string.IsNullOrEmpty(dmcpPassword)
            ? null
            : new Dictionary<string, string> { ["DMCP_PASSWORD"] = dmcpPassword };

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
