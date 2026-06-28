namespace ConfigAdmin.Wpf.Services;

public static class SyncTunnelUrlStore
{
    public static string? LoadSavedUrl()
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ConfigAdmin",
                "sync-tunnel.url");

            if (!System.IO.File.Exists(path))
                return null;

            var url = System.IO.File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch
        {
            return null;
        }
    }
}
