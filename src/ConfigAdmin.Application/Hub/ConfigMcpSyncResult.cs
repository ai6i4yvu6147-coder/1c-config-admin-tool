namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpSyncResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<ConfigMcpFollowUpHint> FollowUpHints { get; init; } = [];
}

public sealed class ConfigMcpFollowUpHint
{
    public string Command { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Blocking { get; init; }
    public string DisplayText { get; init; } = string.Empty;
}
