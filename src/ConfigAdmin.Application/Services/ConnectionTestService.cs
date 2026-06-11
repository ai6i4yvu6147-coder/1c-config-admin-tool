using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;

namespace ConfigAdmin.Application.Services;

public sealed class ConnectionTestService
{
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly ISecretVault _secretVault;
    private readonly IOneCCliAdapter _cliAdapter;

    public ConnectionTestService(
        IInfobaseRepository infobaseRepository,
        ISecretVault secretVault,
        IOneCCliAdapter cliAdapter)
    {
        _infobaseRepository = infobaseRepository;
        _secretVault = secretVault;
        _cliAdapter = cliAdapter;
    }

    public async Task<ProcessResult> TestByNameAsync(string baseName, CancellationToken ct = default)
    {
        var profile = await _infobaseRepository.GetByNameAsync(baseName, ct)
            ?? throw new InvalidOperationException($"База '{baseName}' не найдена.");

        return await TestAsync(profile, ct);
    }

    public async Task<ProcessResult> TestAsync(InfobaseProfile profile, CancellationToken ct = default)
    {
        if (!File.Exists(profile.PlatformPath))
            throw new FileNotFoundException($"Не найден 1cv8.exe: {profile.PlatformPath}", profile.PlatformPath);

        var password = profile.EncryptedPassword is { Length: > 0 }
            ? _secretVault.Decrypt(profile.EncryptedPassword)
            : null;

        return await _cliAdapter.TestConnectionAsync(profile, password, ct);
    }
}
