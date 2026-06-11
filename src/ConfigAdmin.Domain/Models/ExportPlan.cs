using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Domain.Models;

public sealed class ExportPlan
{
    public bool ExportConfiguration { get; set; } = true;
    public bool ExportAllExtensions { get; set; } = true;
    public List<string> SelectedExtensions { get; set; } = [];
    public ExportFormat Format { get; set; } = ExportFormat.Hierarchical;

    public static ExportPlan FromProfile(InfobaseProfile profile) => new()
    {
        ExportConfiguration = profile.ExportConfiguration,
        ExportAllExtensions = profile.ExportAllExtensions,
        SelectedExtensions = [.. profile.SelectedExtensions],
        Format = profile.ExportFormat
    };

    public bool HasWork =>
        ExportConfiguration ||
        ExportAllExtensions ||
        SelectedExtensions.Count > 0;
}
