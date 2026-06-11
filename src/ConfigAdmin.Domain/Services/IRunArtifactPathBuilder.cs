namespace ConfigAdmin.Domain.Services;

public interface IRunArtifactPathBuilder
{
    string GetRunDirectory(string clientName, string baseName, Guid runId);
    string GetMetaJsonPath(string clientName, string baseName, Guid runId);
    string GetOutLogPath(string clientName, string baseName, Guid runId, string stepName);
    string GetDumpResultPath(string clientName, string baseName, Guid runId, string stepName);
}
