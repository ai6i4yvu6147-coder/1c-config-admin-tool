using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class ConfigMcpRegistryFragmentDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = HubModuleIds.ConfigMcp;

    [JsonPropertyName("moduleType")]
    public string ModuleType { get; set; } = "config-mcp";

    [JsonPropertyName("exportedAt")]
    public string ExportedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    [JsonPropertyName("registryFragment")]
    public ConfigMcpRegistryFragment RegistryFragment { get; set; } = new();
}

public sealed class ConfigMcpRegistryFragment
{
    [JsonPropertyName("projects")]
    public List<ConfigMcpRegistryProjectDto> Projects { get; set; } = [];
}

public sealed class ConfigMcpRegistryProjectDto
{
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("databases")]
    public List<ConfigMcpRegistryDatabaseDto> Databases { get; set; } = [];
}

public sealed class ConfigMcpRegistryDatabaseDto
{
    [JsonPropertyName("infobaseId")]
    public string InfobaseId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "base";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; set; } = "directory";

    [JsonPropertyName("platformVersion")]
    public string PlatformVersion { get; set; } = string.Empty;
}
