using System.Reflection;
using ConfigAdmin.Domain.RemoteSync;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncAgentConnectionService : IAsyncDisposable
{
    private readonly SyncAgentClient _client;
    private readonly ILogger<SyncAgentConnectionService> _logger;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private string? _hubUrl;
    private Guid _nodeId;
    private string? _accessToken;
    private int _pollIntervalMs = SyncAgentHubService.DefaultPollIntervalMs;

    public SyncAgentConnectionService(
        SyncAgentClient client,
        ILogger<SyncAgentConnectionService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public event Action<string>? LogLineAdded;
    public event Action<bool>? ConnectionStateChanged;

    public bool IsConnected => _loopCts is not null;
    public string? AccessToken => _accessToken;
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public string StatusText { get; private set; } = "Отключено";

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

    public async Task DisconnectAsync()
    {
        if (_loopCts is null)
            return;

        _loopCts.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }

        _loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;
        _accessToken = null;

        StatusText = "Отключено";
        ConnectionStateChanged?.Invoke(false);
        AddLog("Disconnected");
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollIntervalMs));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync(ct);
                await PollJobsAsync(ct);
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

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        if (_hubUrl is null || _accessToken is null)
            return;

        await _client.HeartbeatAsync(_hubUrl, _accessToken, new HeartbeatRequest
        {
            NodeId = _nodeId,
            Status = "idle"
        }, ct);

        LastHeartbeatAt = DateTimeOffset.Now;
        StatusText = "Подключено";
        ConnectionStateChanged?.Invoke(true);
        AddLog($"Heartbeat {LastHeartbeatAt:HH:mm:ss}");
    }

    private async Task PollJobsAsync(CancellationToken ct)
    {
        if (_hubUrl is null || _accessToken is null)
            return;

        var response = await _client.PollJobsAsync(_hubUrl, _accessToken, _nodeId, ct);
        AddLog(response.Job is null ? "Poll: job=null" : $"Poll: job={response.Job.JobId}");
    }

    private void AddLog(string line) => LogLineAdded?.Invoke(line);

    private static string GetAgentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}
