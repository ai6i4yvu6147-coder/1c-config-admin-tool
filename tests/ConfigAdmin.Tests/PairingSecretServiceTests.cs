using ConfigAdmin.Infrastructure.Security;
using Xunit;

namespace ConfigAdmin.Tests;

public class PairingSecretServiceTests
{
    private readonly PairingSecretService _service = new();

    [Fact]
    public void CreateVerifier_AndVerify_SucceedsForSamePassword()
    {
        var verifier = _service.CreateVerifier("pairing-test-123");

        Assert.True(_service.Verify("pairing-test-123", verifier));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var verifier = _service.CreateVerifier("correct-password");

        Assert.False(_service.Verify("wrong-password", verifier));
    }

    [Fact]
    public void CreateVerifier_WithEmptyPassword_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.CreateVerifier(" "));
    }
}
