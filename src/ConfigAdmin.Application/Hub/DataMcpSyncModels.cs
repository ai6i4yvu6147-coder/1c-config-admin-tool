namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpPortableSyncOptions
{
    public string? AccessKeyId { get; init; }
    public string? SecretAccessKey { get; init; }
    public string? DmcpPassword { get; init; }
}

public sealed class DataMcpPortableSyncResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool Skipped { get; init; }
    public bool SecretsApplied { get; init; }
    public string? CredentialsPath { get; init; }
    public bool RegistryApplied { get; init; }
    public bool ConfigValid { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
