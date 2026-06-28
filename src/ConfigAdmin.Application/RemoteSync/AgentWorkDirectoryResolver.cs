using ConfigAdmin.Domain.Services;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class AgentWorkDirectoryResolver
{
    private readonly IExportPathBuilder _exportPathBuilder;

    public AgentWorkDirectoryResolver(IExportPathBuilder exportPathBuilder)
    {
        _exportPathBuilder = exportPathBuilder;
    }

    public string GetConfigurationWorkPath(Guid jobId, string? remoteExportPathOverride, string? agentWorkRoot)
    {
        if (!string.IsNullOrWhiteSpace(remoteExportPathOverride))
            return remoteExportPathOverride.Trim();

        var root = string.IsNullOrWhiteSpace(agentWorkRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ConfigAdmin",
                "agent",
                "work")
            : agentWorkRoot.Trim();

        return Path.Combine(root, jobId.ToString("N"), _exportPathBuilder.ConfigurationFolderName);
    }

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
