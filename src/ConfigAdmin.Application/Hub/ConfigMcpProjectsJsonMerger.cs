using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigAdmin.Domain.Hub;

namespace ConfigAdmin.Application.Hub;

/// <summary>
/// Planned-path fallback: config-mcp apply-registry пропускает database, если каталог ещё не создан.
/// Hub материализует запись в projects.json (канонический layout), чтобы привязка работала до первой выгрузки.
/// </summary>
public sealed class ConfigMcpProjectsJsonMerger
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public bool TryMergePlannedRegistry(
        string projectsJsonPath,
        ConfigMcpRegistryFragmentDocument fragment,
        out int databasesMerged,
        out string? error)
    {
        databasesMerged = 0;
        error = null;

        try
        {
            var root = LoadRoot(projectsJsonPath);
            var projects = root["projects"] as JsonArray ?? new JsonArray();
            root["projects"] = projects;

            foreach (var fragmentProject in fragment.RegistryFragment.Projects)
            {
                var projectNode = FindOrCreateProject(projects, fragmentProject);
                var databases = projectNode["databases"] as JsonArray ?? new JsonArray();
                projectNode["databases"] = databases;

                foreach (var database in fragmentProject.Databases)
                {
                    if (string.IsNullOrWhiteSpace(database.SourcePath))
                    {
                        error = "Не задан sourcePath для database.";
                        return false;
                    }

                    UpsertDatabase(databases, fragmentProject.Name, database);
                    databasesMerged++;
                }
            }

            WriteAtomic(projectsJsonPath, root);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static JsonObject LoadRoot(string path)
    {
        if (!File.Exists(path))
            return new JsonObject { ["projects"] = new JsonArray() };

        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidOperationException("Некорректный формат projects.json.");

        node["projects"] ??= new JsonArray();
        return node;
    }

    private static JsonObject FindOrCreateProject(JsonArray projects, ConfigMcpRegistryProjectDto fragmentProject)
    {
        foreach (var item in projects)
        {
            if (item is not JsonObject obj)
                continue;

            var id = obj["id"]?.GetValue<string>();
            if (string.Equals(id, fragmentProject.ProjectId, StringComparison.OrdinalIgnoreCase))
            {
                obj["name"] = fragmentProject.Name;
                obj["active"] = fragmentProject.Active;
                obj["clientId"] = fragmentProject.ClientId;
                return obj;
            }
        }

        var created = new JsonObject
        {
            ["id"] = fragmentProject.ProjectId,
            ["name"] = fragmentProject.Name,
            ["active"] = fragmentProject.Active,
            ["clientId"] = fragmentProject.ClientId,
            ["databases"] = new JsonArray()
        };
        projects.Add(created);
        return created;
    }

    private static void UpsertDatabase(
        JsonArray databases,
        string projectName,
        ConfigMcpRegistryDatabaseDto database)
    {
        var sourceXml = Path.Combine(database.SourcePath, "Configuration.xml");

        for (var i = 0; i < databases.Count; i++)
        {
            if (databases[i] is not JsonObject existing)
                continue;

            var id = existing["id"]?.GetValue<string>();
            if (!string.Equals(id, database.InfobaseId, StringComparison.OrdinalIgnoreCase))
                continue;

            existing["name"] = database.Name;
            existing["type"] = database.Type;
            existing["source_path"] = database.SourcePath;
            existing["source_kind"] = database.SourceKind;
            existing["source_xml"] = sourceXml;
            existing["platform_version"] = database.PlatformVersion;
            existing["db_file"] ??= BuildDbFileName(projectName, database.Name);
            return;
        }

        databases.Add(new JsonObject
        {
            ["id"] = database.InfobaseId,
            ["name"] = database.Name,
            ["type"] = database.Type,
            ["source_path"] = database.SourcePath,
            ["source_kind"] = database.SourceKind,
            ["source_xml"] = sourceXml,
            ["platform_version"] = database.PlatformVersion,
            ["db_file"] = BuildDbFileName(projectName, database.Name)
        });
    }

    internal static string BuildDbFileName(string projectName, string databaseName)
    {
        var combined = $"{SanitizeFileToken(projectName)}_{SanitizeFileToken(databaseName)}";
        if (combined.Length > 120)
            combined = combined[..120];
        return combined.TrimEnd('_') + ".db";
    }

    private static string SanitizeFileToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Replace(' ', '_');
    }

    private static void WriteAtomic(string path, JsonObject root)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(
            temp,
            root.ToJsonString(WriteOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(path))
            File.Replace(temp, path, destinationBackupFileName: null);
        else
            File.Move(temp, path);
    }
}
