using ConfigAdmin.Application.Hub;
using ConfigAdmin.Domain.Hub;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class ConfigMcpProjectsJsonMergerTests
{
    [Fact]
    public void TryMergePlannedRegistry_CreatesProjectAndDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"projects-{Guid.NewGuid():N}.json");
        var merger = new ConfigMcpProjectsJsonMerger();
        var projectId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();
        var sourcePath = @"C:\Exports\Client\Base\Ext1";

        var fragment = new ConfigMcpRegistryFragmentDocument
        {
            RegistryFragment = new ConfigMcpRegistryFragment
            {
                Projects =
                [
                    new ConfigMcpRegistryProjectDto
                    {
                        ProjectId = projectId.ToString(),
                        ClientId = Guid.NewGuid().ToString(),
                        Name = "Трансгаз",
                        Active = true,
                        Databases =
                        [
                            new ConfigMcpRegistryDatabaseDto
                            {
                                InfobaseId = databaseId.ToString(),
                                Name = "База / Расширение",
                                Type = "extension",
                                SourcePath = sourcePath,
                                SourceKind = "directory",
                                PlatformVersion = "8.3.27.1688"
                            }
                        ]
                    }
                ]
            }
        };

        try
        {
            Assert.True(merger.TryMergePlannedRegistry(path, fragment, out var merged, out var error), error);
            Assert.Equal(1, merged);

            var json = File.ReadAllText(path);
            Assert.Contains(projectId.ToString(), json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(databaseId.ToString(), json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("source_path", json, StringComparison.Ordinal);
            Assert.Contains("Ext1", json, StringComparison.Ordinal);
            Assert.Contains("Трансгаз", json, StringComparison.Ordinal);
            Assert.DoesNotContain(@"\u0422", json, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void BuildDbFileName_SanitizesTokens()
    {
        var name = ConfigMcpProjectsJsonMerger.BuildDbFileName("Трансгаз", "База / ТД");
        Assert.EndsWith(".db", name);
        Assert.DoesNotContain("/", name);
    }
}
