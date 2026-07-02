namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpSettingsLoadResult
{
    public Guid ToolInstanceId { get; init; }
    public string RootPath { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string DefaultPrefix { get; init; } = string.Empty;
    public string SealedSecretsPath { get; init; } = "credentials.sealed.json";
    public bool HasStoredDmcpPassword { get; init; }
    public bool HasSealedCredentialsFile { get; init; }
    public string? PortableCredentialsPath { get; init; }
    public IReadOnlyList<DataMcpConnectionItem> Connections { get; init; } = [];
}

public sealed class DataMcpConnectionItem
{
    public Guid InfobaseId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string InfobaseName { get; init; } = string.Empty;
    public Guid? ConnectionId { get; init; }
    public string DatabaseId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public bool IsPaired => !string.IsNullOrWhiteSpace(DatabaseId);
}

public sealed class DataMcpSettingsSaveRequest
{
    public Guid ToolInstanceId { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string DefaultPrefix { get; init; } = string.Empty;
    public string SealedSecretsPath { get; init; } = "credentials.sealed.json";
    public string? DmcpPassword { get; init; }
    public string? AccessKeyId { get; init; }
    public string? SecretAccessKey { get; init; }
    public IReadOnlyList<DataMcpConnectionItem> Connections { get; init; } = [];
}
