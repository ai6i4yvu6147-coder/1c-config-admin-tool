using ConfigAdmin.Domain.Services;

namespace ConfigAdmin.Infrastructure.FileSystem;

public sealed class ExportPathBuilder : IExportPathBuilder
{
    public string ConfigurationFolderName => "Основная конфигурация";

    public string GetBaseExportDirectory(string exportRoot, string clientName, string baseName) =>
        Path.Combine(exportRoot, Sanitize(clientName), Sanitize(baseName));

    public string GetConfigurationPath(string exportRoot, string clientName, string baseName) =>
        Path.Combine(GetBaseExportDirectory(exportRoot, clientName, baseName), ConfigurationFolderName);

    public string GetExtensionPath(string exportRoot, string clientName, string baseName, string extensionName) =>
        Path.Combine(GetBaseExportDirectory(exportRoot, clientName, baseName), Sanitize(extensionName));

    public string CreateTempExportDirectory(string exportRoot, string clientName, string baseName)
    {
        var baseDir = GetBaseExportDirectory(exportRoot, clientName, baseName);
        Directory.CreateDirectory(baseDir);
        var tempPath = Path.Combine(baseDir, $"temp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}
