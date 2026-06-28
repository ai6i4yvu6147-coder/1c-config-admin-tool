using System.Text.Json;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class InfobaseRepository : IInfobaseRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public InfobaseRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<InfobaseProfile>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<InfobaseRow>(
            new CommandDefinition("SELECT * FROM infobases ORDER BY name", cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<InfobaseProfile>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<InfobaseRow>(
            new CommandDefinition(
                "SELECT * FROM infobases WHERE client_id = @ClientId ORDER BY name",
                new { ClientId = clientId.ToString() },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<InfobaseProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<InfobaseRow>(
            new CommandDefinition(
                "SELECT * FROM infobases WHERE id = @Id",
                new { Id = id.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<InfobaseProfile?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<InfobaseRow>(
            new CommandDefinition(
                "SELECT * FROM infobases WHERE name = @Name",
                new { Name = name },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(InfobaseProfile profile, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO infobases (
              id, client_id, name, platform_path, connection_type, connection_string,
              username, encrypted_password, export_configuration, export_all_extensions,
              selected_extensions_json, export_format, last_export_at, last_export_status,
              project_id, config_mcp_project_id, export_location, remote_node_id, remote_export_path
            ) VALUES (
              @Id, @ClientId, @Name, @PlatformPath, @ConnectionType, @ConnectionString,
              @Username, @EncryptedPassword, @ExportConfiguration, @ExportAllExtensions,
              @SelectedExtensionsJson, @ExportFormat, @LastExportAt, @LastExportStatus,
              @ProjectId, @ConfigMcpProjectId, @ExportLocation, @RemoteNodeId, @RemoteExportPath
            )
            ON CONFLICT(id) DO UPDATE SET
              client_id = excluded.client_id,
              name = excluded.name,
              platform_path = excluded.platform_path,
              connection_type = excluded.connection_type,
              connection_string = excluded.connection_string,
              username = excluded.username,
              encrypted_password = excluded.encrypted_password,
              export_configuration = excluded.export_configuration,
              export_all_extensions = excluded.export_all_extensions,
              selected_extensions_json = excluded.selected_extensions_json,
              export_format = excluded.export_format,
              last_export_at = excluded.last_export_at,
              last_export_status = excluded.last_export_status,
              project_id = excluded.project_id,
              config_mcp_project_id = excluded.config_mcp_project_id,
              export_location = excluded.export_location,
              remote_node_id = excluded.remote_node_id,
              remote_export_path = excluded.remote_export_path
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = profile.Id.ToString(),
            ClientId = profile.ClientId.ToString(),
            profile.Name,
            profile.PlatformPath,
            ConnectionType = (int)profile.ConnectionType,
            profile.ConnectionString,
            profile.Username,
            profile.EncryptedPassword,
            ExportConfiguration = profile.ExportConfiguration ? 1 : 0,
            ExportAllExtensions = profile.ExportAllExtensions ? 1 : 0,
            SelectedExtensionsJson = JsonSerializer.Serialize(profile.SelectedExtensions),
            ExportFormat = (int)profile.ExportFormat,
            LastExportAt = profile.LastExportAt?.ToString("O"),
            LastExportStatus = (int)profile.LastExportStatus,
            ProjectId = profile.ProjectId?.ToString(),
            ConfigMcpProjectId = profile.ConfigMcpProjectId?.ToString(),
            ExportLocation = (int)profile.ExportLocation,
            RemoteNodeId = profile.RemoteNodeId?.ToString(),
            profile.RemoteExportPath
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM infobases WHERE id = @Id",
            new { Id = id.ToString() },
            cancellationToken: ct));
    }

    public async Task UpdateLastExportAsync(
        Guid id,
        DateTimeOffset exportedAt,
        ExportStatus status,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE infobases
            SET last_export_at = @LastExportAt, last_export_status = @LastExportStatus
            WHERE id = @Id
            """,
            new
            {
                Id = id.ToString(),
                LastExportAt = exportedAt.ToString("O"),
                LastExportStatus = (int)status
            },
            cancellationToken: ct));
    }

    private static InfobaseProfile Map(InfobaseRow row) => new()
    {
        Id = Guid.Parse(row.id),
        ClientId = Guid.Parse(row.client_id),
        Name = row.name,
        PlatformPath = row.platform_path,
        ConnectionType = (ConnectionType)row.connection_type,
        ConnectionString = row.connection_string,
        Username = row.username,
        EncryptedPassword = row.encrypted_password,
        ExportConfiguration = row.export_configuration != 0,
        ExportAllExtensions = row.export_all_extensions != 0,
        SelectedExtensions = string.IsNullOrWhiteSpace(row.selected_extensions_json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(row.selected_extensions_json) ?? [],
        ExportFormat = (ExportFormat)row.export_format,
        LastExportAt = string.IsNullOrWhiteSpace(row.last_export_at)
            ? null
            : DateTimeOffset.Parse(row.last_export_at),
        LastExportStatus = (ExportStatus)(row.last_export_status ?? 0),
        ProjectId = ParseNullableGuid(row.project_id),
        ConfigMcpProjectId = ParseNullableGuid(row.config_mcp_project_id),
        ExportLocation = (ExportLocation)(row.export_location ?? 0),
        RemoteNodeId = ParseNullableGuid(row.remote_node_id),
        RemoteExportPath = row.remote_export_path
    };

    private static Guid? ParseNullableGuid(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);

    private sealed class InfobaseRow
    {
        public string id { get; set; } = string.Empty;
        public string client_id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string platform_path { get; set; } = string.Empty;
        public int connection_type { get; set; }
        public string connection_string { get; set; } = string.Empty;
        public string? username { get; set; }
        public byte[]? encrypted_password { get; set; }
        public int export_configuration { get; set; }
        public int export_all_extensions { get; set; }
        public string? selected_extensions_json { get; set; }
        public int export_format { get; set; }
        public string? last_export_at { get; set; }
        public int? last_export_status { get; set; }
        public string? project_id { get; set; }
        public string? config_mcp_project_id { get; set; }
        public int? export_location { get; set; }
        public string? remote_node_id { get; set; }
        public string? remote_export_path { get; set; }
    }
}
