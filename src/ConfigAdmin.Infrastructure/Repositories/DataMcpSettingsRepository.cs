using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class DataMcpSettingsRepository : IDataMcpSettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DataMcpSettingsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DataMcpSettings?> GetByToolInstanceIdAsync(Guid toolInstanceId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<DataMcpSettingsRow>(
            new CommandDefinition(
                "SELECT * FROM data_mcp_settings WHERE tool_instance_id = @ToolInstanceId",
                new { ToolInstanceId = toolInstanceId.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<DataMcpSettings?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<DataMcpSettingsRow>(
            new CommandDefinition(
                """
                SELECT s.*
                FROM data_mcp_settings s
                INNER JOIN tool_instances t ON t.id = s.tool_instance_id
                WHERE t.module_id = @ModuleId
                """,
                new { ModuleId = moduleId },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(DataMcpSettings settings, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO data_mcp_settings (
              tool_instance_id, endpoint, region, bucket, default_prefix,
              sealed_secrets_path, encrypted_dmcp_password
            ) VALUES (
              @ToolInstanceId, @Endpoint, @Region, @Bucket, @DefaultPrefix,
              @SealedSecretsPath, @EncryptedDmcpPassword
            )
            ON CONFLICT(tool_instance_id) DO UPDATE SET
              endpoint = excluded.endpoint,
              region = excluded.region,
              bucket = excluded.bucket,
              default_prefix = excluded.default_prefix,
              sealed_secrets_path = excluded.sealed_secrets_path,
              encrypted_dmcp_password = excluded.encrypted_dmcp_password
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            ToolInstanceId = settings.ToolInstanceId.ToString(),
            settings.Endpoint,
            settings.Region,
            settings.Bucket,
            settings.DefaultPrefix,
            settings.SealedSecretsPath,
            settings.EncryptedDmcpPassword
        }, cancellationToken: ct));
    }

    public async Task DeleteByToolInstanceIdAsync(Guid toolInstanceId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM data_mcp_settings WHERE tool_instance_id = @ToolInstanceId",
            new { ToolInstanceId = toolInstanceId.ToString() },
            cancellationToken: ct));
    }

    private static DataMcpSettings Map(DataMcpSettingsRow row) => new()
    {
        ToolInstanceId = Guid.Parse(row.tool_instance_id),
        Endpoint = row.endpoint,
        Region = row.region,
        Bucket = row.bucket,
        DefaultPrefix = row.default_prefix,
        SealedSecretsPath = row.sealed_secrets_path,
        EncryptedDmcpPassword = row.encrypted_dmcp_password
    };

    private sealed class DataMcpSettingsRow
    {
        public string tool_instance_id { get; set; } = string.Empty;
        public string endpoint { get; set; } = string.Empty;
        public string region { get; set; } = string.Empty;
        public string bucket { get; set; } = string.Empty;
        public string default_prefix { get; set; } = string.Empty;
        public string sealed_secrets_path { get; set; } = string.Empty;
        public byte[]? encrypted_dmcp_password { get; set; }
    }
}
