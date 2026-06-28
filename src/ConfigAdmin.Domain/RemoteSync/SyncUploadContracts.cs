namespace ConfigAdmin.Domain.RemoteSync;

public sealed class CreateUploadSessionRequest
{
    public Guid JobId { get; init; }
    public string FileName { get; init; } = "configuration.zip";
    public long TotalBytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
}

public sealed class CreateUploadSessionResponse
{
    public Guid SessionId { get; init; }
    public int ChunkSizeBytes { get; init; }
    public int[] AcceptedChunks { get; init; } = [];
}

public sealed class ChunkUploadResponse
{
    public int ChunkIndex { get; init; }
    public long ReceivedBytes { get; init; }
    public long SessionReceivedBytes { get; init; }
}

public sealed class UploadSessionStateResponse
{
    public Guid SessionId { get; init; }
    public long TotalBytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
    public int[] ReceivedChunkIndexes { get; init; } = [];
    public long SessionReceivedBytes { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed class CompleteUploadResponse
{
    public bool Success { get; init; }
    public string AppliedPath { get; init; } = string.Empty;
    public string JobStatus { get; init; } = string.Empty;
}
