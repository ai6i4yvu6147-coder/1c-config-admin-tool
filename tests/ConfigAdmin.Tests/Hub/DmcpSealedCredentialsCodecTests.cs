using System.Text.Json;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Infrastructure.Security;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class DmcpSealedCredentialsCodecTests
{
    [Fact]
    public void Unseal_AgreedTestVector_MatchesPlaintext()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "kdf": "argon2id",
              "kdfParams": {
                "salt": "obLDNNXnBweSmkte1jjvkA==",
                "memorySize": 65536,
                "iterations": 3,
                "parallelism": 4
              },
              "cipher": "aes-256-gcm",
              "payload": "AQIDBAUGBwgJCgsMsEZbEjVmZkiENTP7PIq2jJtMk9zS0kbykZyThsSP6liih2SYJyRds6nsYZ2lefguNu1VPH5r3UpZgHOGmWEcnbcHQg3mYQFu+FtOu8MFHA=="
            }
            """;

        var document = JsonSerializer.Deserialize<DmcpSealedCredentialsDocument>(json)
                       ?? throw new InvalidOperationException("bad fixture");

        var plain = DmcpSealedCredentialsCodec.Unseal(document, "dmcp-test-vector-password");

        Assert.Equal("YCAJTESTKEY", plain.AccessKeyId);
        Assert.Equal("YCOSECRETTEST", plain.SecretAccessKey);
    }

    [Fact]
    public void Seal_RoundTrip_PreservesCredentials()
    {
        var document = DmcpSealedCredentialsCodec.Seal(
            "YCAJTESTKEY",
            "YCOSECRETTEST",
            "dmcp-test-vector-password",
            Convert.FromBase64String("obLDNNXnBweSmkte1jjvkA=="));

        var plain = DmcpSealedCredentialsCodec.Unseal(document, "dmcp-test-vector-password");

        Assert.Equal("YCAJTESTKEY", plain.AccessKeyId);
        Assert.Equal("YCOSECRETTEST", plain.SecretAccessKey);
    }
}
