using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class ConfigurationInstanceRepository : IConfigurationInstanceRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ConfigurationInstanceRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ConfigurationInstance>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<InstanceRow>(
            new CommandDefinition(
                """
                SELECT * FROM configuration_instances
                ORDER BY infobase_id, sort_order, display_name
                """,
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ConfigurationInstance>> GetByInfobaseIdAsync(
        Guid infobaseId,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<InstanceRow>(
            new CommandDefinition(
                """
                SELECT * FROM configuration_instances
                WHERE infobase_id = @InfobaseId
                ORDER BY sort_order, display_name
                """,
                new { InfobaseId = infobaseId.ToString() },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<ConfigurationInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<InstanceRow>(
            new CommandDefinition(
                "SELECT * FROM configuration_instances WHERE id = @Id",
                new { Id = id.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(ConfigurationInstance instance, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO configuration_instances (
              id, infobase_id, template_id, kind, display_name, designer_name,
              export_enabled, sort_order, config_mcp_project_id, config_mcp_database_id
            ) VALUES (
              @Id, @InfobaseId, @TemplateId, @Kind, @DisplayName, @DesignerName,
              @ExportEnabled, @SortOrder, @ConfigMcpProjectId, @ConfigMcpDatabaseId
            )
            ON CONFLICT(id) DO UPDATE SET
              template_id = excluded.template_id,
              kind = excluded.kind,
              display_name = excluded.display_name,
              designer_name = excluded.designer_name,
              export_enabled = excluded.export_enabled,
              sort_order = excluded.sort_order,
              config_mcp_project_id = excluded.config_mcp_project_id,
              config_mcp_database_id = excluded.config_mcp_database_id
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = instance.Id.ToString(),
            InfobaseId = instance.InfobaseId.ToString(),
            TemplateId = instance.TemplateId?.ToString(),
            Kind = (int)instance.Kind,
            instance.DisplayName,
            instance.DesignerName,
            ExportEnabled = instance.ExportEnabled ? 1 : 0,
            instance.SortOrder,
            ConfigMcpProjectId = instance.ConfigMcpProjectId?.ToString(),
            ConfigMcpDatabaseId = instance.ConfigMcpDatabaseId?.ToString()
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM configuration_instances
            WHERE id = @Id
              AND kind != @BaseKind
            """,
            new { Id = id.ToString(), BaseKind = (int)ConfigurationKind.Base },
            cancellationToken: ct));
    }

    public async Task DeleteByInfobaseIdAsync(Guid infobaseId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM configuration_instances WHERE infobase_id = @InfobaseId",
            new { InfobaseId = infobaseId.ToString() },
            cancellationToken: ct));
    }

    private static ConfigurationInstance Map(InstanceRow row) => new()
    {
        Id = Guid.Parse(row.id),
        InfobaseId = Guid.Parse(row.infobase_id),
        TemplateId = string.IsNullOrWhiteSpace(row.template_id) ? null : Guid.Parse(row.template_id),
        Kind = (ConfigurationKind)row.kind,
        DisplayName = row.display_name,
        DesignerName = row.designer_name,
        ExportEnabled = row.export_enabled != 0,
        SortOrder = row.sort_order,
        ConfigMcpProjectId = ParseNullableGuid(row.config_mcp_project_id),
        ConfigMcpDatabaseId = ParseNullableGuid(row.config_mcp_database_id)
    };

    private static Guid? ParseNullableGuid(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);

    private sealed class InstanceRow
    {
        public string id { get; set; } = string.Empty;
        public string infobase_id { get; set; } = string.Empty;
        public string? template_id { get; set; }
        public int kind { get; set; }
        public string display_name { get; set; } = string.Empty;
        public string? designer_name { get; set; }
        public int export_enabled { get; set; }
        public int sort_order { get; set; }
        public string? config_mcp_project_id { get; set; }
        public string? config_mcp_database_id { get; set; }
    }
}
