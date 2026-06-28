using ConfigAdmin.Domain.Enums;

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
    public string? ProgressMessage { get; init; }
}

public sealed class PollJobsResponse
{
    public SyncJobDto? Job { get; init; }
}

public sealed class SyncJobDto
{
    public Guid JobId { get; init; }
    public Guid InfobaseId { get; init; }
    public Guid NodeId { get; init; }
    public string Action { get; init; } = "exportAndUpload";
    public string RemoteExportPath { get; init; } = string.Empty;
    public string Packaging { get; init; } = "zip";
    public int MaxChunkSizeBytes { get; init; } = 8_388_608;
    public ExportJobSpec Export { get; init; } = new();
    public byte[]? EncryptedConnectionPassword { get; init; }
}

public sealed class ExportJobSpec
{
    public string PlatformPath { get; init; } = string.Empty;
    public ConnectionType ConnectionType { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string? Username { get; init; }
    public bool ExportConfiguration { get; init; } = true;
    public ExportFormat ExportFormat { get; init; } = ExportFormat.Hierarchical;
}

public sealed class SyncAgentErrorResponse
{
    public string Error { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public sealed class FailJobRequest
{
    public string ErrorMessage { get; init; } = string.Empty;
}
