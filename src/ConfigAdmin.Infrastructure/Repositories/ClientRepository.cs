using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ClientRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ClientProfile>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ClientRow>(
            new CommandDefinition("SELECT * FROM clients ORDER BY name", cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<ClientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ClientRow>(
            new CommandDefinition("SELECT * FROM clients WHERE id = @Id", new { Id = id.ToString() }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<ClientProfile?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ClientRow>(
            new CommandDefinition("SELECT * FROM clients WHERE name = @Name", new { Name = name }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(ClientProfile client, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO clients (id, name, comment, export_root_path)
            VALUES (@Id, @Name, @Comment, @ExportRootPath)
            ON CONFLICT(id) DO UPDATE SET
              name = excluded.name,
              comment = excluded.comment,
              export_root_path = excluded.export_root_path
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = client.Id.ToString(),
            client.Name,
            client.Comment,
            client.ExportRootPath
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM clients WHERE id = @Id",
            new { Id = id.ToString() },
            cancellationToken: ct));
    }

    private static ClientProfile Map(ClientRow row) => new()
    {
        Id = Guid.Parse(row.id),
        Name = row.name,
        Comment = row.comment,
        ExportRootPath = row.export_root_path
    };

    private sealed class ClientRow
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string? comment { get; set; }
        public string export_root_path { get; set; } = string.Empty;
    }
}
