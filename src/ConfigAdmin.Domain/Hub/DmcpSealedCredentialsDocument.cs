using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class DmcpSealedCredentialsDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("kdf")]
    public string Kdf { get; set; } = "argon2id";

    [JsonPropertyName("kdfParams")]
    public DmcpSealedKdfParams KdfParams { get; set; } = new();

    [JsonPropertyName("cipher")]
    public string Cipher { get; set; } = "aes-256-gcm";

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
}

public sealed class DmcpSealedKdfParams
{
    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;

    [JsonPropertyName("memorySize")]
    public int MemorySize { get; set; } = 65536;

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; } = 3;

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 4;
}

public sealed class DmcpSealedCredentialsPlaintext
{
    [JsonPropertyName("accessKeyId")]
    public string AccessKeyId { get; set; } = string.Empty;

    [JsonPropertyName("secretAccessKey")]
    public string SecretAccessKey { get; set; } = string.Empty;
}
