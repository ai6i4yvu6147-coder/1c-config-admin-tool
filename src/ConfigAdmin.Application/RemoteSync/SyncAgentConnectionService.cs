using System.Reflection;
using ConfigAdmin.Domain.RemoteSync;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncAgentConnectionService : IAsyncDisposable
{
    private readonly SyncAgentClient _client;
    private readonly SyncAgentJobProcessor _jobProcessor;
    private readonly ILogger<SyncAgentConnectionService> _logger;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private string? _hubUrl;
    private Guid _nodeId;
    private string? _accessToken;
    private int _pollIntervalMs = SyncAgentHubService.DefaultPollIntervalMs;
    private bool _processingJob;
    private bool _suppressEvents;
    private Guid? _activeJobId;
    private string? _lastProgressMessage;
    private DateTimeOffset _lastProgressHeartbeatUtc;

    public SyncAgentConnectionService(
        SyncAgentClient client,
        SyncAgentJobProcessor jobProcessor,
        ILogger<SyncAgentConnectionService> logger)
    {
        _client = client;
        _jobProcessor = jobProcessor;
        _logger = logger;
        _jobProcessor.ProgressChanged += OnJobProgress;
    }

    public event Action<string>? LogLineAdded;
    public event Action<bool>? ConnectionStateChanged;
    public event Action<string?>? ProgressChanged;

    public bool IsConnected => _loopCts is not null;
    public string? AccessToken => _accessToken;
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public string StatusText { get; private set; } = "Отключено";
    public string? CurrentProgress { get; private set; }
    public bool IsBusy => _processingJob;
    public Guid? ActiveJobId => _processingJob ? _activeJobId : null;

    public async Task ConnectAsync(
        string hubUrl,
        Guid nodeId,
        string pairingPassword,
        CancellationToken ct = default)
    {
        await DisconnectAsync();

        _hubUrl = hubUrl.Trim();
        _nodeId = nodeId;

        var response = await _client.RegisterAsync(_hubUrl, new RegisterAgentRequest
        {
            NodeId = nodeId,
            PairingPassword = pairingPassword,
            AgentVersion = GetAgentVersion(),
            MachineName = Environment.MachineName
        }, ct);

        _accessToken = response.AccessToken;
        _pollIntervalMs = response.PollIntervalMs > 0
            ? response.PollIntervalMs
            : SyncAgentHubService.DefaultPollIntervalMs;

        _loopCts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCts.Token);

        StatusText = "Подключено";
        ConnectionStateChanged?.Invoke(true);
        AddLog($"Register OK, poll={_pollIntervalMs}ms");
    }

    public async Task DisconnectAsync(CancellationToken ct = default) =>
        await StopLoopAsync(ct, suppressEvents: false);

    public Task ShutdownAsync(CancellationToken ct = default) =>
        StopLoopAsync(ct, suppressEvents: true);

    public async ValueTask DisposeAsync() => await ShutdownAsync();

    private async Task StopLoopAsync(CancellationToken ct, bool suppressEvents)
    {
        if (_loopCts is null)
            return;

        _suppressEvents = suppressEvents;
        var loopCts = _loopCts;
        var loopTask = _loopTask;

        loopCts.Cancel();
        if (loopTask is not null)
        {
            try
            {
                await loopTask.WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // expected when the agent loop is cancelled
            }
        }

        loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;
        _accessToken = null;
        _activeJobId = null;
        _processingJob = false;
        CurrentProgress = null;

        if (!_suppressEvents)
        {
            ProgressChanged?.Invoke(null);
            StatusText = "Отключено";
            ConnectionStateChanged?.Invoke(false);
            AddLog("Disconnected");
        }
        else
        {
            StatusText = "Отключено";
            _jobProcessor.ProgressChanged -= OnJobProgress;
        }

        _suppressEvents = false;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollIntervalMs));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_processingJob)
                {
                    await SendHeartbeatAsync("idle", null, null, ct);
                    await PollJobsAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync agent loop error");
                AddLog($"Ошибка: {ex.Message}");
                StatusText = $"Ошибка: {ex.Message}";
                ConnectionStateChanged?.Invoke(false);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(ct))
                    break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SendHeartbeatAsync(
        string status,
        Guid? jobId,
        string? progressMessage,
        CancellationToken ct)
    {
        if (_hubUrl is null || _accessToken is null)
            return;

        await _client.HeartbeatAsync(_hubUrl, _accessToken, new HeartbeatRequest
        {
            NodeId = _nodeId,
            Status = status,
            CurrentJobId = jobId,
            ProgressMessage = progressMessage
        }, ct);

        LastHeartbeatAt = DateTimeOffset.Now;
        StatusText = _processingJob ? "Выполнение задачи..." : "Подключено";
        ConnectionStateChanged?.Invoke(true);
        if (!_processingJob)
            AddLog($"Heartbeat {LastHeartbeatAt:HH:mm:ss}");
    }

    private async Task PollJobsAsync(CancellationToken ct)
    {
        if (_hubUrl is null || _accessToken is null)
            return;

        var response = await _client.PollJobsAsync(_hubUrl, _accessToken, _nodeId, ct);
        if (response.Job is null)
        {
            AddLog("Poll: job=null");
            return;
        }

        AddLog($"Poll: job={response.Job.JobId}");
        _processingJob = true;
        _activeJobId = response.Job.JobId;
        _lastProgressMessage = null;
        StatusText = "Выполнение задачи...";

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var periodicHeartbeat = RunPeriodicJobHeartbeatsAsync(heartbeatCts.Token);

        try
        {
            await SendHeartbeatAsync(
                "exporting",
                response.Job.JobId,
                "Передатчик получил задачу, подготовка выгрузки…",
                ct);

            await _jobProcessor.ProcessJobAsync(
                _hubUrl,
                _accessToken,
                response.Job,
                agentWorkRoot: null,
                ct);

            AddLog($"Job {response.Job.JobId} completed");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job processing failed");
            AddLog($"Job failed: {ex.Message}");
            try
            {
                if (_hubUrl is not null && _accessToken is not null)
                    await _client.FailJobAsync(_hubUrl, _accessToken, response.Job.JobId, ex.Message, ct);
            }
            catch (Exception failEx)
            {
                _logger.LogWarning(failEx, "Failed to report job failure to hub");
            }
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await periodicHeartbeat;
            }
            catch (OperationCanceledException)
            {
                // expected
            }

            _processingJob = false;
            _activeJobId = null;
            CurrentProgress = null;
            ProgressChanged?.Invoke(null);
            StatusText = "Подключено";
        }
    }

    private async Task RunPeriodicJobHeartbeatsAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(ct))
            await TrySendJobProgressHeartbeatAsync(force: true, ct);
    }

    private void OnJobProgress(JobProgressUpdate update)
    {
        if (_suppressEvents)
            return;

        CurrentProgress = update.Message;
        ProgressChanged?.Invoke(update.Message);
        if (update.WriteToJournal)
            LogLineAdded?.Invoke(update.Message);
        _lastProgressMessage = update.Message;
        _ = TrySendJobProgressHeartbeatAsync(
            force: update.WriteToJournal || IsPhaseChange(update.Message),
            CancellationToken.None);
    }

    private async Task TrySendJobProgressHeartbeatAsync(bool force, CancellationToken ct)
    {
        if (_hubUrl is null || _accessToken is null || _activeJobId is null)
            return;

        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastProgressHeartbeatUtc < TimeSpan.FromSeconds(5))
            return;

        _lastProgressHeartbeatUtc = now;
        var status = InferAgentStatus(_lastProgressMessage);
        var message = _lastProgressMessage ?? "Выполнение задачи на RDP…";

        try
        {
            await SendHeartbeatAsync(status, _activeJobId, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Progress heartbeat failed");
        }
    }

    private static bool IsPhaseChange(string message)
    {
        if (ExportDirectoryMonitor.IsRoutineStatusMessage(message))
            return false;

        return message.Contains("DumpConfig", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("завершена", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Упаковка", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Загрузка zip", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Finalize", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Done:", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Resume upload", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Job ", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferAgentStatus(string? message)
    {
        if (message is null)
            return "exporting";

        if (message.Contains("Upload", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("chunk", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Finalize", StringComparison.OrdinalIgnoreCase))
            return "uploading";

        return "exporting";
    }

    private void AddLog(string line)
    {
        if (_suppressEvents)
            return;

        LogLineAdded?.Invoke(line);
    }

    private static string GetAgentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}
