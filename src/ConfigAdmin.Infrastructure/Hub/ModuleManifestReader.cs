using System.Text.Json;
using ConfigAdmin.Domain.Hub;

namespace ConfigAdmin.Infrastructure.Hub;

public sealed class ModuleManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ModuleManifestDto Read(string rootPath)
    {
        var manifestPath = Path.Combine(rootPath, "module.manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"module.manifest.json не найден в {rootPath}");

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<ModuleManifestDto>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Не удалось разобрать {manifestPath}");
    }

    public string ResolveCliPath(string rootPath)
    {
        var manifest = Read(rootPath);
        var cliRelative = manifest.Runtime?.CliExe;
        if (string.IsNullOrWhiteSpace(cliRelative))
            throw new InvalidOperationException("В manifest отсутствует runtime.cliExe.");

        var cliPath = Path.Combine(rootPath, cliRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(cliPath))
            throw new FileNotFoundException($"CLI не найден: {cliPath}");

        return cliPath;
    }
}
