using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class ConfigurationExportRepository : IConfigurationExportRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ConfigurationExportRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ConfigurationExport?> GetCurrentByInstanceIdAsync(
        Guid instanceId,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ExportRow>(
            new CommandDefinition(
                """
                SELECT * FROM configuration_exports
                WHERE instance_id = @InstanceId AND is_current = 1
                LIMIT 1
                """,
                new { InstanceId = instanceId.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<ConfigurationExport>> GetByInstanceIdsAsync(
        IEnumerable<Guid> instanceIds,
        CancellationToken ct = default)
    {
        var ids = instanceIds.Select(id => id.ToString()).ToList();
        if (ids.Count == 0)
            return [];

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ExportRow>(
            new CommandDefinition(
                """
                SELECT * FROM configuration_exports
                WHERE instance_id IN @Ids AND is_current = 1
                """,
                new { Ids = ids },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task SaveAsync(ConfigurationExport export, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO configuration_exports (id, instance_id, exported_at, is_current)
            VALUES (@Id, @InstanceId, @ExportedAt, @IsCurrent)
            ON CONFLICT(id) DO UPDATE SET
              exported_at = excluded.exported_at,
              is_current = excluded.is_current
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = export.Id.ToString(),
            InstanceId = export.InstanceId.ToString(),
            ExportedAt = export.ExportedAt?.ToString("O"),
            IsCurrent = export.IsCurrent ? 1 : 0
        }, cancellationToken: ct));
    }

    public async Task MarkAllNotCurrentForInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE configuration_exports SET is_current = 0 WHERE instance_id = @InstanceId",
            new { InstanceId = instanceId.ToString() },
            cancellationToken: ct));
    }

    private static ConfigurationExport Map(ExportRow row) => new()
    {
        Id = Guid.Parse(row.id),
        InstanceId = Guid.Parse(row.instance_id),
        ExportedAt = string.IsNullOrWhiteSpace(row.exported_at)
            ? null
            : DateTimeOffset.Parse(row.exported_at),
        IsCurrent = row.is_current != 0
    };

    private sealed class ExportRow
    {
        public string id { get; set; } = string.Empty;
        public string instance_id { get; set; } = string.Empty;
        public string? exported_at { get; set; }
        public int is_current { get; set; }
    }
}
