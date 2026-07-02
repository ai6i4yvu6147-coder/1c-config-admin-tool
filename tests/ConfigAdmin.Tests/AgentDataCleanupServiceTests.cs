using ConfigAdmin.Application.RemoteSync;
using Xunit;

namespace ConfigAdmin.Tests;

public class AgentDataCleanupServiceTests
{
    [Fact]
    public void CleanupCompletedJob_DeletesEntireWorkDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"agent-cleanup-{Guid.NewGuid():N}");
        var jobId = Guid.NewGuid();
        var workRoot = Path.Combine(tempRoot, "work", jobId.ToString("N"));
        var configDir = Path.Combine(workRoot, "Основная конфигурация");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "module.xml"), "<xml/>");
        File.WriteAllText(Path.Combine(workRoot, "configuration.zip"), "zip");

        var service = new AgentDataCleanupService();
        service.CleanupCompletedJob(jobId, agentWorkRoot: Path.Combine(tempRoot, "work"));

        Assert.False(Directory.Exists(workRoot));
    }

    [Fact]
    public void CleanupCompletedJob_DeletesExportPathOutsideWorkRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"agent-cleanup-{Guid.NewGuid():N}");
        var jobId = Guid.NewGuid();
        var workRoot = Path.Combine(tempRoot, "work", jobId.ToString("N"));
        var exportPath = Path.Combine(tempRoot, "custom-export");
        Directory.CreateDirectory(workRoot);
        File.WriteAllText(Path.Combine(workRoot, "configuration.zip"), "zip");
        Directory.CreateDirectory(exportPath);
        File.WriteAllText(Path.Combine(exportPath, "module.xml"), "<xml/>");

        var service = new AgentDataCleanupService();
        service.CleanupCompletedJob(jobId, agentWorkRoot: Path.Combine(tempRoot, "work"), exportPath);

        Assert.False(Directory.Exists(workRoot));
        Assert.False(Directory.Exists(exportPath));
    }

    [Fact]
    public void IsUnderDirectory_DetectsNestedPath()
    {
        var parent = Path.Combine(Path.GetTempPath(), "parent");
        var child = Path.Combine(parent, "child");

        Assert.True(AgentDataCleanupService.IsUnderDirectory(child, parent));
        Assert.False(AgentDataCleanupService.IsUnderDirectory(parent, child));
    }
}
