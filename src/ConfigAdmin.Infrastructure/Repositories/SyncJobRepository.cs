using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Infrastructure.Data;
using Dapper;

namespace ConfigAdmin.Infrastructure.Repositories;

public sealed class SyncJobRepository : ISyncJobRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SyncJobRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveAsync(SyncJobProfile job, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO sync_jobs (
              id, infobase_id, remote_node_id, status, requested_at, started_at, finished_at,
              upload_session_id, bytes_total, bytes_received, content_sha256, error_message,
              sync_mcp_after_complete
            ) VALUES (
              @Id, @InfobaseId, @RemoteNodeId, @Status, @RequestedAt, @StartedAt, @FinishedAt,
              @UploadSessionId, @BytesTotal, @BytesReceived, @ContentSha256, @ErrorMessage,
              @SyncMcpAfterComplete
            )
            ON CONFLICT(id) DO UPDATE SET
              status = excluded.status,
              started_at = excluded.started_at,
              finished_at = excluded.finished_at,
              upload_session_id = excluded.upload_session_id,
              bytes_total = excluded.bytes_total,
              bytes_received = excluded.bytes_received,
              content_sha256 = excluded.content_sha256,
              error_message = excluded.error_message,
              sync_mcp_after_complete = excluded.sync_mcp_after_complete
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, MapParams(job), cancellationToken: ct));
    }

    public async Task<SyncJobProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SyncJobRow>(
            new CommandDefinition("SELECT * FROM sync_jobs WHERE id = @Id", new { Id = id.ToString() }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<SyncJobProfile?> GetActiveForInfobaseAsync(Guid infobaseId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SyncJobRow>(
            new CommandDefinition(
                """
                SELECT * FROM sync_jobs
                WHERE infobase_id = @InfobaseId
                  AND status NOT IN (@Completed, @Failed, @Cancelled)
                ORDER BY requested_at DESC
                LIMIT 1
                """,
                new
                {
                    InfobaseId = infobaseId.ToString(),
                    Completed = (int)SyncJobStatus.Completed,
                    Failed = (int)SyncJobStatus.Failed,
                    Cancelled = (int)SyncJobStatus.Cancelled
                },
                cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<SyncJobProfile?> ClaimPendingForNodeAsync(Guid nodeId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<SyncJobRow>(
            new CommandDefinition(
                """
                SELECT * FROM sync_jobs
                WHERE remote_node_id = @NodeId AND status = @Pending
                ORDER BY requested_at ASC
                LIMIT 1
                """,
                new { NodeId = nodeId.ToString(), Pending = (int)SyncJobStatus.Pending },
                transaction: tx,
                cancellationToken: ct));

        if (row is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }

        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sync_jobs
            SET status = @Claimed, started_at = @StartedAt
            WHERE id = @Id AND status = @Pending
            """,
            new
            {
                Id = row.id,
                Claimed = (int)SyncJobStatus.Claimed,
                Pending = (int)SyncJobStatus.Pending,
                StartedAt = startedAt
            },
            transaction: tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);
        row.status = (int)SyncJobStatus.Claimed;
        row.started_at = startedAt;
        return Map(row);
    }

    public async Task UpdateStatusAsync(
        Guid id,
        SyncJobStatus status,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var finishedAt = status is SyncJobStatus.Completed or SyncJobStatus.Failed or SyncJobStatus.Cancelled
            ? DateTimeOffset.UtcNow.ToString("O")
            : null;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sync_jobs
            SET status = @Status,
                error_message = @ErrorMessage,
                finished_at = COALESCE(@FinishedAt, finished_at)
            WHERE id = @Id
            """,
            new
            {
                Id = id.ToString(),
                Status = (int)status,
                ErrorMessage = errorMessage,
                FinishedAt = finishedAt
            },
            cancellationToken: ct));
    }

    public async Task UpdateProgressAsync(
        Guid id,
        long bytesTotal,
        long bytesReceived,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sync_jobs
            SET bytes_total = @BytesTotal, bytes_received = @BytesReceived
            WHERE id = @Id
            """,
            new { Id = id.ToString(), BytesTotal = bytesTotal, BytesReceived = bytesReceived },
            cancellationToken: ct));
    }

    public async Task SetUploadSessionAsync(Guid jobId, string sessionId, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sync_jobs
            SET upload_session_id = @SessionId, status = @Uploading
            WHERE id = @Id
            """,
            new
            {
                Id = jobId.ToString(),
                SessionId = sessionId,
                Uploading = (int)SyncJobStatus.Uploading
            },
            cancellationToken: ct));
    }

    private static object MapParams(SyncJobProfile job) => new
    {
        Id = job.Id.ToString(),
        InfobaseId = job.InfobaseId.ToString(),
        RemoteNodeId = job.RemoteNodeId.ToString(),
        Status = (int)job.Status,
        RequestedAt = job.RequestedAt.ToString("O"),
        StartedAt = job.StartedAt?.ToString("O"),
        FinishedAt = job.FinishedAt?.ToString("O"),
        job.UploadSessionId,
        job.BytesTotal,
        job.BytesReceived,
        job.ContentSha256,
        job.ErrorMessage,
        SyncMcpAfterComplete = job.SyncMcpAfterComplete ? 1 : 0
    };

    private static SyncJobProfile Map(SyncJobRow row) => new()
    {
        Id = Guid.Parse(row.id),
        InfobaseId = Guid.Parse(row.infobase_id),
        RemoteNodeId = Guid.Parse(row.remote_node_id),
        Status = (SyncJobStatus)row.status,
        RequestedAt = DateTimeOffset.Parse(row.requested_at),
        StartedAt = ParseNullableDate(row.started_at),
        FinishedAt = ParseNullableDate(row.finished_at),
        UploadSessionId = row.upload_session_id,
        BytesTotal = row.bytes_total,
        BytesReceived = row.bytes_received,
        ContentSha256 = row.content_sha256,
        ErrorMessage = row.error_message,
        SyncMcpAfterComplete = row.sync_mcp_after_complete != 0
    };

    private static DateTimeOffset? ParseNullableDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DateTimeOffset.Parse(value);

    private sealed class SyncJobRow
    {
        public string id { get; set; } = string.Empty;
        public string infobase_id { get; set; } = string.Empty;
        public string remote_node_id { get; set; } = string.Empty;
        public int status { get; set; }
        public string requested_at { get; set; } = string.Empty;
        public string? started_at { get; set; }
        public string? finished_at { get; set; }
        public string? upload_session_id { get; set; }
        public long bytes_total { get; set; }
        public long bytes_received { get; set; }
        public string? content_sha256 { get; set; }
        public string? error_message { get; set; }
        public int sync_mcp_after_complete { get; set; }
    }
}
