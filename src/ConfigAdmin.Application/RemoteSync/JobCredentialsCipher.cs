using System.Security.Cryptography;
using System.Text;

namespace ConfigAdmin.Application.RemoteSync;

public static class JobCredentialsCipher
{
    private const string HkdfInfo = "configadmin-sync-job-v1";

    public static byte[] Encrypt(string accessToken, Guid jobId, Guid nodeId, string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            return [];

        var key = DeriveKey(accessToken, jobId);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plainPassword);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plainBytes, cipher, tag, BuildAad(jobId, nodeId));

        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
        CryptographicOperations.ZeroMemory(plainBytes);
        return result;
    }

    public static string Decrypt(string accessToken, Guid jobId, Guid nodeId, byte[]? encryptedPayload)
    {
        if (encryptedPayload is null || encryptedPayload.Length == 0)
            return string.Empty;

        if (encryptedPayload.Length < 12 + 16)
            throw new InvalidOperationException("Некорректный формат encryptedConnectionPassword.");

        var nonce = encryptedPayload.AsSpan(0, 12);
        var tag = encryptedPayload.AsSpan(12, 16);
        var cipher = encryptedPayload.AsSpan(28);

        var key = DeriveKey(accessToken, jobId);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Decrypt(nonce, cipher, tag, plain, BuildAad(jobId, nodeId));

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] DeriveKey(string accessToken, Guid jobId)
    {
        var ikm = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, jobId.ToByteArray(), Encoding.UTF8.GetBytes(HkdfInfo));
    }

    private static byte[] BuildAad(Guid jobId, Guid nodeId)
    {
        var bytes = new byte[32];
        jobId.TryWriteBytes(bytes.AsSpan(0, 16));
        nodeId.TryWriteBytes(bytes.AsSpan(16, 16));
        return bytes;
    }
}
