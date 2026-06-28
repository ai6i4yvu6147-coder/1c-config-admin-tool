using ConfigAdmin.Application.RemoteSync;

namespace ConfigAdmin.Wpf.Services;

public sealed class HubRuntimeService
{
    private readonly SyncReceiverHost _syncReceiverHost;
    private readonly SyncReceiverOptions _syncReceiverOptions;

    public HubRuntimeService(SyncReceiverHost syncReceiverHost, SyncReceiverOptions syncReceiverOptions)
    {
        _syncReceiverHost = syncReceiverHost;
        _syncReceiverOptions = syncReceiverOptions;
    }

    public string ListenUrl => _syncReceiverOptions.ListenUrl;
    public bool IsReceiverRunning => _syncReceiverHost.IsRunning;

    public void ConfigureListenUrl(string listenUrl)
    {
        if (_syncReceiverHost.IsRunning)
            throw new InvalidOperationException("Нельзя менять URL пока receiver запущен.");

        _syncReceiverOptions.ListenUrl = listenUrl;
    }

    public Task StartReceiverAsync(CancellationToken ct = default) =>
        _syncReceiverHost.StartAsync(ct);

    public Task StopReceiverAsync(CancellationToken ct = default) =>
        _syncReceiverHost.StopAsync(ct);
}
