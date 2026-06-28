namespace ConfigAdmin.Application.RemoteSync;

public sealed class UploadSessionMeta
{
    public Guid SessionId { get; set; }
    public Guid JobId { get; set; }
    public Guid NodeId { get; set; }
    public string FileName { get; set; } = "configuration.zip";
    public long TotalBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int ChunkSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<int> ReceivedChunkIndexes { get; set; } = [];
    public long SessionReceivedBytes { get; set; }
}

public sealed class SyncUploadSessionStore
{
    public const int DefaultChunkSizeBytes = 8_388_608;
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

    private readonly string _syncRoot;

    public SyncUploadSessionStore(string? syncRoot = null)
    {
        _syncRoot = syncRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConfigAdmin",
            "sync");
    }

    public UploadSessionMeta CreateSession(
        Guid sessionId,
        Guid jobId,
        Guid nodeId,
        string fileName,
        long totalBytes,
        string sha256,
        int chunkSizeBytes)
    {
        var now = DateTimeOffset.UtcNow;
        var meta = new UploadSessionMeta
        {
            SessionId = sessionId,
            JobId = jobId,
            NodeId = nodeId,
            FileName = fileName,
            TotalBytes = totalBytes,
            Sha256 = sha256.ToLowerInvariant(),
            ChunkSizeBytes = chunkSizeBytes,
            CreatedAt = now,
            ExpiresAt = now.Add(SessionTtl)
        };

        var dir = GetSessionDir(sessionId);
        Directory.CreateDirectory(Path.Combine(dir, "chunks"));
        SaveMeta(meta);
        return meta;
    }

    public UploadSessionMeta? GetSession(Guid sessionId)
    {
        var path = GetMetaPath(sessionId);
        if (!File.Exists(path))
            return null;

        var meta = LoadMeta(path);
        if (meta.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        return meta;
    }

    public UploadSessionMeta SaveChunk(Guid sessionId, int chunkIndex, ReadOnlySpan<byte> data)
    {
        var meta = GetSession(sessionId)
            ?? throw new InvalidOperationException("Upload session not found or expired.");

        var chunkPath = Path.Combine(GetSessionDir(sessionId), "chunks", $"{chunkIndex}.part");
        File.WriteAllBytes(chunkPath, data.ToArray());

        if (!meta.ReceivedChunkIndexes.Contains(chunkIndex))
        {
            meta.ReceivedChunkIndexes.Add(chunkIndex);
            meta.SessionReceivedBytes += data.Length;
        }

        SaveMeta(meta);
        return meta;
    }

    public string AssembleToFile(Guid sessionId)
    {
        var meta = GetSession(sessionId)
            ?? throw new InvalidOperationException("Upload session not found or expired.");

        var indexes = meta.ReceivedChunkIndexes.OrderBy(i => i).ToList();
        var incomingDir = Path.Combine(_syncRoot, "incoming");
        Directory.CreateDirectory(incomingDir);
        var outputPath = Path.Combine(incomingDir, $"{sessionId:N}.zip");

        using var output = File.Create(outputPath);
        foreach (var index in indexes)
        {
            var chunkPath = Path.Combine(GetSessionDir(sessionId), "chunks", $"{index}.part");
            if (!File.Exists(chunkPath))
                throw new InvalidOperationException($"Missing chunk {index}.");

            using var input = File.OpenRead(chunkPath);
            input.CopyTo(output);
        }

        return outputPath;
    }

    public void DeleteSession(Guid sessionId)
    {
        var dir = GetSessionDir(sessionId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        var assembled = Path.Combine(_syncRoot, "incoming", $"{sessionId:N}.zip");
        if (File.Exists(assembled))
            File.Delete(assembled);
    }

    public void CleanupExpired()
    {
        var sessionsDir = Path.Combine(_syncRoot, "sessions");
        if (!Directory.Exists(sessionsDir))
            return;

        foreach (var dir in Directory.EnumerateDirectories(sessionsDir))
        {
            var metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath))
                continue;

            var meta = LoadMeta(metaPath);
            if (meta.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                if (Guid.TryParse(Path.GetFileName(dir), out var sessionId))
                    DeleteSession(sessionId);
            }
        }
    }

    private string GetSessionDir(Guid sessionId) =>
        Path.Combine(_syncRoot, "sessions", sessionId.ToString("N"));

    private string GetMetaPath(Guid sessionId) =>
        Path.Combine(GetSessionDir(sessionId), "meta.json");

    private void SaveMeta(UploadSessionMeta meta)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(GetMetaPath(meta.SessionId), json);
    }

    private static UploadSessionMeta LoadMeta(string path)
    {
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<UploadSessionMeta>(json)
            ?? throw new InvalidOperationException("Invalid session meta.");
    }
}
