using System.IO;
using System.Text.Json;

namespace ConfigAdmin.Wpf.Services;

public enum AppRunMode
{
    Hub,
    Agent
}

public sealed class LocalAppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AppRunMode? Mode { get; set; }
    public string HubListenUrl { get; set; } = "http://0.0.0.0:18443";

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConfigAdmin",
            "appsettings.local.json");

    public static LocalAppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new LocalAppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LocalAppSettings>(json, JsonOptions) ?? new LocalAppSettings();
        }
        catch
        {
            return new LocalAppSettings();
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
