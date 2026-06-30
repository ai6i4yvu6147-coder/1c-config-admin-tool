using ConfigAdmin.Domain;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class ConfigurationTemplateRepository : IConfigurationTemplateRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ConfigurationTemplateRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ConfigurationTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TemplateRow>(
            new CommandDefinition(
                "SELECT * FROM configuration_templates ORDER BY is_system DESC, name",
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<ConfigurationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TemplateRow>(
            new CommandDefinition(
                "SELECT * FROM configuration_templates WHERE id = @Id",
                new { Id = id.ToString() },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public Task<ConfigurationTemplate?> GetSystemBaseTemplateAsync(CancellationToken ct = default) =>
        GetByIdAsync(ConfigurationTemplateIds.SystemBaseTemplateId, ct);

    public async Task SaveAsync(ConfigurationTemplate template, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO configuration_templates (id, name, kind, description, is_system)
            VALUES (@Id, @Name, @Kind, @Description, @IsSystem)
            ON CONFLICT(id) DO UPDATE SET
              name = excluded.name,
              kind = excluded.kind,
              description = excluded.description
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = template.Id.ToString(),
            template.Name,
            Kind = (int)template.Kind,
            template.Description,
            IsSystem = template.IsSystem ? 1 : 0
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM configuration_templates WHERE id = @Id AND is_system = 0",
            new { Id = id.ToString() },
            cancellationToken: ct));
    }

    private static ConfigurationTemplate Map(TemplateRow row) => new()
    {
        Id = Guid.Parse(row.id),
        Name = row.name,
        Kind = (ConfigurationKind)row.kind,
        Description = row.description,
        IsSystem = row.is_system != 0
    };

    private sealed class TemplateRow
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public int kind { get; set; }
        public string? description { get; set; }
        public int is_system { get; set; }
    }
}
