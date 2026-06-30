using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IConfigurationTemplateRepository
{
    Task<IReadOnlyList<ConfigurationTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<ConfigurationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ConfigurationTemplate?> GetSystemBaseTemplateAsync(CancellationToken ct = default);
    Task SaveAsync(ConfigurationTemplate template, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
