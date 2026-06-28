using System.Text.Json;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class AgentResumeState
{
    public Guid SessionId { get; set; }
    public Guid JobId { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string ZipPath { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public int ChunkSizeBytes { get; set; }
    public List<int> SentChunkIndexes { get; set; } = [];
}

public sealed class AgentResumeStore
{
    private readonly string _resumeRoot;

    public AgentResumeStore(string? resumeRoot = null)
    {
        _resumeRoot = resumeRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConfigAdmin",
            "agent",
            "resume");
    }

    public void Save(AgentResumeState state)
    {
        Directory.CreateDirectory(_resumeRoot);
        var path = GetPath(state.SessionId);
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    public AgentResumeState? Load(Guid sessionId)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path))
            return null;

        return JsonSerializer.Deserialize<AgentResumeState>(File.ReadAllText(path));
    }

    public AgentResumeState? LoadLatestForJob(Guid jobId)
    {
        if (!Directory.Exists(_resumeRoot))
            return null;

        AgentResumeState? latest = null;
        foreach (var file in Directory.EnumerateFiles(_resumeRoot, "*.json"))
        {
            var state = JsonSerializer.Deserialize<AgentResumeState>(File.ReadAllText(file));
            if (state?.JobId == jobId)
                latest = state;
        }

        return latest;
    }

    public void Delete(Guid sessionId)
    {
        var path = GetPath(sessionId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetPath(Guid sessionId) =>
        Path.Combine(_resumeRoot, $"{sessionId:N}.json");
}
