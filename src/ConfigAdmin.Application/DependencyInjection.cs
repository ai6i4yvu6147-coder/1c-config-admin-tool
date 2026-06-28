using ConfigAdmin.Application.Export;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Application.RemoteSync;using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigAdmin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddConfigAdminApplication(
        this IServiceCollection services,
        string? databasePath = null)
    {
        services.AddInfrastructure(databasePath);
        services.AddConfigAdminLogging();
        services.AddSingleton<IExportOrchestrator, ExportOrchestrator>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<VaultSessionService>();
        services.AddSingleton<ConnectionTestService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<ExportRunQueryService>();
        services.AddSingleton<ConfiguratorLaunchService>();
        services.AddSingleton<ManagedToolRegistryService>();
        services.AddSingleton<ConfigMcpToolClient>();
        services.AddSingleton<ConfigMcpFragmentBuilder>();
        services.AddSingleton<ConfigMcpSyncService>();
        services.AddSingleton<RemoteNodeService>();
        services.AddSingleton<SyncAgentHubService>();
        services.AddSingleton<SyncAgentClient>();
        services.AddSingleton<SyncAgentConnectionService>();
        services.AddSingleton<SyncReceiverOptions>();
        services.AddSingleton<SyncReceiverHost>();
        return services;
    }
}
