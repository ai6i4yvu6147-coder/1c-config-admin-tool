using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncUploadCompleter
{
    private readonly IClientRepository _clientRepository;
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly ISyncJobRepository _syncJobRepository;
    private readonly IConfigurationInstanceRepository _instanceRepository;
    private readonly IConfigurationExportRepository _exportRepository;
    private readonly IExportPathBuilder _exportPathBuilder;
    private readonly AtomicDirectoryService _directoryService;
    private readonly SyncUploadSessionStore _sessionStore;
    private readonly SyncJobProgressStore _progressStore;
    private readonly ILogger<SyncUploadCompleter> _logger;

    public SyncUploadCompleter(
        IClientRepository clientRepository,
        IInfobaseRepository infobaseRepository,
        ISyncJobRepository syncJobRepository,
        IConfigurationInstanceRepository instanceRepository,
        IConfigurationExportRepository exportRepository,
        IExportPathBuilder exportPathBuilder,
        AtomicDirectoryService directoryService,
        SyncUploadSessionStore sessionStore,
        SyncJobProgressStore progressStore,
        ILogger<SyncUploadCompleter> logger)
    {
        _clientRepository = clientRepository;
        _infobaseRepository = infobaseRepository;
        _syncJobRepository = syncJobRepository;
        _instanceRepository = instanceRepository;
        _exportRepository = exportRepository;
        _exportPathBuilder = exportPathBuilder;
        _directoryService = directoryService;
        _sessionStore = sessionStore;
        _progressStore = progressStore;
        _logger = logger;
    }

    public async Task<CompleteUploadResponse> CompleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var meta = _sessionStore.GetSession(sessionId)
            ?? throw new SyncUploadException("Upload session not found or expired.", "SESSION_EXPIRED", 410);

        var job = await _syncJobRepository.GetByIdAsync(meta.JobId, ct)
            ?? throw new SyncUploadException("Sync job not found.", "JOB_NOT_FOUND", 404);

        var expectedChunks = (int)Math.Ceiling((double)meta.TotalBytes / meta.ChunkSizeBytes);
        if (meta.TotalBytes > 0)
        {
            var maxIndex = expectedChunks - 1;
            for (var i = 0; i <= maxIndex; i++)
            {
                if (!meta.ReceivedChunkIndexes.Contains(i))
                    throw new SyncUploadException("Incomplete chunks.", "HASH_MISMATCH", 409);
            }
        }

        await _syncJobRepository.UpdateStatusAsync(job.Id, SyncJobStatus.Applying, ct: ct);
        _progressStore.Set(job.Id, "applying", "Распаковка и применение в ExportRoot…");

        string? zipPath = null;
        string? extractTemp = null;
        try
        {
            zipPath = _sessionStore.AssembleToFile(sessionId);
            await VerifySha256Async(zipPath, meta.Sha256, ct);

            var profile = await _infobaseRepository.GetByIdAsync(job.InfobaseId, ct)
                ?? throw new SyncUploadException("Infobase not found.", "JOB_NOT_FOUND", 404);
            var client = await _clientRepository.GetByIdAsync(profile.ClientId, ct)
                ?? throw new SyncUploadException("Client not found.", "JOB_NOT_FOUND", 404);

            var instance = job.ConfigurationInstanceId is Guid instanceId
                ? await _instanceRepository.GetByIdAsync(instanceId, ct)
                : null;

            var targetPath = instance?.Kind == ConfigurationKind.Extension && instance.DesignerName is not null
                ? _exportPathBuilder.GetExtensionPath(
                    client.ExportRootPath, client.Name, profile.Name, instance.DesignerName)
                : _exportPathBuilder.GetConfigurationPath(
                    client.ExportRootPath, client.Name, profile.Name);

            extractTemp = Path.Combine(Path.GetDirectoryName(targetPath)!, $"sync_extract_{Guid.NewGuid():N}");
            Directory.CreateDirectory(extractTemp);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractTemp, overwriteFiles: true);

            _directoryService.ReplaceDirectory(targetPath, extractTemp);
            extractTemp = null;

            if (instance is not null)
            {
                await _exportRepository.MarkAllNotCurrentForInstanceAsync(instance.Id, ct);
                await _exportRepository.SaveAsync(new ConfigurationExport
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    ExportedAt = DateTimeOffset.UtcNow,
                    IsCurrent = true
                }, ct);
            }

            await _infobaseRepository.UpdateLastExportAsync(
                profile.Id, DateTimeOffset.UtcNow, ExportStatus.Success, ct);
            await _syncJobRepository.UpdateStatusAsync(job.Id, SyncJobStatus.Completed, ct: ct);
            _sessionStore.DeleteSession(sessionId);
            _progressStore.Clear(job.Id);

            _logger.LogInformation(
                "Sync job {JobId} completed, applied to {TargetPath}",
                job.Id,
                targetPath);

            return new CompleteUploadResponse
            {
                Success = true,
                AppliedPath = targetPath,
                JobStatus = SyncJobStatus.Completed.ToString()
            };
        }
        catch (SyncUploadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _syncJobRepository.UpdateStatusAsync(job.Id, SyncJobStatus.Failed, ex.Message, ct);
            _progressStore.Clear(job.Id);
            throw new SyncUploadException(ex.Message, "APPLY_FAILED", 409);
        }
        finally
        {
            if (extractTemp is not null && Directory.Exists(extractTemp))
                _directoryService.SafeDelete(extractTemp);
        }
    }

    private static async Task VerifySha256Async(string filePath, string expectedHex, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, expectedHex.ToLowerInvariant(), StringComparison.Ordinal))
            throw new SyncUploadException("SHA-256 mismatch.", "HASH_MISMATCH", 409);
    }
}

public sealed class SyncUploadException : Exception
{
    public SyncUploadException(string message, string code, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
