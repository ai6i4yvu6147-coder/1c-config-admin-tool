using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class DataConnectionRepository : IDataConnectionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DataConnectionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DataConnection?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<DataConnectionRow>(
            new CommandDefinition(
                "SELECT * FROM data_connections WHERE id = @Id",
                new { Id = id.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<DataConnection?> GetByInfobaseIdAsync(Guid infobaseId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<DataConnectionRow>(
            new CommandDefinition(
                "SELECT * FROM data_connections WHERE infobase_id = @InfobaseId",
                new { InfobaseId = infobaseId.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<DataConnection>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<DataConnectionRow>(
            new CommandDefinition(
                "SELECT * FROM data_connections ORDER BY display_name",
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DataConnection>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<DataConnectionRow>(
            new CommandDefinition(
                """
                SELECT c.*
                FROM data_connections c
                INNER JOIN infobases i ON i.id = c.infobase_id
                WHERE i.client_id = @ClientId
                ORDER BY c.display_name
                """,
                new { ClientId = clientId.ToString() },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task SaveAsync(DataConnection connection, CancellationToken ct = default)
    {
        await using var dbConnection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO data_connections (id, infobase_id, databaseid, display_name)
            VALUES (@Id, @InfobaseId, @DatabaseId, @DisplayName)
            ON CONFLICT(id) DO UPDATE SET
              infobase_id = excluded.infobase_id,
              databaseid = excluded.databaseid,
              display_name = excluded.display_name
            """;

        await dbConnection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = connection.Id.ToString(),
            InfobaseId = connection.InfobaseId.ToString(),
            DatabaseId = connection.DatabaseId,
            connection.DisplayName
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM data_connections WHERE id = @Id",
            new { Id = id.ToString() },
            cancellationToken: ct));
    }

    private static DataConnection Map(DataConnectionRow row) => new()
    {
        Id = Guid.Parse(row.id),
        InfobaseId = Guid.Parse(row.infobase_id),
        DatabaseId = row.databaseid,
        DisplayName = row.display_name
    };

    private sealed class DataConnectionRow
    {
        public string id { get; set; } = string.Empty;
        public string infobase_id { get; set; } = string.Empty;
        public string databaseid { get; set; } = string.Empty;
        public string display_name { get; set; } = string.Empty;
    }
}
