namespace ConfigAdmin.Domain.Services;

public interface IExportPathBuilder
{
    string ConfigurationFolderName { get; }
    string GetBaseExportDirectory(string exportRoot, string clientName, string baseName);
    string GetConfigurationPath(string exportRoot, string clientName, string baseName);
    string GetExtensionPath(string exportRoot, string clientName, string baseName, string extensionName);
    string CreateTempExportDirectory(string exportRoot, string clientName, string baseName);
}
