using Xunit;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Infrastructure.Security;

namespace ConfigAdmin.Tests;

public class SecretVaultTests
{
    [Fact]
    public async Task InitializeAndUnlock_RoundTripSecret()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"vault-test-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var metaRepo = new VaultMetaRepository(factory);
        var vault = new SecretVault(metaRepo);

        await vault.InitializeAsync("master-password-123");
        var encrypted = vault.Encrypt("base-secret");
        vault.Lock();

        await vault.UnlockAsync("master-password-123");
        var plain = vault.Decrypt(encrypted);

        Assert.Equal("base-secret", plain);
    }

    [Fact]
    public async Task Unlock_WithWrongPassword_Throws()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"vault-test-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);
        await new DatabaseInitializer(factory).InitializeAsync();

        var vault = new SecretVault(new VaultMetaRepository(factory));
        await vault.InitializeAsync("correct-password");
        vault.Lock();

        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(
            () => vault.UnlockAsync("wrong-password"));
    }
}
