using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;

namespace ConfigAdmin.Application.Services;

public sealed class InfobaseConfigurationService
{
    private readonly IConfigurationTemplateRepository _templateRepository;
    private readonly IConfigurationInstanceRepository _instanceRepository;
    private readonly IConfigurationExportRepository _exportRepository;
    private readonly IInfobaseRepository _infobaseRepository;

    public InfobaseConfigurationService(
        IConfigurationTemplateRepository templateRepository,
        IConfigurationInstanceRepository instanceRepository,
        IConfigurationExportRepository exportRepository,
        IInfobaseRepository infobaseRepository)
    {
        _templateRepository = templateRepository;
        _instanceRepository = instanceRepository;
        _exportRepository = exportRepository;
        _infobaseRepository = infobaseRepository;
    }

    public async Task<IReadOnlyList<ConfigurationInstance>> GetInstancesAsync(
        Guid infobaseId,
        CancellationToken ct = default)
    {
        await EnsureBaseInstanceAsync(infobaseId, ct);
        return await _instanceRepository.GetByInfobaseIdAsync(infobaseId, ct);
    }

    public async Task EnsureBaseInstanceAsync(Guid infobaseId, CancellationToken ct = default)
    {
        var instances = await _instanceRepository.GetByInfobaseIdAsync(infobaseId, ct);
        if (instances.Any(i => i.Kind == ConfigurationKind.Base))
            return;

        var baseTemplate = await _templateRepository.GetSystemBaseTemplateAsync(ct)
            ?? throw new InvalidOperationException("Системный шаблон основной конфигурации не найден.");

        await _instanceRepository.SaveAsync(new ConfigurationInstance
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobaseId,
            TemplateId = baseTemplate.Id,
            Kind = ConfigurationKind.Base,
            DisplayName = baseTemplate.Name,
            ExportEnabled = true,
            SortOrder = 0
        }, ct);
    }

    public async Task ImportLegacyExportSettingsAsync(InfobaseProfile profile, CancellationToken ct = default)
    {
        await EnsureBaseInstanceAsync(profile.Id, ct);
        var instances = await _instanceRepository.GetByInfobaseIdAsync(profile.Id, ct);
        var baseInstance = instances.First(i => i.Kind == ConfigurationKind.Base);
        baseInstance.ExportEnabled = profile.ExportConfiguration;
        await _instanceRepository.SaveAsync(baseInstance, ct);

        if (instances.Count > 1)
            return;

        if (profile.ExportAllExtensions || profile.SelectedExtensions.Count > 0)
        {
            var names = profile.ExportAllExtensions
                ? profile.SelectedExtensions
                : profile.SelectedExtensions;

            var sort = 1;
            foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                await _instanceRepository.SaveAsync(new ConfigurationInstance
                {
                    Id = Guid.NewGuid(),
                    InfobaseId = profile.Id,
                    TemplateId = null,
                    Kind = ConfigurationKind.Extension,
                    DisplayName = name.Trim(),
                    DesignerName = name.Trim(),
                    ExportEnabled = true,
                    SortOrder = sort++
                }, ct);
            }
        }
    }

    public async Task SaveInstancesAsync(
        Guid infobaseId,
        IReadOnlyList<ConfigurationInstance> instances,
        CancellationToken ct = default)
    {
        await EnsureBaseInstanceAsync(infobaseId, ct);
        var existing = await _instanceRepository.GetByInfobaseIdAsync(infobaseId, ct);
        var baseInstance = existing.First(i => i.Kind == ConfigurationKind.Base);
        var incomingBase = instances.FirstOrDefault(i => i.Kind == ConfigurationKind.Base);

        if (incomingBase is not null)
        {
            baseInstance.ExportEnabled = incomingBase.ExportEnabled;
            await _instanceRepository.SaveAsync(baseInstance, ct);
        }

        var existingExtensions = existing.Where(i => i.Kind == ConfigurationKind.Extension).ToList();
        var incomingExtensions = instances.Where(i => i.Kind == ConfigurationKind.Extension).ToList();
        var incomingIds = incomingExtensions.Select(i => i.Id).ToHashSet();

        foreach (var old in existingExtensions)
        {
            if (!incomingIds.Contains(old.Id))
                await _instanceRepository.DeleteAsync(old.Id, ct);
        }

        var sort = 1;
        foreach (var instance in incomingExtensions.OrderBy(i => i.SortOrder).ThenBy(i => i.DisplayName))
        {
            instance.InfobaseId = infobaseId;
            instance.Kind = ConfigurationKind.Extension;
            instance.SortOrder = sort++;

            var previous = existingExtensions.FirstOrDefault(e => e.Id == instance.Id);
            if (previous is not null)
            {
                instance.ConfigMcpProjectId = previous.ConfigMcpProjectId;
                instance.ConfigMcpDatabaseId = previous.ConfigMcpDatabaseId;
            }
            else if (baseInstance.ConfigMcpProjectId is Guid inheritedProject)
            {
                instance.ConfigMcpProjectId = inheritedProject;
                instance.ConfigMcpDatabaseId = null;
            }

            if (instance.TemplateId is null)
            {
                if (string.IsNullOrWhiteSpace(instance.DisplayName) ||
                    string.IsNullOrWhiteSpace(instance.DesignerName))
                {
                    throw new InvalidOperationException(
                        "Для локального расширения укажите отображаемое имя и имя в конфигураторе.");
                }
            }
            else
            {
                var template = await _templateRepository.GetByIdAsync(instance.TemplateId.Value, ct)
                    ?? throw new InvalidOperationException("Шаблон не найден.");
                if (string.IsNullOrWhiteSpace(instance.DisplayName))
                    instance.DisplayName = template.Name;
                if (string.IsNullOrWhiteSpace(instance.DesignerName))
                    throw new InvalidOperationException($"Укажите имя в конфигураторе для «{template.Name}».");
            }

            if (instance.Id == Guid.Empty)
                instance.Id = Guid.NewGuid();

            await _instanceRepository.SaveAsync(instance, ct);
        }
    }

    public async Task<InstanceExportPlan> BuildExportPlanAsync(Guid infobaseId, CancellationToken ct = default)
    {
        var profile = await _infobaseRepository.GetByIdAsync(infobaseId, ct)
            ?? throw new InvalidOperationException("База не найдена.");

        await EnsureBaseInstanceAsync(infobaseId, ct);
        var instances = await _instanceRepository.GetByInfobaseIdAsync(infobaseId, ct);

        if (instances.Count == 1 && !instances[0].ExportEnabled &&
            (profile.ExportConfiguration || profile.SelectedExtensions.Count > 0))
        {
            await ImportLegacyExportSettingsAsync(profile, ct);
            instances = await _instanceRepository.GetByInfobaseIdAsync(infobaseId, ct);
        }

        var enabled = instances
            .Where(i => i.ExportEnabled)
            .OrderBy(i => i.Kind == ConfigurationKind.Base ? 0 : 1)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.DisplayName)
            .Select(i => new ExportInstancePlan
            {
                InstanceId = i.Id,
                Kind = i.Kind,
                DisplayName = i.DisplayName,
                DesignerName = i.DesignerName
            })
            .ToList();

        return new InstanceExportPlan
        {
            Format = profile.ExportFormat,
            Instances = enabled
        };
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetExportSummariesForAllBasesAsync(
        CancellationToken ct = default)
    {
        var allInstances = await _instanceRepository.GetAllAsync(ct);
        return allInstances
            .GroupBy(i => i.InfobaseId)
            .ToDictionary(g => g.Key, g => BuildExportSummary(g.ToList()));
    }

    public static string BuildExportSummary(IReadOnlyList<ConfigurationInstance> instances)
    {
        var enabled = instances
            .Where(i => i.ExportEnabled)
            .OrderBy(i => i.Kind == ConfigurationKind.Base ? 0 : 1)
            .ThenBy(i => i.SortOrder)
            .Select(i => i.DisplayName)
            .ToList();

        return enabled.Count > 0 ? string.Join(", ", enabled) : "не задано";
    }

    public async Task<ConfigurationExport> GetOrCreateCurrentExportAsync(
        Guid instanceId,
        CancellationToken ct = default)
    {
        var current = await _exportRepository.GetCurrentByInstanceIdAsync(instanceId, ct);
        if (current is not null)
            return current;

        var export = new ConfigurationExport
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            IsCurrent = true
        };
        await _exportRepository.SaveAsync(export, ct);
        return export;
    }

    public async Task<IReadOnlyList<(ConfigurationInstance Instance, ConfigurationExport Export)>> GetEnabledInstancesWithExportsAsync(
        Guid infobaseId,
        CancellationToken ct = default)
    {
        var instances = (await GetInstancesAsync(infobaseId, ct))
            .Where(i => i.ExportEnabled)
            .OrderBy(i => i.Kind == ConfigurationKind.Base ? 0 : 1)
            .ThenBy(i => i.SortOrder)
            .ToList();

        var result = new List<(ConfigurationInstance, ConfigurationExport)>();
        foreach (var instance in instances)
        {
            var export = await GetOrCreateCurrentExportAsync(instance.Id, ct);
            result.Add((instance, export));
        }

        return result;
    }
}
