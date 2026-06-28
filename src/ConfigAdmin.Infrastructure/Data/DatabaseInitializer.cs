using Dapper;

namespace ConfigAdmin.Infrastructure.Data;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        const string sql = """
            CREATE TABLE IF NOT EXISTS clients (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              comment TEXT,
              export_root_path TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS projects (
              id TEXT PRIMARY KEY,
              client_id TEXT NOT NULL REFERENCES clients(id),
              name TEXT NOT NULL,
              active INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS tool_instances (
              id TEXT PRIMARY KEY,
              module_id TEXT NOT NULL UNIQUE,
              root_path TEXT NOT NULL,
              enabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS infobases (
              id TEXT PRIMARY KEY,
              client_id TEXT NOT NULL REFERENCES clients(id),
              name TEXT NOT NULL,
              platform_path TEXT NOT NULL,
              connection_type INTEGER NOT NULL,
              connection_string TEXT NOT NULL,
              username TEXT,
              encrypted_password BLOB,
              export_configuration INTEGER DEFAULT 1,
              export_all_extensions INTEGER DEFAULT 1,
              selected_extensions_json TEXT,
              export_format INTEGER DEFAULT 0,
              last_export_at TEXT,
              last_export_status INTEGER,
              project_id TEXT REFERENCES projects(id),
              config_mcp_project_id TEXT
            );

            CREATE TABLE IF NOT EXISTS export_runs (
              id TEXT PRIMARY KEY,
              infobase_id TEXT NOT NULL,
              started_at TEXT NOT NULL,
              finished_at TEXT,
              success INTEGER,
              exit_code INTEGER,
              duration_ms INTEGER,
              command_masked TEXT,
              error_message TEXT,
              output_path TEXT,
              meta_json_path TEXT
            );

            CREATE TABLE IF NOT EXISTS vault_meta (
              key TEXT PRIMARY KEY,
              value BLOB NOT NULL
            );

            CREATE TABLE IF NOT EXISTS remote_nodes (
              id TEXT PRIMARY KEY,
              client_id TEXT NOT NULL REFERENCES clients(id),
              name TEXT NOT NULL,
              description TEXT,
              pairing_secret_verifier BLOB NOT NULL,
              hub_listen_url TEXT,
              last_seen_at TEXT,
              agent_version TEXT,
              enabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS agent_sessions (
              token_hash BLOB PRIMARY KEY,
              node_id TEXT NOT NULL REFERENCES remote_nodes(id),
              expires_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sync_jobs (
              id TEXT PRIMARY KEY,
              infobase_id TEXT NOT NULL REFERENCES infobases(id),
              remote_node_id TEXT NOT NULL REFERENCES remote_nodes(id),
              status INTEGER NOT NULL,
              requested_at TEXT NOT NULL,
              started_at TEXT,
              finished_at TEXT,
              upload_session_id TEXT,
              bytes_total INTEGER NOT NULL DEFAULT 0,
              bytes_received INTEGER NOT NULL DEFAULT 0,
              content_sha256 TEXT,
              error_message TEXT,
              sync_mcp_after_complete INTEGER NOT NULL DEFAULT 0
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        await EnsureInfobaseHubColumnsAsync(connection, ct);
    }

    private static async Task EnsureInfobaseHubColumnsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken ct)
    {
        foreach (var column in new[] { "project_id", "config_mcp_project_id", "export_location", "remote_node_id", "remote_export_path" })
        {
            var hasColumn = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM pragma_table_info('infobases') WHERE name = @Name",
                    new { Name = column },
                    cancellationToken: ct));

            if (hasColumn == 0)
            {
                var sql = column switch
                {
                    "export_location" => "ALTER TABLE infobases ADD COLUMN export_location INTEGER NOT NULL DEFAULT 0",
                    "remote_node_id" => "ALTER TABLE infobases ADD COLUMN remote_node_id TEXT",
                    "remote_export_path" => "ALTER TABLE infobases ADD COLUMN remote_export_path TEXT",
                    _ => $"ALTER TABLE infobases ADD COLUMN {column} TEXT"
                };
                await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
            }
        }

        var hasMetaJson = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM pragma_table_info('export_runs') WHERE name = 'meta_json_path'",
                cancellationToken: ct));

        if (hasMetaJson == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE export_runs ADD COLUMN meta_json_path TEXT",
                cancellationToken: ct));
        }
    }
}
