using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
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
              configuration_instance_id TEXT REFERENCES configuration_instances(id),
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

            CREATE TABLE IF NOT EXISTS configuration_templates (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              kind INTEGER NOT NULL,
              description TEXT,
              is_system INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS configuration_instances (
              id TEXT PRIMARY KEY,
              infobase_id TEXT NOT NULL REFERENCES infobases(id) ON DELETE CASCADE,
              template_id TEXT REFERENCES configuration_templates(id),
              kind INTEGER NOT NULL,
              display_name TEXT NOT NULL,
              designer_name TEXT,
              export_enabled INTEGER NOT NULL DEFAULT 1,
              sort_order INTEGER NOT NULL DEFAULT 0,
              UNIQUE(infobase_id, template_id),
              UNIQUE(infobase_id, designer_name)
            );

            CREATE TABLE IF NOT EXISTS configuration_exports (
              id TEXT PRIMARY KEY,
              instance_id TEXT NOT NULL REFERENCES configuration_instances(id) ON DELETE CASCADE,
              exported_at TEXT,
              is_current INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS data_mcp_settings (
              tool_instance_id TEXT PRIMARY KEY REFERENCES tool_instances(id) ON DELETE CASCADE,
              endpoint TEXT NOT NULL,
              region TEXT NOT NULL,
              bucket TEXT NOT NULL,
              default_prefix TEXT NOT NULL DEFAULT '',
              sealed_secrets_path TEXT NOT NULL DEFAULT 'credentials.sealed.json',
              encrypted_dmcp_password BLOB
            );

            CREATE TABLE IF NOT EXISTS data_connections (
              id TEXT PRIMARY KEY,
              infobase_id TEXT NOT NULL UNIQUE REFERENCES infobases(id) ON DELETE CASCADE,
              databaseid TEXT NOT NULL,
              display_name TEXT NOT NULL
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        await EnsureInfobaseHubColumnsAsync(connection, ct);
        await EnsureConfigurationInstanceMcpColumnsAsync(connection, ct);
        await EnsureSyncJobInstanceColumnAsync(connection, ct);
        await SeedSystemBaseTemplateAsync(connection, ct);
    }

    private static async Task EnsureConfigurationInstanceMcpColumnsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken ct)
    {
        foreach (var column in new[] { "config_mcp_project_id", "config_mcp_database_id" })
        {
            var hasColumn = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM pragma_table_info('configuration_instances') WHERE name = @Name",
                    new { Name = column },
                    cancellationToken: ct));

            if (hasColumn == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"ALTER TABLE configuration_instances ADD COLUMN {column} TEXT",
                    cancellationToken: ct));
            }
        }
    }

    private static async Task EnsureSyncJobInstanceColumnAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken ct)
    {
        var hasColumn = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM pragma_table_info('sync_jobs') WHERE name = 'configuration_instance_id'",
                cancellationToken: ct));

        if (hasColumn == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE sync_jobs ADD COLUMN configuration_instance_id TEXT REFERENCES configuration_instances(id)",
                cancellationToken: ct));
        }
    }

    private static async Task SeedSystemBaseTemplateAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO configuration_templates (id, name, kind, description, is_system)
            VALUES (@Id, @Name, @Kind, @Description, 1)
            ON CONFLICT(id) DO NOTHING
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = ConfigurationTemplateIds.SystemBaseTemplateId.ToString(),
            Name = "Основная конфигурация",
            Kind = (int)ConfigurationKind.Base,
            Description = "Системный шаблон основной конфигурации"
        }, cancellationToken: ct));
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
