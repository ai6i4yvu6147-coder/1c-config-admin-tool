using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Domain.Repositories;

public interface IToolInstanceRepository
{
    Task<ToolInstanceProfile?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default);
    Task SaveAsync(ToolInstanceProfile instance, CancellationToken ct = default);
}
