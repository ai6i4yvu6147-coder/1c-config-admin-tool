using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Infrastructure.Hub;

namespace ConfigAdmin.Application.Hub;

public interface IConfigMcpToolClient
{
    Task<ConfigMcpInventoryResponse> GetInventoryAsync(CancellationToken ct = default);

    Task<ConfigMcpStatusResponse> GetStatusAsync(CancellationToken ct = default);

    Task<(ConfigMcpApplyRegistryResponse Response, JsonCliResult Raw)> ApplyRegistryAsync(
        ConfigMcpRegistryFragmentDocument fragment,
        CancellationToken ct = default);

    Task<(ConfigMcpRebuildIndexResponse Response, JsonCliResult Raw)> RebuildIndexAsync(
        string databaseId,
        CancellationToken ct = default);
}
