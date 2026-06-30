namespace ConfigAdmin.Wpf.Services;

public enum UiActivityLevel
{
    Info,
    Warning,
    Error
}

public sealed class UiActivityEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public UiActivityLevel Level { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Detail { get; init; }

    public string LevelLabel => Level switch
    {
        UiActivityLevel.Warning => "WARN",
        UiActivityLevel.Error => "ERROR",
        _ => "INFO"
    };

    public string DisplayText => string.IsNullOrWhiteSpace(Detail)
        ? Message
        : $"{Message}{Environment.NewLine}{Detail}";
}
