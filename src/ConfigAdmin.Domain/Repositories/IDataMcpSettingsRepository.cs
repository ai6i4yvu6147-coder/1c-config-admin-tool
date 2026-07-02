using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IDataMcpSettingsRepository
{
    Task<DataMcpSettings?> GetByToolInstanceIdAsync(Guid toolInstanceId, CancellationToken ct = default);
    Task<DataMcpSettings?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default);
    Task SaveAsync(DataMcpSettings settings, CancellationToken ct = default);
    Task DeleteByToolInstanceIdAsync(Guid toolInstanceId, CancellationToken ct = default);
}
