using ConfigAdmin.Domain.Security;

namespace ConfigAdmin.Application.Services;

public sealed class VaultSessionService
{
    private readonly ISecretVault _secretVault;

    public VaultSessionService(ISecretVault secretVault)
    {
        _secretVault = secretVault;
    }

    public bool IsInitialized => _secretVault.IsInitialized;
    public bool IsUnlocked => _secretVault.IsUnlocked;

    public Task<bool> CheckInitializedAsync(CancellationToken ct = default) =>
        _secretVault.CheckInitializedAsync(ct);

    public Task InitializeAsync(string masterPassword, CancellationToken ct = default) =>
        _secretVault.InitializeAsync(masterPassword, ct);

    public Task UnlockAsync(string masterPassword, CancellationToken ct = default) =>
        _secretVault.UnlockAsync(masterPassword, ct);

    public void Lock() => _secretVault.Lock();
}
