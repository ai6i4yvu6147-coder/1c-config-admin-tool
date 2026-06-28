using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class ToolInstanceRepository : IToolInstanceRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ToolInstanceRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ToolInstanceProfile?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ToolInstanceRow>(
            new CommandDefinition(
                "SELECT * FROM tool_instances WHERE module_id = @ModuleId",
                new { ModuleId = moduleId },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(ToolInstanceProfile instance, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO tool_instances (id, module_id, root_path, enabled)
            VALUES (@Id, @ModuleId, @RootPath, @Enabled)
            ON CONFLICT(module_id) DO UPDATE SET
              root_path = excluded.root_path,
              enabled = excluded.enabled
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = instance.Id.ToString(),
            instance.ModuleId,
            instance.RootPath,
            Enabled = instance.Enabled ? 1 : 0
        }, cancellationToken: ct));
    }

    private static ToolInstanceProfile Map(ToolInstanceRow row) => new()
    {
        Id = Guid.Parse(row.id),
        ModuleId = row.module_id,
        RootPath = row.root_path,
        Enabled = row.enabled != 0
    };

    private sealed class ToolInstanceRow
    {
        public string id { get; set; } = string.Empty;
        public string module_id { get; set; } = string.Empty;
        public string root_path { get; set; } = string.Empty;
        public int enabled { get; set; }
    }
}
