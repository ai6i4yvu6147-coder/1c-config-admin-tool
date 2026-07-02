using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;

namespace ConfigAdmin.Application.Hub;

public sealed class InfobaseContextService
{
    private readonly IClientRepository _clientRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IConfigurationInstanceRepository _instanceRepository;
    private readonly IConfigurationExportRepository _exportRepository;
    private readonly IDataConnectionRepository _dataConnectionRepository;
    private readonly IConfigMcpToolClient _configMcpToolClient;
    private readonly IDataMcpToolClient _dataMcpToolClient;

    public InfobaseContextService(
        IClientRepository clientRepository,
        IInfobaseRepository infobaseRepository,
        IConfigurationInstanceRepository instanceRepository,
        IConfigurationExportRepository exportRepository,
        IDataConnectionRepository dataConnectionRepository,
        IConfigMcpToolClient configMcpToolClient,
        IDataMcpToolClient dataMcpToolClient)
    {
        _clientRepository = clientRepository;
        _infobaseRepository = infobaseRepository;
        _instanceRepository = instanceRepository;
        _exportRepository = exportRepository;
        _dataConnectionRepository = dataConnectionRepository;
        _configMcpToolClient = configMcpToolClient;
        _dataMcpToolClient = dataMcpToolClient;
    }

    public async Task<IReadOnlyList<HubClientListItemDto>> ListClientsAsync(CancellationToken ct = default)
    {
        var clients = await _clientRepository.GetAllAsync(ct);
        return clients
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new HubClientListItemDto
            {
                ClientId = c.Id.ToString(),
                Name = c.Name,
                ExportRootPath = c.ExportRootPath
            })
            .ToList();
    }

    public async Task<IReadOnlyList<HubInfobaseListItemDto>> ListInfobasesAsync(CancellationToken ct = default)
    {
        var clients = (await _clientRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);
        var infobases = await _infobaseRepository.GetAllAsync(ct);

        return infobases
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i =>
            {
                clients.TryGetValue(i.ClientId, out var client);
                return new HubInfobaseListItemDto
                {
                    InfobaseId = i.Id.ToString(),
                    Name = i.Name,
                    ClientId = i.ClientId.ToString(),
                    ClientName = client?.Name ?? string.Empty
                };
            })
            .ToList();
    }

    public async Task<InfobaseContextDocument> ResolveAsync(
        string? infobaseId,
        string? infobaseName,
        CancellationToken ct = default)
    {
        var infobase = await ResolveInfobaseAsync(infobaseId, infobaseName, ct);
        var client = await _clientRepository.GetByIdAsync(infobase.ClientId, ct)
                     ?? throw new InvalidOperationException("Клиент инфобазы не найден.");

        return new InfobaseContextDocument
        {
            InfobaseId = infobase.Id.ToString(),
            InfobaseName = infobase.Name,
            ClientName = client.Name,
            ConfigMcp = await BuildConfigMcpContextAsync(infobase, client, ct),
            DataMcp = await BuildDataMcpContextAsync(infobase, ct)
        };
    }

    private async Task<InfobaseProfile> ResolveInfobaseAsync(
        string? infobaseId,
        string? infobaseName,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(infobaseId))
        {
            if (!Guid.TryParse(infobaseId, out var id))
                throw new InvalidOperationException("Некорректный infobaseId.");

            return await _infobaseRepository.GetByIdAsync(id, ct)
                   ?? throw new InvalidOperationException($"Инфобаза не найдена: {infobaseId}");
        }

        if (!string.IsNullOrWhiteSpace(infobaseName))
        {
            return await _infobaseRepository.GetByNameAsync(infobaseName.Trim(), ct)
                   ?? throw new InvalidOperationException($"Инфобаза не найдена: {infobaseName}");
        }

        throw new InvalidOperationException("Укажите infobaseId или infobaseName.");
    }

    private async Task<InfobaseContextConfigMcpDto?> BuildConfigMcpContextAsync(
        InfobaseProfile infobase,
        ClientProfile client,
        CancellationToken ct)
    {
        var instances = await _instanceRepository.GetByInfobaseIdAsync(infobase.Id, ct);
        var linkedInstances = instances.Where(i => i.IsMcpLinked).ToList();
        var projectId = linkedInstances
                            .Select(i => i.ConfigMcpProjectId)
                            .FirstOrDefault(id => id is Guid value && value != Guid.Empty)
                        ?? infobase.ConfigMcpProjectId;

        if (projectId is not Guid resolvedProjectId || resolvedProjectId == Guid.Empty)
            return null;

        var exports = await _exportRepository.GetByInstanceIdsAsync(linkedInstances.Select(i => i.Id), ct);
        var exportByInstance = exports
            .Where(e => e.IsCurrent)
            .GroupBy(e => e.InstanceId)
            .ToDictionary(g => g.Key, g => g.First());

        var configMcpStatus = await TryGetConfigMcpStatusAsync(ct);
        var projectEntry = configMcpStatus?.Projects.FirstOrDefault(p =>
            string.Equals(p.ProjectId, resolvedProjectId.ToString(), StringComparison.OrdinalIgnoreCase));
        var projectFilter = projectEntry?.Name
            ?? BuildDefaultProjectFilter(client.Name, infobase.Name);

        var instanceDtos = new List<InfobaseContextConfigMcpInstanceDto>();
        foreach (var instance in linkedInstances.OrderBy(i => i.SortOrder))
        {
            exportByInstance.TryGetValue(instance.Id, out var export);
            var databaseId = instance.ConfigMcpDatabaseId ?? export?.Id;
            if (databaseId is null || databaseId == Guid.Empty)
                continue;

            var databaseIdText = databaseId.Value.ToString();
            var extensionFilter = projectEntry?.Databases
                .FirstOrDefault(d => string.Equals(d.InfobaseId, databaseIdText, StringComparison.OrdinalIgnoreCase))
                ?.Name
                ?? instance.DisplayName.Trim();

            instanceDtos.Add(new InfobaseContextConfigMcpInstanceDto
            {
                DatabaseId = databaseIdText,
                DisplayName = instance.DisplayName,
                ExtensionFilter = extensionFilter,
                Type = instance.Kind == ConfigurationKind.Base ? "base" : "extension"
            });
        }

        return new InfobaseContextConfigMcpDto
        {
            ProjectId = resolvedProjectId.ToString(),
            ProjectFilter = projectFilter,
            ProjectName = projectFilter,
            Instances = instanceDtos
        };
    }

    private async Task<ConfigMcpStatusResponse?> TryGetConfigMcpStatusAsync(CancellationToken ct)
    {
        try
        {
            return await _configMcpToolClient.GetStatusAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    internal static string BuildDefaultProjectFilter(string clientName, string infobaseName) =>
        $"{clientName.Trim()} / {infobaseName.Trim()}";

    private async Task<InfobaseContextDataMcpDto?> BuildDataMcpContextAsync(
        InfobaseProfile infobase,
        CancellationToken ct)
    {
        var connection = await _dataConnectionRepository.GetByInfobaseIdAsync(infobase.Id, ct);
        if (connection is null || string.IsNullOrWhiteSpace(connection.DatabaseId))
            return null;

        return new InfobaseContextDataMcpDto
        {
            DataConnectionId = connection.Id.ToString(),
            DatabaseId = connection.DatabaseId,
            Paired = true,
            CredentialsState = await ResolveCredentialsStateAsync(ct)
        };
    }

    private async Task<string> ResolveCredentialsStateAsync(CancellationToken ct)
    {
        try
        {
            var status = await _dataMcpToolClient.GetStatusAsync(ct);
            var details = status.Details;
            if (details is null)
                return "unknown";

            if (details.CredentialsResolvable)
                return "unlocked";

            if (details.CredentialsExists)
                return "locked";

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
