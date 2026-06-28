using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class ConfigMcpStatusResponse
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("readiness")]
    public string Readiness { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("projects")]
    public List<ConfigMcpStatusProjectDto> Projects { get; set; } = [];
}

public sealed class ConfigMcpStatusProjectDto
{
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("databases")]
    public List<ConfigMcpStatusDatabaseDto> Databases { get; set; } = [];
}

public sealed class ConfigMcpStatusDatabaseDto
{
    [JsonPropertyName("infobaseId")]
    public string InfobaseId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sourcePath")]
    public string? SourcePath { get; set; }

    [JsonPropertyName("sourceKind")]
    public string? SourceKind { get; set; }

    [JsonPropertyName("sourcePathExists")]
    public bool? SourcePathExists { get; set; }

    [JsonPropertyName("isOutdated")]
    public bool IsOutdated { get; set; }

    [JsonPropertyName("isBuilding")]
    public bool IsBuilding { get; set; }
}
