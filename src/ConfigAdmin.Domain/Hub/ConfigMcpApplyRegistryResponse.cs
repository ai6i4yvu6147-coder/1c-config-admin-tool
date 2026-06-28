using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class ConfigMcpApplyRegistryResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("appliedAt")]
    public string? AppliedAt { get; set; }

    [JsonPropertyName("changes")]
    public ConfigMcpApplyChangesDto? Changes { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("postApplyActions")]
    public ConfigMcpPostApplyActionsDto? PostApplyActions { get; set; }
}

public sealed class ConfigMcpApplyChangesDto
{
    [JsonPropertyName("created")]
    public int Created { get; set; }

    [JsonPropertyName("updated")]
    public int Updated { get; set; }

    [JsonPropertyName("removed")]
    public int Removed { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }
}

public sealed class ConfigMcpPostApplyActionsDto
{
    [JsonPropertyName("restartRequired")]
    public bool RestartRequired { get; set; }

    [JsonPropertyName("reloadRequired")]
    public bool ReloadRequired { get; set; }

    [JsonPropertyName("followUpOperations")]
    public List<ConfigMcpFollowUpOperationDto> FollowUpOperations { get; set; } = [];
}

public sealed class ConfigMcpFollowUpOperationDto
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public Dictionary<string, string>? Args { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("blocking")]
    public bool Blocking { get; set; }
}
