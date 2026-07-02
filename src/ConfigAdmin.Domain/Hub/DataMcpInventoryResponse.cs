using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class DataMcpInventoryResponse
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("moduleType")]
    public string ModuleType { get; set; } = string.Empty;

    [JsonPropertyName("moduleVersion")]
    public string ModuleVersion { get; set; } = string.Empty;

    [JsonPropertyName("rootPath")]
    public string RootPath { get; set; } = string.Empty;

    [JsonPropertyName("cliPath")]
    public string? CliPath { get; set; }

    [JsonPropertyName("configPath")]
    public string? ConfigPath { get; set; }

    [JsonPropertyName("statusSupport")]
    public bool StatusSupport { get; set; }

    [JsonPropertyName("syncSupport")]
    public bool SyncSupport { get; set; }

    [JsonPropertyName("cliSupport")]
    public bool CliSupport { get; set; }
}
