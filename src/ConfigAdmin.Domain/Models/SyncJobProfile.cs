using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Domain.Models;

public sealed class SyncJobProfile
{
    public Guid Id { get; set; }
    public Guid InfobaseId { get; set; }
    public Guid RemoteNodeId { get; set; }
    public SyncJobStatus Status { get; set; } = SyncJobStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? UploadSessionId { get; set; }
    public long BytesTotal { get; set; }
    public long BytesReceived { get; set; }
    public string? ContentSha256 { get; set; }
    public string? ErrorMessage { get; set; }
    public bool SyncMcpAfterComplete { get; set; }
    public Guid? ConfigurationInstanceId { get; set; }
}
