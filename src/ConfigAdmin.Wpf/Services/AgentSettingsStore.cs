using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ConfigAdmin.Wpf.Services;

public sealed class AgentSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConfigAdmin",
            "agent",
            "settings.json");

    public AgentSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AgentSettings();

            var json = File.ReadAllText(SettingsPath);
            var stored = JsonSerializer.Deserialize<StoredAgentSettings>(json, JsonOptions);
            if (stored is null)
                return new AgentSettings();

            return new AgentSettings
            {
                HubUrl = stored.HubUrl ?? string.Empty,
                NodeId = stored.NodeId ?? string.Empty,
                AccessToken = DecryptToken(stored.EncryptedAccessToken)
            };
        }
        catch
        {
            return new AgentSettings();
        }
    }

    public void Save(AgentSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        var stored = new StoredAgentSettings
        {
            HubUrl = settings.HubUrl,
            NodeId = settings.NodeId,
            EncryptedAccessToken = string.IsNullOrWhiteSpace(settings.AccessToken)
                ? null
                : EncryptToken(settings.AccessToken)
        };

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(stored, JsonOptions));
    }

    public void Clear()
    {
        if (File.Exists(SettingsPath))
            File.Delete(SettingsPath);
    }

    private static string EncryptToken(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string DecryptToken(string? protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
            return string.Empty;

        var protectedBytes = Convert.FromBase64String(protectedBase64);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    private sealed class StoredAgentSettings
    {
        public string? HubUrl { get; set; }
        public string? NodeId { get; set; }
        public string? EncryptedAccessToken { get; set; }
    }
}

public sealed class AgentSettings
{
    public string HubUrl { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}
