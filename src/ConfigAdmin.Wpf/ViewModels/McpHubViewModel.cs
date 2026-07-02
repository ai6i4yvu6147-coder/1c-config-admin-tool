using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class McpHubViewModel : IRefreshOnNavigate
{
    public McpHubViewModel(ConfigMcpViewModel configMcp, DataMcpViewModel dataMcp)
    {
        ConfigMcp = configMcp;
        DataMcp = dataMcp;
    }

    public ConfigMcpViewModel ConfigMcp { get; }
    public DataMcpViewModel DataMcp { get; }

    public async Task RefreshOnNavigateAsync()
    {
        await ConfigMcp.RefreshOnNavigateAsync();
        await DataMcp.RefreshOnNavigateAsync();
    }
}
