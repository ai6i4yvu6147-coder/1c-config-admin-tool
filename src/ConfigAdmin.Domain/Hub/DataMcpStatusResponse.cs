using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class DataMcpStatusResponse
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

    [JsonPropertyName("details")]
    public DataMcpStatusDetailsDto? Details { get; set; }

    [JsonPropertyName("connections")]
    public List<DataMcpStatusConnectionDto> Connections { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];
}

public sealed class DataMcpStatusDetailsDto
{
    [JsonPropertyName("configReadable")]
    public bool ConfigReadable { get; set; }

    [JsonPropertyName("runtimeExists")]
    public bool RuntimeExists { get; set; }

    [JsonPropertyName("cliExists")]
    public bool CliExists { get; set; }

    [JsonPropertyName("credentialsExists")]
    public bool CredentialsExists { get; set; }

    [JsonPropertyName("credentialsResolvable")]
    public bool CredentialsResolvable { get; set; }

    [JsonPropertyName("bucket")]
    public string? Bucket { get; set; }
}

public sealed class DataMcpStatusConnectionDto
{
    [JsonPropertyName("databaseId")]
    public string DatabaseId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
