using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class ExportRunRepository : IExportRunRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ExportRunRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveAsync(ExportRunLog run, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO export_runs (
              id, infobase_id, started_at, finished_at, success, exit_code,
              duration_ms, command_masked, error_message, output_path, meta_json_path
            ) VALUES (
              @Id, @InfobaseId, @StartedAt, @FinishedAt, @Success, @ExitCode,
              @DurationMs, @CommandMasked, @ErrorMessage, @OutputPath, @MetaJsonPath
            )
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = run.Id.ToString(),
            InfobaseId = run.InfobaseId.ToString(),
            StartedAt = run.StartedAt.ToString("O"),
            FinishedAt = run.FinishedAt?.ToString("O"),
            Success = run.Success ? 1 : 0,
            run.ExitCode,
            run.DurationMs,
            run.CommandMasked,
            run.ErrorMessage,
            run.OutputPath,
            run.MetaJsonPath
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExportRunLog>> GetByInfobaseIdAsync(
        Guid infobaseId,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ExportRunRow>(
            new CommandDefinition(
                """
                SELECT * FROM export_runs
                WHERE infobase_id = @InfobaseId
                ORDER BY started_at DESC
                LIMIT @Limit
                """,
                new { InfobaseId = infobaseId.ToString(), Limit = limit },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ExportRunLog>> GetAllAsync(int limit = 100, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ExportRunRow>(
            new CommandDefinition(
                "SELECT * FROM export_runs ORDER BY started_at DESC LIMIT @Limit",
                new { Limit = limit },
                cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    private static ExportRunLog Map(ExportRunRow row) => new()
    {
        Id = Guid.Parse(row.id),
        InfobaseId = Guid.Parse(row.infobase_id),
        StartedAt = DateTimeOffset.Parse(row.started_at),
        FinishedAt = string.IsNullOrWhiteSpace(row.finished_at) ? null : DateTimeOffset.Parse(row.finished_at),
        Success = row.success != 0,
        ExitCode = row.exit_code,
        DurationMs = row.duration_ms,
        CommandMasked = row.command_masked,
        ErrorMessage = row.error_message,
        OutputPath = row.output_path,
        MetaJsonPath = row.meta_json_path
    };

    private sealed class ExportRunRow
    {
        public string id { get; set; } = string.Empty;
        public string infobase_id { get; set; } = string.Empty;
        public string started_at { get; set; } = string.Empty;
        public string? finished_at { get; set; }
        public int success { get; set; }
        public int exit_code { get; set; }
        public long duration_ms { get; set; }
        public string command_masked { get; set; } = string.Empty;
        public string? error_message { get; set; }
        public string? output_path { get; set; }
        public string? meta_json_path { get; set; }
    }
}
