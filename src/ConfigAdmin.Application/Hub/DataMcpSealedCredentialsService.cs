using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Infrastructure.Security;

namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpSealedCredentialsService
{
    private static readonly JsonSerializerOptions DocumentJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public bool CredentialsFileExists(string portableRoot, string hubSealedSecretsPath) =>
        File.Exists(ResolveCredentialsFilePath(portableRoot, hubSealedSecretsPath));

    public string? GetExistingCredentialsRelativePath(string portableRoot, string hubSealedSecretsPath) =>
        TryResolveExistingCredentialsRelativePath(portableRoot, hubSealedSecretsPath);

    public async Task<DmcpSealedCredentialsPlaintext> ReadAsync(
        string portableRoot,
        string sealedSecretsPath,
        string dmcpPassword,
        CancellationToken ct = default)
    {
        var targetPath = ResolveCredentialsFilePath(portableRoot, sealedSecretsPath);
        if (!File.Exists(targetPath))
            throw new FileNotFoundException("Sealed-файл не найден.", targetPath);

        var json = await File.ReadAllTextAsync(targetPath, ct);
        var document = JsonSerializer.Deserialize<DmcpSealedCredentialsDocument>(json, DocumentJsonOptions)
                       ?? throw new InvalidOperationException("Некорректный sealed-файл.");

        return DmcpSealedCredentialsCodec.Unseal(document, dmcpPassword);
    }

    public static string ResolveCredentialsFilePath(string portableRoot, string hubSealedSecretsPath)
    {
        var relative = TryResolveExistingCredentialsRelativePath(portableRoot, hubSealedSecretsPath)
                       ?? NormalizeRelativePath(hubSealedSecretsPath);
        return Path.GetFullPath(Path.Combine(portableRoot.Trim(), relative));
    }

    public static string ResolveSealedFilePath(string portableRoot, string sealedSecretsPath) =>
        ResolveCredentialsFilePath(portableRoot, sealedSecretsPath);

    private static string? TryResolveExistingCredentialsRelativePath(
        string portableRoot,
        string hubSealedSecretsPath)
    {
        var root = portableRoot.Trim();
        var fromConfig = TryReadCredentialsFileFromConfig(root);
        if (fromConfig is not null && File.Exists(Path.Combine(root, fromConfig)))
            return fromConfig;

        var hubPath = NormalizeRelativePath(hubSealedSecretsPath);
        if (File.Exists(Path.Combine(root, hubPath)))
            return hubPath;

        return fromConfig ?? (File.Exists(Path.Combine(root, hubPath)) ? hubPath : null);
    }

    private static string? TryReadCredentialsFileFromConfig(string portableRoot)
    {
        var configPath = Path.Combine(portableRoot, "config.local.json");
        if (!File.Exists(configPath))
            return null;

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject;
            var value = node?["credentials_file"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeRelativePath(string sealedSecretsPath) =>
        string.IsNullOrWhiteSpace(sealedSecretsPath)
            ? "credentials.sealed.json"
            : sealedSecretsPath.Trim();
}
