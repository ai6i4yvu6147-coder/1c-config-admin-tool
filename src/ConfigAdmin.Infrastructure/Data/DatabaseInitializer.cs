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
              last_export_status INTEGER
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
              output_path TEXT
            );

            CREATE TABLE IF NOT EXISTS vault_meta (
              key TEXT PRIMARY KEY,
              value BLOB NOT NULL
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        await MigrateAsync(connection, ct);
    }

    private static async Task MigrateAsync(Microsoft.Data.Sqlite.SqliteConnection connection, CancellationToken ct)
    {
        var hasColumn = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM pragma_table_info('export_runs') WHERE name = 'meta_json_path'",
                cancellationToken: ct));

        if (hasColumn == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE export_runs ADD COLUMN meta_json_path TEXT",
                cancellationToken: ct));
        }
    }
}
