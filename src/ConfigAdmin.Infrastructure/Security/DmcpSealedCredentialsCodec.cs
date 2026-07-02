using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConfigAdmin.Domain.Hub;
using Konscious.Security.Cryptography;

namespace ConfigAdmin.Infrastructure.Security;

public static class DmcpSealedCredentialsCodec
{
    public const int SaltSize = 16;
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int DefaultMemorySize = 65536;
    public const int DefaultIterations = 3;
    public const int DefaultParallelism = 4;

    private static readonly JsonSerializerOptions PlaintextJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static DmcpSealedCredentialsDocument Seal(
        string accessKeyId,
        string secretAccessKey,
        string dmcpPassword,
        byte[]? salt = null)
    {
        if (string.IsNullOrWhiteSpace(dmcpPassword))
            throw new ArgumentException("D-MCP password не может быть пустым.", nameof(dmcpPassword));

        salt ??= RandomNumberGenerator.GetBytes(SaltSize);
        if (salt.Length != SaltSize)
            throw new ArgumentException($"Salt must be {SaltSize} bytes.", nameof(salt));

        var plaintext = JsonSerializer.Serialize(new DmcpSealedCredentialsPlaintext
        {
            AccessKeyId = accessKeyId,
            SecretAccessKey = secretAccessKey
        }, PlaintextJsonOptions);

        var key = DeriveKey(dmcpPassword, salt);
        var payload = EncryptWithKey(key, plaintext);

        return new DmcpSealedCredentialsDocument
        {
            SchemaVersion = 1,
            Kdf = "argon2id",
            KdfParams = new DmcpSealedKdfParams
            {
                Salt = Convert.ToBase64String(salt),
                MemorySize = DefaultMemorySize,
                Iterations = DefaultIterations,
                Parallelism = DefaultParallelism
            },
            Cipher = "aes-256-gcm",
            Payload = Convert.ToBase64String(payload)
        };
    }

    public static DmcpSealedCredentialsPlaintext Unseal(
        DmcpSealedCredentialsDocument document,
        string dmcpPassword)
    {
        if (document.SchemaVersion != 1)
            throw new CryptographicException("Неподдерживаемая schemaVersion sealed-файла.");
        if (!string.Equals(document.Kdf, "argon2id", StringComparison.Ordinal))
            throw new CryptographicException("Неподдерживаемый KDF sealed-файла.");
        if (!string.Equals(document.Cipher, "aes-256-gcm", StringComparison.Ordinal))
            throw new CryptographicException("Неподдерживаемый cipher sealed-файла.");

        var salt = Convert.FromBase64String(document.KdfParams.Salt);
        var key = DeriveKey(
            dmcpPassword,
            salt,
            document.KdfParams.MemorySize,
            document.KdfParams.Iterations,
            document.KdfParams.Parallelism);
        var payload = Convert.FromBase64String(document.Payload);
        var json = DecryptWithKey(key, payload);

        return JsonSerializer.Deserialize<DmcpSealedCredentialsPlaintext>(json, PlaintextJsonOptions)
               ?? throw new CryptographicException("Некорректный plaintext sealed-файла.");
    }

    private static byte[] DeriveKey(
        string password,
        byte[] salt,
        int memorySize = DefaultMemorySize,
        int iterations = DefaultIterations,
        int parallelism = DefaultParallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memorySize,
            Iterations = iterations
        };
        return argon2.GetBytes(KeySize);
    }

    private static byte[] EncryptWithKey(byte[] key, string plainText)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var result = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);
        return result;
    }

    private static string DecryptWithKey(byte[] key, byte[] payload)
    {
        if (payload.Length < NonceSize + TagSize)
            throw new CryptographicException("Некорректные зашифрованные данные.");

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
