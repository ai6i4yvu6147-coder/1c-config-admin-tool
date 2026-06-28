using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class RemoteNodeRepository : IRemoteNodeRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public RemoteNodeRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RemoteNodeProfile>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RemoteNodeRow>(
            new CommandDefinition("SELECT * FROM remote_nodes ORDER BY name", cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RemoteNodeProfile>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RemoteNodeRow>(
            new CommandDefinition(
                "SELECT * FROM remote_nodes WHERE client_id = @ClientId ORDER BY name",
                new { ClientId = clientId.ToString() },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<RemoteNodeProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<RemoteNodeRow>(
            new CommandDefinition(
                "SELECT * FROM remote_nodes WHERE id = @Id",
                new { Id = id.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(RemoteNodeProfile node, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO remote_nodes (
              id, client_id, name, description, pairing_secret_verifier,
              hub_listen_url, last_seen_at, agent_version, enabled
            ) VALUES (
              @Id, @ClientId, @Name, @Description, @PairingSecretVerifier,
              @HubListenUrl, @LastSeenAt, @AgentVersion, @Enabled
            )
            ON CONFLICT(id) DO UPDATE SET
              client_id = excluded.client_id,
              name = excluded.name,
              description = excluded.description,
              pairing_secret_verifier = excluded.pairing_secret_verifier,
              hub_listen_url = excluded.hub_listen_url,
              last_seen_at = excluded.last_seen_at,
              agent_version = excluded.agent_version,
              enabled = excluded.enabled
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = node.Id.ToString(),
            ClientId = node.ClientId.ToString(),
            node.Name,
            node.Description,
            node.PairingSecretVerifier,
            node.HubListenUrl,
            LastSeenAt = node.LastSeenAt?.ToString("O"),
            node.AgentVersion,
            Enabled = node.Enabled ? 1 : 0
        }, cancellationToken: ct));
    }

    public async Task TouchLastSeenAsync(Guid nodeId, DateTimeOffset seenAt, string? agentVersion, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE remote_nodes
            SET last_seen_at = @LastSeenAt,
                agent_version = COALESCE(@AgentVersion, agent_version)
            WHERE id = @Id
            """,
            new
            {
                Id = nodeId.ToString(),
                LastSeenAt = seenAt.ToString("O"),
                AgentVersion = agentVersion
            },
            cancellationToken: ct));
    }

    private static RemoteNodeProfile Map(RemoteNodeRow row) => new()
    {
        Id = Guid.Parse(row.id),
        ClientId = Guid.Parse(row.client_id),
        Name = row.name,
        Description = row.description,
        PairingSecretVerifier = row.pairing_secret_verifier ?? [],
        HubListenUrl = row.hub_listen_url,
        LastSeenAt = string.IsNullOrWhiteSpace(row.last_seen_at)
            ? null
            : DateTimeOffset.Parse(row.last_seen_at),
        AgentVersion = row.agent_version,
        Enabled = row.enabled != 0
    };

    private sealed class RemoteNodeRow
    {
        public string id { get; set; } = string.Empty;
        public string client_id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
        public byte[]? pairing_secret_verifier { get; set; }
        public string? hub_listen_url { get; set; }
        public string? last_seen_at { get; set; }
        public string? agent_version { get; set; }
        public int enabled { get; set; }
    }
}
