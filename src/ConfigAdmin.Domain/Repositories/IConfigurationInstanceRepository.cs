using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IConfigurationInstanceRepository
{
    Task<IReadOnlyList<ConfigurationInstance>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ConfigurationInstance>> GetByInfobaseIdAsync(Guid infobaseId, CancellationToken ct = default);
    Task<ConfigurationInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(ConfigurationInstance instance, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task DeleteByInfobaseIdAsync(Guid infobaseId, CancellationToken ct = default);
}
