namespace ConfigAdmin.Domain.Security;

public interface ISecretVault
{
    bool IsInitialized { get; }
    bool IsUnlocked { get; }

    Task<bool> CheckInitializedAsync(CancellationToken ct = default);
    Task InitializeAsync(string masterPassword, CancellationToken ct = default);
    Task UnlockAsync(string masterPassword, CancellationToken ct = default);
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] cipherText);
    void Lock();
}
