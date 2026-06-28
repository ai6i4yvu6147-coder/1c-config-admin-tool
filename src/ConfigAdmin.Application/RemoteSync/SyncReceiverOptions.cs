namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncReceiverOptions
{
    public string ListenUrl { get; set; } = "http://0.0.0.0:18443";
}
