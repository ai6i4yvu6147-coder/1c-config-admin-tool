using ConfigAdmin.Application.RemoteSync;
using Xunit;

namespace ConfigAdmin.Tests;

public class JobCredentialsCipherTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginalPassword()
    {
        const string accessToken = "test-access-token-abc123";
        const string plainPassword = "1C-base-password!@#";
        var jobId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var encrypted = JobCredentialsCipher.Encrypt(accessToken, jobId, nodeId, plainPassword);
        var decrypted = JobCredentialsCipher.Decrypt(accessToken, jobId, nodeId, encrypted);

        Assert.Equal(plainPassword, decrypted);
    }

    [Fact]
    public void Encrypt_EmptyPassword_ReturnsEmptyArray()
    {
        var encrypted = JobCredentialsCipher.Encrypt("token", Guid.NewGuid(), Guid.NewGuid(), string.Empty);
        Assert.Empty(encrypted);
    }

    [Fact]
    public void Decrypt_WrongAccessToken_Throws()
    {
        var jobId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var encrypted = JobCredentialsCipher.Encrypt("token-a", jobId, nodeId, "secret");

        Assert.ThrowsAny<Exception>(() =>
            JobCredentialsCipher.Decrypt("token-b", jobId, nodeId, encrypted));
    }

    [Fact]
    public void Decrypt_WrongNodeId_Throws()
    {
        var jobId = Guid.NewGuid();
        const string token = "same-token";
        var encrypted = JobCredentialsCipher.Encrypt(token, jobId, Guid.NewGuid(), "secret");

        Assert.ThrowsAny<Exception>(() =>
            JobCredentialsCipher.Decrypt(token, jobId, Guid.NewGuid(), encrypted));
    }

    [Fact]
    public void Decrypt_WrongJobId_Throws()
    {
        const string token = "same-token";
        var nodeId = Guid.NewGuid();
        var encrypted = JobCredentialsCipher.Encrypt(token, Guid.NewGuid(), nodeId, "secret");

        Assert.ThrowsAny<Exception>(() =>
            JobCredentialsCipher.Decrypt(token, Guid.NewGuid(), nodeId, encrypted));
    }
}
