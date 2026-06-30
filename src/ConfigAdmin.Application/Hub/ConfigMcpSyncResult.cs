namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpSyncResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<ConfigMcpFollowUpHint> FollowUpHints { get; init; } = [];
    public int ChangesCreated { get; init; }
    public int ChangesUpdated { get; init; }
    public int ChangesSkipped { get; init; }
    public string? RegistryPath { get; init; }
    public int IndexRebuildsSucceeded { get; init; }
    public int IndexRebuildsFailed { get; init; }

    public bool HasRegistryChanges => ChangesCreated + ChangesUpdated > 0;
}

public sealed class ConfigMcpFollowUpHint
{
    public string Command { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Blocking { get; init; }
    public string DisplayText { get; init; } = string.Empty;
}
