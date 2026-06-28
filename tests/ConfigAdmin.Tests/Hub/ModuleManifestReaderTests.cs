using System.Text.Json;
using ConfigAdmin.Infrastructure.Hub;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class ModuleManifestReaderTests
{
    [Fact]
    public void ResolveCliPath_CombinesRootAndManifestRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "configadmin-manifest-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Tools"));

        var manifest = """
            {
              "schemaVersion": 1,
              "moduleId": "1c-config-mcp",
              "moduleType": "config-mcp",
              "runtime": { "cliExe": "Tools/1c-config-cli.exe" }
            }
            """;
        File.WriteAllText(Path.Combine(root, "module.manifest.json"), manifest);
        File.WriteAllText(Path.Combine(root, "Tools", "1c-config-cli.exe"), "stub");

        var reader = new ModuleManifestReader();
        var cliPath = reader.ResolveCliPath(root);

        Assert.Equal(Path.Combine(root, "Tools", "1c-config-cli.exe"), cliPath);
    }

    [Fact]
    public void Read_DeserializesModuleId()
    {
        var root = Path.Combine(Path.GetTempPath(), "configadmin-manifest-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var manifest = """
            {
              "schemaVersion": 1,
              "moduleId": "1c-config-mcp",
              "moduleType": "config-mcp",
              "runtime": { "cliExe": "Tools/1c-config-cli.exe" }
            }
            """;
        File.WriteAllText(Path.Combine(root, "module.manifest.json"), manifest);

        var reader = new ModuleManifestReader();
        var dto = reader.Read(root);

        Assert.Equal("1c-config-mcp", dto.ModuleId);
        Assert.Equal("config-mcp", dto.ModuleType);
    }
}
