using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class ModuleManifestDto
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("moduleType")]
    public string ModuleType { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public ModuleRuntimeDto? Runtime { get; set; }
}

public sealed class ModuleRuntimeDto
{
    [JsonPropertyName("cliExe")]
    public string CliExe { get; set; } = string.Empty;
}
