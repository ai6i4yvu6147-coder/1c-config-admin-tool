using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class AgentSessionRepository : IAgentSessionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AgentSessionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveAsync(AgentSession session, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO agent_sessions (token_hash, node_id, expires_at)
            VALUES (@TokenHash, @NodeId, @ExpiresAt)
            ON CONFLICT(token_hash) DO UPDATE SET
              node_id = excluded.node_id,
              expires_at = excluded.expires_at
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            session.TokenHash,
            NodeId = session.NodeId.ToString(),
            ExpiresAt = session.ExpiresAt.ToString("O")
        }, cancellationToken: ct));
    }

    public async Task<AgentSession?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AgentSessionRow>(
            new CommandDefinition(
                "SELECT token_hash, node_id, expires_at FROM agent_sessions WHERE token_hash = @TokenHash",
                new { TokenHash = tokenHash },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task DeleteByNodeIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM agent_sessions WHERE node_id = @NodeId",
            new { NodeId = nodeId.ToString() },
            cancellationToken: ct));
    }

    public async Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM agent_sessions WHERE expires_at < @Now",
            new { Now = now.ToString("O") },
            cancellationToken: ct));
    }

    private static AgentSession Map(AgentSessionRow row) => new()
    {
        TokenHash = row.token_hash,
        NodeId = Guid.Parse(row.node_id),
        ExpiresAt = DateTimeOffset.Parse(row.expires_at)
    };

    private sealed class AgentSessionRow
    {
        public byte[] token_hash { get; set; } = [];
        public string node_id { get; set; } = string.Empty;
        public string expires_at { get; set; } = string.Empty;
    }
}
