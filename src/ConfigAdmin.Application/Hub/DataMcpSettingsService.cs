using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;

namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpSettingsService
{
    private readonly ManagedToolRegistryService _registryService;
    private readonly IDataMcpSettingsRepository _settingsRepository;
    private readonly IDataConnectionRepository _connectionRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ISecretVault _secretVault;
    private readonly VaultSessionService _vaultSessionService;
    private readonly DataMcpSealedCredentialsService _sealedCredentialsService;

    public DataMcpSettingsService(
        ManagedToolRegistryService registryService,
        IDataMcpSettingsRepository settingsRepository,
        IDataConnectionRepository connectionRepository,
        IInfobaseRepository infobaseRepository,
        IClientRepository clientRepository,
        ISecretVault secretVault,
        VaultSessionService vaultSessionService,
        DataMcpSealedCredentialsService sealedCredentialsService)
    {
        _registryService = registryService;
        _settingsRepository = settingsRepository;
        _connectionRepository = connectionRepository;
        _infobaseRepository = infobaseRepository;
        _clientRepository = clientRepository;
        _secretVault = secretVault;
        _vaultSessionService = vaultSessionService;
        _sealedCredentialsService = sealedCredentialsService;
    }

    public async Task<DataMcpSettingsLoadResult> LoadAsync(CancellationToken ct = default)
    {
        var tool = await _registryService.GetOrCreateDataMcpInstanceAsync(ct);
        var stored = await _settingsRepository.GetByToolInstanceIdAsync(tool.Id, ct);
        var connections = await BuildConnectionItemsAsync(ct);
        var sealedPath = stored?.SealedSecretsPath ?? "credentials.sealed.json";
        var credentialsRelative = _sealedCredentialsService.GetExistingCredentialsRelativePath(tool.RootPath, sealedPath);

        return new DataMcpSettingsLoadResult
        {
            ToolInstanceId = tool.Id,
            RootPath = tool.RootPath,
            Endpoint = stored?.Endpoint ?? "https://storage.yandexcloud.net",
            Region = stored?.Region ?? "ru-central1",
            Bucket = stored?.Bucket ?? string.Empty,
            DefaultPrefix = stored?.DefaultPrefix ?? string.Empty,
            SealedSecretsPath = sealedPath,
            HasStoredDmcpPassword = stored?.EncryptedDmcpPassword is { Length: > 0 },
            HasSealedCredentialsFile = credentialsRelative is not null,
            PortableCredentialsPath = credentialsRelative,
            Connections = connections
        };
    }

    public Task SaveRootPathAsync(string rootPath, CancellationToken ct = default) =>
        _registryService.SaveDataMcpRootPathAsync(rootPath, ct);

    public async Task SaveAsync(DataMcpSettingsSaveRequest request, CancellationToken ct = default)
    {
        if (!_vaultSessionService.IsUnlocked)
            throw new InvalidOperationException("Разблокируйте хранилище Hub для сохранения.");

        if (string.IsNullOrWhiteSpace(request.Endpoint))
            throw new InvalidOperationException("Укажите endpoint S3.");
        if (string.IsNullOrWhiteSpace(request.Region))
            throw new InvalidOperationException("Укажите region.");
        if (string.IsNullOrWhiteSpace(request.Bucket))
            throw new InvalidOperationException("Укажите bucket.");

        var tool = await _registryService.GetOrCreateDataMcpInstanceAsync(ct);
        var existing = await _settingsRepository.GetByToolInstanceIdAsync(request.ToolInstanceId, ct);
        byte[]? encryptedPassword = existing?.EncryptedDmcpPassword;
        if (!string.IsNullOrEmpty(request.DmcpPassword))
            encryptedPassword = _secretVault.Encrypt(request.DmcpPassword);

        var sealedSecretsPath = string.IsNullOrWhiteSpace(request.SealedSecretsPath)
            ? "credentials.sealed.json"
            : request.SealedSecretsPath.Trim();

        await _settingsRepository.SaveAsync(new DataMcpSettings
        {
            ToolInstanceId = request.ToolInstanceId,
            Endpoint = request.Endpoint.Trim(),
            Region = request.Region.Trim(),
            Bucket = request.Bucket.Trim(),
            DefaultPrefix = request.DefaultPrefix.Trim(),
            SealedSecretsPath = sealedSecretsPath,
            EncryptedDmcpPassword = encryptedPassword
        }, ct);

        var existingConnections = await _connectionRepository.GetAllAsync(ct);
        var existingByInfobase = existingConnections.ToDictionary(c => c.InfobaseId);

        foreach (var item in request.Connections)
        {
            var databaseId = item.DatabaseId.Trim();
            var displayName = string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.InfobaseName
                : item.DisplayName.Trim();

            if (string.IsNullOrWhiteSpace(databaseId))
            {
                if (item.ConnectionId is Guid connectionId)
                    await _connectionRepository.DeleteAsync(connectionId, ct);
                else if (existingByInfobase.TryGetValue(item.InfobaseId, out var existingConnection))
                    await _connectionRepository.DeleteAsync(existingConnection.Id, ct);

                continue;
            }

            await _connectionRepository.SaveAsync(new DataConnection
            {
                Id = item.ConnectionId ?? Guid.NewGuid(),
                InfobaseId = item.InfobaseId,
                DatabaseId = databaseId,
                DisplayName = displayName
            }, ct);
        }
    }

    private async Task<IReadOnlyList<DataMcpConnectionItem>> BuildConnectionItemsAsync(CancellationToken ct)
    {
        var clients = await _clientRepository.GetAllAsync(ct);
        var clientNames = clients.ToDictionary(c => c.Id, c => c.Name);
        var infobases = await _infobaseRepository.GetAllAsync(ct);
        var connections = await _connectionRepository.GetAllAsync(ct);
        var connectionByInfobase = connections.ToDictionary(c => c.InfobaseId);

        return infobases
            .OrderBy(i => clientNames.GetValueOrDefault(i.ClientId, string.Empty))
            .ThenBy(i => i.Name)
            .Select(infobase =>
            {
                connectionByInfobase.TryGetValue(infobase.Id, out var connection);
                return new DataMcpConnectionItem
                {
                    InfobaseId = infobase.Id,
                    ClientName = clientNames.GetValueOrDefault(infobase.ClientId, "—"),
                    InfobaseName = infobase.Name,
                    ConnectionId = connection?.Id,
                    DatabaseId = connection?.DatabaseId ?? string.Empty,
                    DisplayName = connection?.DisplayName ?? infobase.Name
                };
            })
            .ToList();
    }
}
