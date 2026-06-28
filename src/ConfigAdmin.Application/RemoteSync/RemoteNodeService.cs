using System.Security.Cryptography;
using System.Text;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class RemoteNodeService
{
    private readonly IRemoteNodeRepository _remoteNodeRepository;
    private readonly IClientRepository _clientRepository;
    private readonly PairingSecretService _pairingSecretService;

    public RemoteNodeService(
        IRemoteNodeRepository remoteNodeRepository,
        IClientRepository clientRepository,
        PairingSecretService pairingSecretService)
    {
        _remoteNodeRepository = remoteNodeRepository;
        _clientRepository = clientRepository;
        _pairingSecretService = pairingSecretService;
    }

    public Task<IReadOnlyList<RemoteNodeProfile>> GetAllAsync(CancellationToken ct = default) =>
        _remoteNodeRepository.GetAllAsync(ct);

    public Task<RemoteNodeProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _remoteNodeRepository.GetByIdAsync(id, ct);

    public async Task<RemoteNodeProfile> CreateOrUpdateAsync(
        Guid? id,
        Guid clientId,
        string name,
        string? description,
        string? pairingPassword,
        string? hubListenUrl,
        bool enabled,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Укажите имя узла.", nameof(name));

        var client = await _clientRepository.GetByIdAsync(clientId, ct)
            ?? throw new InvalidOperationException("Клиент не найден.");

        var node = id is { } existingId
            ? await _remoteNodeRepository.GetByIdAsync(existingId, ct)
              ?? throw new InvalidOperationException("Узел не найден.")
            : new RemoteNodeProfile { Id = Guid.NewGuid(), ClientId = client.Id };

        node.ClientId = client.Id;
        node.Name = name.Trim();
        node.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        node.HubListenUrl = string.IsNullOrWhiteSpace(hubListenUrl) ? null : hubListenUrl.Trim();
        node.Enabled = enabled;

        if (!string.IsNullOrWhiteSpace(pairingPassword))
            node.PairingSecretVerifier = _pairingSecretService.CreateVerifier(pairingPassword);
        else if (node.PairingSecretVerifier.Length == 0)
            throw new InvalidOperationException("Задайте pairing-пароль для нового узла.");

        await _remoteNodeRepository.SaveAsync(node, ct);
        return node;
    }
}
