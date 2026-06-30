using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Services;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class AgentWorkDirectoryResolver
{
    private readonly IExportPathBuilder _exportPathBuilder;

    public AgentWorkDirectoryResolver(IExportPathBuilder exportPathBuilder)
    {
        _exportPathBuilder = exportPathBuilder;
    }

    public string GetInstanceWorkPath(
        Guid jobId,
        ConfigurationKind kind,
        string? designerName,
        string? remoteExportPathOverride,
        string? agentWorkRoot)
    {
        if (!string.IsNullOrWhiteSpace(remoteExportPathOverride))
            return remoteExportPathOverride.Trim();

        var root = GetJobWorkRoot(jobId, agentWorkRoot);
        if (kind == ConfigurationKind.Base)
            return Path.Combine(root, _exportPathBuilder.ConfigurationFolderName);

        return Path.Combine(root, designerName ?? "extension");
    }

    public string GetConfigurationWorkPath(Guid jobId, string? remoteExportPathOverride, string? agentWorkRoot) =>
        GetInstanceWorkPath(jobId, ConfigurationKind.Base, null, remoteExportPathOverride, agentWorkRoot);

    public string GetJobWorkRoot(Guid jobId, string? agentWorkRoot)
    {
        if (!string.IsNullOrWhiteSpace(agentWorkRoot))
            return Path.Combine(agentWorkRoot.Trim(), jobId.ToString("N"));

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConfigAdmin",
            "agent",
            "work",
            jobId.ToString("N"));
    }
}
