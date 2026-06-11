using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class InfobaseListItem
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ConnectionType ConnectionType { get; init; }
    public ExportStatus LastExportStatus { get; init; }
    public DateTimeOffset? LastExportAt { get; init; }
    public string ExportSettingsSummary { get; init; } = string.Empty;

    public string DisplayName => $"{ClientName} / {Name}";
    public string StatusText => $"{LastExportStatus} ({LastExportAt?.ToLocalTime():g})";

    public static string BuildExportSummary(
        bool exportConfiguration,
        bool exportAllExtensions,
        IReadOnlyList<string> selectedExtensions)
    {
        var parts = new List<string>();
        if (exportConfiguration)
            parts.Add("конфигурация");
        if (exportAllExtensions)
            parts.Add("все расширения");
        else if (selectedExtensions.Count > 0)
            parts.Add($"расш.: {string.Join(", ", selectedExtensions)}");

        return parts.Count > 0 ? string.Join("; ", parts) : "не задано";
    }
}
