using System.Collections.Concurrent;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncJobProgressStore
{
    private readonly ConcurrentDictionary<Guid, JobProgressEntry> _entries = new();

    public void Set(Guid jobId, string agentStatus, string? progressMessage)
    {
        if (string.IsNullOrWhiteSpace(progressMessage))
            return;

        _entries[jobId] = new JobProgressEntry(
            progressMessage.Trim(),
            agentStatus,
            DateTimeOffset.UtcNow);
    }

    public JobProgressEntry? Get(Guid jobId) =>
        _entries.TryGetValue(jobId, out var entry) ? entry : null;

    public void Clear(Guid jobId) => _entries.TryRemove(jobId, out _);

    public sealed record JobProgressEntry(
        string Message,
        string AgentStatus,
        DateTimeOffset UpdatedAt);
}
