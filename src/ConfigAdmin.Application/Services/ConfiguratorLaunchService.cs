using System.Diagnostics;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Integration.OneC;

namespace ConfigAdmin.Application.Services;

public sealed class ConfiguratorLaunchService
{
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly ISecretVault _secretVault;

    public ConfiguratorLaunchService(IInfobaseRepository infobaseRepository, ISecretVault secretVault)
    {
        _infobaseRepository = infobaseRepository;
        _secretVault = secretVault;
    }

    public async Task LaunchAsync(Guid infobaseId, CancellationToken ct = default)
    {
        var profile = await _infobaseRepository.GetByIdAsync(infobaseId, ct)
            ?? throw new InvalidOperationException($"База {infobaseId} не найдена.");

        if (!File.Exists(profile.PlatformPath))
            throw new FileNotFoundException($"Не найден 1cv8.exe: {profile.PlatformPath}", profile.PlatformPath);

        var password = profile.EncryptedPassword is { Length: > 0 }
            ? _secretVault.Decrypt(profile.EncryptedPassword)
            : null;

        var connection = new ConnectionSettings
        {
            ConnectionType = profile.ConnectionType,
            ConnectionString = profile.ConnectionString,
            Username = profile.Username,
            Password = password
        };

        var arguments = OneCCommandBuilder.BuildOpenConfiguratorCommand(connection);

        Process.Start(new ProcessStartInfo
        {
            FileName = profile.PlatformPath,
            Arguments = arguments,
            UseShellExecute = true
        });
    }
}
