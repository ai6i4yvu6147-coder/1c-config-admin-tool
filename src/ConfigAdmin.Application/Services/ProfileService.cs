using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;

namespace ConfigAdmin.Application.Services;

public sealed class ProfileService
{
    private readonly IClientRepository _clientRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly ISecretVault _secretVault;

    public ProfileService(
        IClientRepository clientRepository,
        IInfobaseRepository infobaseRepository,
        ISecretVault secretVault)
    {
        _clientRepository = clientRepository;
        _infobaseRepository = infobaseRepository;
        _secretVault = secretVault;
    }

    public Task<IReadOnlyList<ClientProfile>> GetClientsAsync(CancellationToken ct = default) =>
        _clientRepository.GetAllAsync(ct);

    public Task<ClientProfile?> GetClientByNameAsync(string name, CancellationToken ct = default) =>
        _clientRepository.GetByNameAsync(name, ct);

    public async Task<ClientProfile> AddOrUpdateClientAsync(
        string name,
        string exportRoot,
        string? comment = null,
        CancellationToken ct = default)
    {
        var existing = await _clientRepository.GetByNameAsync(name, ct);
        var client = existing ?? new ClientProfile { Id = Guid.NewGuid() };
        client.Name = name;
        client.ExportRootPath = exportRoot;
        client.Comment = comment;
        await _clientRepository.SaveAsync(client, ct);
        return client;
    }

    public Task<IReadOnlyList<InfobaseProfile>> GetInfobasesAsync(CancellationToken ct = default) =>
        _infobaseRepository.GetAllAsync(ct);

    public Task<InfobaseProfile?> GetInfobaseByNameAsync(string name, CancellationToken ct = default) =>
        _infobaseRepository.GetByNameAsync(name, ct);

    public Task<InfobaseProfile?> GetInfobaseByIdAsync(Guid id, CancellationToken ct = default) =>
        _infobaseRepository.GetByIdAsync(id, ct);

    public async Task<InfobaseProfile> AddOrUpdateInfobaseAsync(
        string clientName,
        string baseName,
        string platformPath,
        ConnectionType connectionType,
        string connectionString,
        string? username,
        string? password,
        bool exportConfiguration = true,
        bool exportAllExtensions = true,
        IEnumerable<string>? selectedExtensions = null,
        ExportFormat format = ExportFormat.Hierarchical,
        ExportLocation exportLocation = ExportLocation.Local,
        Guid? remoteNodeId = null,
        string? remoteExportPath = null,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.GetByNameAsync(clientName, ct)
            ?? throw new InvalidOperationException($"Клиент '{clientName}' не найден.");

        var profile = await _infobaseRepository.GetByNameAsync(baseName, ct)
                      ?? new InfobaseProfile { Id = Guid.NewGuid(), ClientId = client.Id };

        profile.ClientId = client.Id;
        profile.Name = baseName;
        profile.PlatformPath = platformPath;
        profile.ConnectionType = connectionType;
        profile.ConnectionString = connectionString;
        profile.Username = username;
        profile.ExportConfiguration = exportConfiguration;
        profile.ExportAllExtensions = exportAllExtensions;
        profile.SelectedExtensions = selectedExtensions?.ToList() ?? [];
        profile.ExportFormat = format;
        profile.ExportLocation = exportLocation;
        profile.RemoteNodeId = remoteNodeId;
        profile.RemoteExportPath = string.IsNullOrWhiteSpace(remoteExportPath) ? null : remoteExportPath.Trim();

        if (!string.IsNullOrEmpty(password))
            profile.EncryptedPassword = _secretVault.Encrypt(password);

        await _infobaseRepository.SaveAsync(profile, ct);
        return profile;
    }

    public async Task DeleteClientAsync(Guid id, CancellationToken ct = default) =>
        await _clientRepository.DeleteAsync(id, ct);

    public async Task DeleteInfobaseAsync(Guid id, CancellationToken ct = default) =>
        await _infobaseRepository.DeleteAsync(id, ct);
}
