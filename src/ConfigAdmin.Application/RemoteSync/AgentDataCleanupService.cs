using ConfigAdmin.Infrastructure;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class AgentDataCleanupService
{
    public static string AgentRoot =>
        Path.Combine(AppPaths.AppDataDirectory, "agent");

    public static string AgentWorkRoot =>
        Path.Combine(AgentRoot, "work");

    public static string AgentResumeRoot =>
        Path.Combine(AgentRoot, "resume");

    public AgentCleanupResult CleanupJobDirectories(Guid? excludeJobId, bool includeResume = true)
    {
        var deleted = new List<string>();
        var skipped = new List<string>();

        if (Directory.Exists(AgentWorkRoot))
        {
            foreach (var dir in Directory.GetDirectories(AgentWorkRoot))
            {
                var folderName = Path.GetFileName(dir);
                if (excludeJobId is Guid jobId &&
                    string.Equals(folderName, jobId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                {
                    skipped.Add(dir);
                    continue;
                }

                Directory.Delete(dir, recursive: true);
                deleted.Add(dir);
            }
        }

        if (includeResume && Directory.Exists(AgentResumeRoot))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(AgentResumeRoot))
            {
                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);

                deleted.Add(entry);
            }
        }

        return new AgentCleanupResult(deleted, skipped);
    }

    public AgentCleanupResult CleanupAllAgentData()
    {
        var deleted = new List<string>();

        if (Directory.Exists(AgentRoot))
        {
            Directory.Delete(AgentRoot, recursive: true);
            deleted.Add(AgentRoot);
        }

        if (Directory.Exists(AppPaths.LogsDirectory))
        {
            Directory.Delete(AppPaths.LogsDirectory, recursive: true);
            deleted.Add(AppPaths.LogsDirectory);
        }

        if (File.Exists(AppPaths.DatabasePath))
        {
            File.Delete(AppPaths.DatabasePath);
            deleted.Add(AppPaths.DatabasePath);
        }

        return new AgentCleanupResult(deleted, []);
    }
}

public sealed record AgentCleanupResult(
    IReadOnlyList<string> Deleted,
    IReadOnlyList<string> Skipped);
