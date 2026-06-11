using Microsoft.Data.Sqlite;

namespace ConfigAdmin.Infrastructure.Data;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? databasePath = null)
    {
        var path = databasePath ?? AppPaths.DatabasePath;
        AppPaths.EnsureDirectories();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
