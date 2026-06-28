namespace ConfigAdmin.Domain.RemoteSync;

public sealed class RegisterAgentRequest
{
    public Guid NodeId { get; init; }
    public string PairingPassword { get; init; } = string.Empty;
    public string AgentVersion { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
}

public sealed class RegisterAgentResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public int PollIntervalMs { get; init; }
    public string HubVersion { get; init; } = string.Empty;
}

public sealed class HeartbeatRequest
{
    public Guid NodeId { get; init; }
    public string Status { get; init; } = "idle";
    public Guid? CurrentJobId { get; init; }
}

public sealed class PollJobsResponse
{
    public SyncJobDto? Job { get; init; }
}

public sealed class SyncJobDto
{
    public Guid JobId { get; init; }
    public Guid InfobaseId { get; init; }
    public string RemoteSourcePath { get; init; } = string.Empty;
    public string Packaging { get; init; } = "zip";
    public int MaxChunkSizeBytes { get; init; }
}

public sealed class SyncAgentErrorResponse
{
    public string Error { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}
