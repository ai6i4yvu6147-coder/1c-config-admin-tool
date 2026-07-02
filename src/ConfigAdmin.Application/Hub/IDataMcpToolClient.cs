using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Infrastructure.Hub;

namespace ConfigAdmin.Application.Hub;

public interface IDataMcpToolClient
{
    Task<DataMcpInventoryResponse> GetInventoryAsync(CancellationToken ct = default);

    Task<DataMcpStatusResponse> GetStatusAsync(CancellationToken ct = default);

    Task<(DataMcpApplySecretsResponse Response, JsonCliResult Raw)> ApplySecretsAsync(
        DataMcpApplySecretsInput input,
        string dmcpPassword,
        CancellationToken ct = default);

    Task<(ConfigMcpApplyRegistryResponse Response, JsonCliResult Raw)> ApplyRegistryAsync(
        DataMcpRegistryFragmentDocument fragment,
        CancellationToken ct = default);

    Task<(DataMcpValidateConfigResponse Response, JsonCliResult Raw)> ValidateConfigAsync(
        string? dmcpPassword = null,
        CancellationToken ct = default);
}
