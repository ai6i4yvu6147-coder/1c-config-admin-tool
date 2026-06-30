using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;

namespace ConfigAdmin.Application.Services;

public sealed class ConfigurationCatalogService
{
    private readonly IConfigurationTemplateRepository _templateRepository;

    public ConfigurationCatalogService(IConfigurationTemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public Task<IReadOnlyList<ConfigurationTemplate>> GetTemplatesAsync(CancellationToken ct = default) =>
        _templateRepository.GetAllAsync(ct);

    public Task<ConfigurationTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default) =>
        _templateRepository.GetByIdAsync(id, ct);

    public async Task<ConfigurationTemplate> SaveTemplateAsync(
        ConfigurationTemplate template,
        CancellationToken ct = default)
    {
        if (template.IsSystem)
            throw new InvalidOperationException("Системный шаблон нельзя изменять.");

        if (string.IsNullOrWhiteSpace(template.Name))
            throw new InvalidOperationException("Укажите имя шаблона.");

        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();

        template.Kind = ConfigurationKind.Extension;
        await _templateRepository.SaveAsync(template, ct);
        return template;
    }

    public Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        if (id == ConfigurationTemplateIds.SystemBaseTemplateId)
            throw new InvalidOperationException("Системный шаблон нельзя удалить.");

        return _templateRepository.DeleteAsync(id, ct);
    }
}
