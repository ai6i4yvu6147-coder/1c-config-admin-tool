using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class VaultMetaRepository : IVaultMetaRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public VaultMetaRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<byte[]?>(
            new CommandDefinition(
                "SELECT value FROM vault_meta WHERE key = @Key",
                new { Key = key },
                cancellationToken: ct));
    }

    public async Task SetAsync(string key, byte[] value, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO vault_meta (key, value) VALUES (@Key, @Value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """,
            new { Key = key, Value = value },
            cancellationToken: ct));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM vault_meta WHERE key = @Key",
                new { Key = key },
                cancellationToken: ct));
        return count > 0;
    }
}
