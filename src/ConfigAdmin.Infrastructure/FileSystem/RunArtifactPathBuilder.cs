using ConfigAdmin.Domain.Services;

namespace ConfigAdmin.Infrastructure.FileSystem;

public sealed class RunArtifactPathBuilder : IRunArtifactPathBuilder
{
    public string GetRunDirectory(string clientName, string baseName, Guid runId) =>
        Path.Combine(AppPaths.RunsDirectory, Sanitize(clientName), Sanitize(baseName), runId.ToString("N"));

    public string GetMetaJsonPath(string clientName, string baseName, Guid runId) =>
        Path.Combine(GetRunDirectory(clientName, baseName, runId), "export-meta.json");

    public string GetOutLogPath(string clientName, string baseName, Guid runId, string stepName) =>
        Path.Combine(GetRunDirectory(clientName, baseName, runId), $"{SanitizeStepName(stepName)}.out.log");

    public string GetDumpResultPath(string clientName, string baseName, Guid runId, string stepName) =>
        Path.Combine(GetRunDirectory(clientName, baseName, runId), $"{SanitizeStepName(stepName)}.dumpresult");

    private static string SanitizeStepName(string stepName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = stepName
            .Replace(':', '-')
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(chars).Trim();
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}
