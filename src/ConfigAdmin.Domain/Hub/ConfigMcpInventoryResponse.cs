using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class ConfigMcpInventoryResponse
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("moduleVersion")]
    public string? ModuleVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public Dictionary<string, bool>? Capabilities { get; set; }
}
