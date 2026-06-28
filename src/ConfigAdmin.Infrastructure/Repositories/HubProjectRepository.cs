using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class HubProjectRepository : IHubProjectRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public HubProjectRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<HubProjectProfile>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<HubProjectRow>(
            new CommandDefinition("SELECT * FROM projects ORDER BY name", cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<HubProjectProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<HubProjectRow>(
            new CommandDefinition(
                "SELECT * FROM projects WHERE id = @Id",
                new { Id = id.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(HubProjectProfile project, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO projects (id, client_id, name, active)
            VALUES (@Id, @ClientId, @Name, @Active)
            ON CONFLICT(id) DO UPDATE SET
              client_id = excluded.client_id,
              name = excluded.name,
              active = excluded.active
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = project.Id.ToString(),
            ClientId = project.ClientId.ToString(),
            project.Name,
            Active = project.Active ? 1 : 0
        }, cancellationToken: ct));
    }

    private static HubProjectProfile Map(HubProjectRow row) => new()
    {
        Id = Guid.Parse(row.id),
        ClientId = Guid.Parse(row.client_id),
        Name = row.name,
        Active = row.active != 0
    };

    private sealed class HubProjectRow
    {
        public string id { get; set; } = string.Empty;
        public string client_id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public int active { get; set; }
    }
}
