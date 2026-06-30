using System.Text.Json.Serialization;

namespace ConfigAdmin.Domain.Hub;

public sealed class ConfigMcpRebuildIndexResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("operation")]
    public string? Operation { get; set; }

    [JsonPropertyName("targetId")]
    public string? TargetId { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("dbFile")]
    public string? DbFile { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];
}
