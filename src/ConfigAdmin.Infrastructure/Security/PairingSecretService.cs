using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace ConfigAdmin.Infrastructure.Security;

public sealed class PairingSecretService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public byte[] CreateVerifier(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Pairing-пароль не может быть пустым.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = DeriveHash(password, salt);
        var verifier = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, verifier, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, verifier, SaltSize, HashSize);
        return verifier;
    }

    public bool Verify(string password, byte[] verifier)
    {
        if (string.IsNullOrWhiteSpace(password) || verifier.Length != SaltSize + HashSize)
            return false;

        var salt = verifier.AsSpan(0, SaltSize);
        var expected = verifier.AsSpan(SaltSize, HashSize);
        var actual = DeriveHash(password, salt);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] DeriveHash(string password, ReadOnlySpan<byte> salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt.ToArray(),
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        return argon2.GetBytes(HashSize);
    }
}
