using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class InfobaseContextDocument
{
    [JsonPropertyName("infobaseId")]
    public string InfobaseId { get; set; } = string.Empty;

    [JsonPropertyName("infobaseName")]
    public string InfobaseName { get; set; } = string.Empty;

    [JsonPropertyName("clientName")]
    public string ClientName { get; set; } = string.Empty;

    [JsonPropertyName("configMcp")]
    public InfobaseContextConfigMcpDto? ConfigMcp { get; set; }

    [JsonPropertyName("dataMcp")]
    public InfobaseContextDataMcpDto? DataMcp { get; set; }
}

public sealed class InfobaseContextConfigMcpDto
{
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Exact config-mcp <c>project_filter</c> (from portable registry when available).</summary>
    [JsonPropertyName("projectFilter")]
    public string ProjectFilter { get; set; } = string.Empty;

    /// <summary>Human label; same as <see cref="ProjectFilter"/> when resolved from portable.</summary>
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("instances")]
    public List<InfobaseContextConfigMcpInstanceDto> Instances { get; set; } = [];
}

public sealed class InfobaseContextConfigMcpInstanceDto
{
    [JsonPropertyName("databaseId")]
    public string DatabaseId { get; set; } = string.Empty;

    /// <summary>Hub configuration instance label (short).</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Exact config-mcp <c>extension_filter</c> when scoped to a single database.</summary>
    [JsonPropertyName("extensionFilter")]
    public string ExtensionFilter { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public sealed class InfobaseContextDataMcpDto
{
    [JsonPropertyName("dataConnectionId")]
    public string DataConnectionId { get; set; } = string.Empty;

    [JsonPropertyName("databaseId")]
    public string DatabaseId { get; set; } = string.Empty;

    [JsonPropertyName("paired")]
    public bool Paired { get; set; }

    [JsonPropertyName("credentialsState")]
    public string CredentialsState { get; set; } = "unknown";
}

public sealed class HubClientListItemDto
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("exportRootPath")]
    public string ExportRootPath { get; set; } = string.Empty;
}

public sealed class HubInfobaseListItemDto
{
    [JsonPropertyName("infobaseId")]
    public string InfobaseId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientName")]
    public string ClientName { get; set; } = string.Empty;
}
