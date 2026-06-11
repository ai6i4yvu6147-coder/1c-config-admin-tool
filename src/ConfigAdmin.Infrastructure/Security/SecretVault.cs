using System.Security.Cryptography;
using System.Text;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using Konscious.Security.Cryptography;

namespace ConfigAdmin.Infrastructure.Security;

public sealed class SecretVault : ISecretVault, IDisposable
{
    private const string SaltKey = "vault.salt";
    private const string VerifierKey = "vault.verifier";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IVaultMetaRepository _vaultMetaRepository;
    private byte[]? _key;
    private bool? _isInitialized;

    public SecretVault(IVaultMetaRepository vaultMetaRepository)
    {
        _vaultMetaRepository = vaultMetaRepository;
    }

    public bool IsInitialized => _isInitialized ?? false;
    public bool IsUnlocked => _key is not null;

    public async Task<bool> CheckInitializedAsync(CancellationToken ct = default)
    {
        _isInitialized ??= await _vaultMetaRepository.ExistsAsync(SaltKey, ct);
        return _isInitialized.Value;
    }

    public async Task InitializeAsync(string masterPassword, CancellationToken ct = default)
    {
        if (await _vaultMetaRepository.ExistsAsync(SaltKey, ct))
            throw new InvalidOperationException("Хранилище уже инициализировано.");

        if (string.IsNullOrWhiteSpace(masterPassword))
            throw new ArgumentException("Мастер-пароль не может быть пустым.", nameof(masterPassword));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(masterPassword, salt);
        var verifier = EncryptWithKey(key, "configadmin-vault-verifier");

        await _vaultMetaRepository.SetAsync(SaltKey, salt, ct);
        await _vaultMetaRepository.SetAsync(VerifierKey, verifier, ct);

        _isInitialized = true;
        _key = key;
    }

    public async Task UnlockAsync(string masterPassword, CancellationToken ct = default)
    {
        if (!await CheckInitializedAsync(ct))
            throw new InvalidOperationException("Хранилище не инициализировано. Выполните init-vault.");

        var salt = await _vaultMetaRepository.GetAsync(SaltKey, ct)
            ?? throw new InvalidOperationException("Соль хранилища не найдена.");
        var verifier = await _vaultMetaRepository.GetAsync(VerifierKey, ct)
            ?? throw new InvalidOperationException("Верификатор хранилища не найден.");

        var key = DeriveKey(masterPassword, salt);
        try
        {
            var plain = DecryptWithKey(key, verifier);
            if (plain != "configadmin-vault-verifier")
                throw new CryptographicException("Неверный мастер-пароль.");
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("Неверный мастер-пароль.");
        }

        Lock();
        _key = key;
    }

    public byte[] Encrypt(string plainText)
    {
        EnsureUnlocked();
        return EncryptWithKey(_key!, plainText);
    }

    public string Decrypt(byte[] cipherText)
    {
        EnsureUnlocked();
        return DecryptWithKey(_key!, cipherText);
    }

    public void Lock()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }

    public void Dispose() => Lock();

    private void EnsureUnlocked()
    {
        if (_key is null)
            throw new InvalidOperationException("Хранилище заблокировано. Введите мастер-пароль.");
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
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
