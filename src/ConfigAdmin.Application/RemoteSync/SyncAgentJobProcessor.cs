using System.IO.Compression;
using System.Security.Cryptography;
using ConfigAdmin.Domain.RemoteSync;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncAgentJobProcessor
{
    private readonly SyncAgentClient _client;
    private readonly RemoteConfigurationExportService _exportService;
    private readonly AgentWorkDirectoryResolver _workDirectoryResolver;
    private readonly AgentResumeStore _resumeStore;
    private readonly ILogger<SyncAgentJobProcessor> _logger;

    public SyncAgentJobProcessor(
        SyncAgentClient client,
        RemoteConfigurationExportService exportService,
        AgentWorkDirectoryResolver workDirectoryResolver,
        AgentResumeStore resumeStore,
        ILogger<SyncAgentJobProcessor> logger)
    {
        _client = client;
        _exportService = exportService;
        _workDirectoryResolver = workDirectoryResolver;
        _resumeStore = resumeStore;
        _logger = logger;
    }

    public event Action<JobProgressUpdate>? ProgressChanged;

    public async Task ProcessJobAsync(
        string hubUrl,
        string accessToken,
        SyncJobDto job,
        string? agentWorkRoot,
        CancellationToken ct)
    {
        var workRoot = _workDirectoryResolver.GetJobWorkRoot(job.JobId, agentWorkRoot);
        var configPath = _workDirectoryResolver.GetInstanceWorkPath(
            job.JobId,
            job.Export.Kind,
            job.Export.DesignerName,
            string.IsNullOrWhiteSpace(job.RemoteExportPath) ? null : job.RemoteExportPath,
            agentWorkRoot);

        try
        {
            Report($"Job {job.JobId}: выгрузка {job.Export.DisplayName} → {configPath}");
            Report($"Выгрузка 1С (DumpConfigToFiles, может занять много времени)…");
            var password = JobCredentialsCipher.Decrypt(
                accessToken, job.JobId, job.NodeId, job.EncryptedConnectionPassword);

            var exportResult = await _exportService.ExportInstanceAsync(
                job.Export,
                password,
                configPath,
                new Progress<ExportDirectoryStats>(stats =>
                    ReportStatus(ExportDirectoryMonitor.FormatProgressMessage(stats))),
                ct);

            if (!exportResult.Success)
                throw new InvalidOperationException(exportResult.ErrorMessage ?? "Export failed.");

            Report("Выгрузка 1С завершена, упаковка zip…");

            var zipPath = Path.Combine(workRoot, "configuration.zip");
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            Report("Упаковка zip…");
            ZipFile.CreateFromDirectory(configPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            var fileInfo = new FileInfo(zipPath);
            var sha256 = await ComputeSha256HexAsync(zipPath, ct);
            var chunkSize = job.MaxChunkSizeBytes > 0
                ? job.MaxChunkSizeBytes
                : SyncUploadSessionStore.DefaultChunkSizeBytes;

            var resume = _resumeStore.LoadLatestForJob(job.JobId);
            Guid sessionId;
            int[] alreadySent;

            if (resume is not null && File.Exists(resume.ZipPath) &&
                string.Equals(resume.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            {
                sessionId = resume.SessionId;
                alreadySent = resume.SentChunkIndexes.ToArray();
                Report($"Resume upload session {sessionId}, sent={alreadySent.Length}");
            }
            else
            {
                var created = await _client.CreateUploadSessionAsync(hubUrl, accessToken, new CreateUploadSessionRequest
                {
                    JobId = job.JobId,
                    FileName = "configuration.zip",
                    TotalBytes = fileInfo.Length,
                    Sha256 = sha256,
                    ChunkSizeBytes = chunkSize
                }, ct);

                sessionId = created.SessionId;
                alreadySent = created.AcceptedChunks;
                resume = new AgentResumeState
                {
                    SessionId = sessionId,
                    JobId = job.JobId,
                    Sha256 = sha256,
                    ZipPath = zipPath,
                    TotalBytes = fileInfo.Length,
                    ChunkSizeBytes = chunkSize
                };
            }

            var sentSet = alreadySent.ToHashSet();
            var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);

            Report("Загрузка zip на Hub…");
            await using var zipStream = File.OpenRead(zipPath);
            for (var index = 0; index < totalChunks; index++)
            {
                ct.ThrowIfCancellationRequested();
                if (sentSet.Contains(index))
                    continue;

                var offset = (long)index * chunkSize;
                var length = (int)Math.Min(chunkSize, fileInfo.Length - offset);
                zipStream.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[length];
                _ = await zipStream.ReadAsync(buffer.AsMemory(0, length), ct);

                await using var chunkStream = new MemoryStream(buffer, writable: false);
                await UploadChunkWithRetryAsync(hubUrl, accessToken, sessionId, index, chunkStream, ct);

                sentSet.Add(index);
                resume!.SentChunkIndexes = sentSet.OrderBy(i => i).ToList();
                _resumeStore.Save(resume);

                var pct = fileInfo.Length == 0 ? 100 : (int)(sentSet.Count * 100.0 / totalChunks);
                ReportStatus($"Upload {pct}% ({sentSet.Count}/{totalChunks} chunks, {FormatBytes(fileInfo.Length)})");
            }

            Report("Finalize on Hub...");
            var complete = await _client.CompleteUploadAsync(hubUrl, accessToken, sessionId, ct);
            Report($"Done: {complete.AppliedPath}");

            _resumeStore.Delete(sessionId);
            TryCleanupWorkDir(workRoot, configPath, zipPath);
        }
        catch
        {
            throw;
        }
    }

    private async Task UploadChunkWithRetryAsync(
        string hubUrl,
        string accessToken,
        Guid sessionId,
        int chunkIndex,
        MemoryStream chunkStream,
        CancellationToken ct)
    {
        const int maxAttempts = 3;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            chunkStream.Position = 0;

            try
            {
                await _client.UploadChunkAsync(hubUrl, accessToken, sessionId, chunkIndex, chunkStream, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientUploadError(ex))
            {
                lastError = ex;
                _logger.LogWarning(ex, "Chunk {ChunkIndex} upload attempt {Attempt} failed", chunkIndex, attempt);
                Report($"Повтор chunk {chunkIndex} ({attempt}/{maxAttempts})…");
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }

        throw lastError ?? new InvalidOperationException($"Chunk {chunkIndex} upload failed.");
    }

    private static bool IsTransientUploadError(Exception ex) =>
        ex is TaskCanceledException or TimeoutException or SyncAgentClientException { StatusCode: 0 }
        || ex.InnerException is TaskCanceledException or TimeoutException;

    private void TryCleanupWorkDir(string workRoot, string configPath, string zipPath)
    {
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            if (Directory.Exists(configPath))
                Directory.Delete(configPath, recursive: true);
            if (Directory.Exists(workRoot) && !Directory.EnumerateFileSystemEntries(workRoot).Any())
                Directory.Delete(workRoot, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup work dir {WorkRoot}", workRoot);
        }
    }

    private static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void Report(string message) =>
        ProgressChanged?.Invoke(new JobProgressUpdate(message, WriteToJournal: true));

    private void ReportStatus(string message) =>
        ProgressChanged?.Invoke(new JobProgressUpdate(message, WriteToJournal: false));

    private static string FormatBytes(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
}
