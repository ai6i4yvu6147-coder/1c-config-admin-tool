using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class DataMcpRegistryFragmentDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = HubModuleIds.DataMcp;

    [JsonPropertyName("moduleType")]
    public string ModuleType { get; set; } = "data-mcp";

    [JsonPropertyName("registryFragment")]
    public DataMcpRegistryFragment RegistryFragment { get; set; } = new();
}

public sealed class DataMcpRegistryFragment
{
    [JsonPropertyName("yandex")]
    public DataMcpYandexFragment Yandex { get; set; } = new();

    [JsonPropertyName("connections")]
    public List<DataMcpConnectionFragment> Connections { get; set; } = [];
}

public sealed class DataMcpYandexFragment
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [JsonPropertyName("defaultPrefix")]
    public string DefaultPrefix { get; set; } = string.Empty;
}

public sealed class DataMcpConnectionFragment
{
    [JsonPropertyName("dataConnectionId")]
    public string DataConnectionId { get; set; } = string.Empty;

    [JsonPropertyName("infobaseId")]
    public string InfobaseId { get; set; } = string.Empty;

    [JsonPropertyName("databaseid")]
    public string DatabaseId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class DataMcpApplySecretsInput
{
    [JsonPropertyName("accessKeyId")]
    public string AccessKeyId { get; set; } = string.Empty;

    [JsonPropertyName("secretAccessKey")]
    public string SecretAccessKey { get; set; } = string.Empty;

    [JsonPropertyName("credentialsFile")]
    public string? CredentialsFile { get; set; }
}

public sealed class DataMcpApplySecretsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("appliedAt")]
    public string? AppliedAt { get; set; }

    [JsonPropertyName("credentialsPath")]
    public string? CredentialsPath { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("postApplyActions")]
    public ConfigMcpPostApplyActionsDto? PostApplyActions { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }
}

public sealed class DataMcpValidateConfigResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("checks")]
    public List<DataMcpValidateCheckDto> Checks { get; set; } = [];
}

public sealed class DataMcpValidateCheckDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
