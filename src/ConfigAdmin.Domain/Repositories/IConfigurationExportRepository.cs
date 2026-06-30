using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IConfigurationExportRepository
{
    Task<ConfigurationExport?> GetCurrentByInstanceIdAsync(Guid instanceId, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigurationExport>> GetByInstanceIdsAsync(IEnumerable<Guid> instanceIds, CancellationToken ct = default);
    Task SaveAsync(ConfigurationExport export, CancellationToken ct = default);
    Task MarkAllNotCurrentForInstanceAsync(Guid instanceId, CancellationToken ct = default);
}
