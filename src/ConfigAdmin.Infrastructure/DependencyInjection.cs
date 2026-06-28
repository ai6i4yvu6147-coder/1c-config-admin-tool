using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using ConfigAdmin.Domain.Services;
using ConfigAdmin.Infrastructure.Data;
using ConfigAdmin.Infrastructure.FileSystem;
using ConfigAdmin.Infrastructure.Hub;
using ConfigAdmin.Infrastructure.Repositories;
using ConfigAdmin.Infrastructure.Security;
using ConfigAdmin.Integration.OneC;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigAdmin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? databasePath = null)
    {
        services.AddSingleton(new SqliteConnectionFactory(databasePath));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IClientRepository, ClientRepository>();
        services.AddSingleton<IInfobaseRepository, InfobaseRepository>();
        services.AddSingleton<IExportRunRepository, ExportRunRepository>();
        services.AddSingleton<IVaultMetaRepository, VaultMetaRepository>();
        services.AddSingleton<IToolInstanceRepository, ToolInstanceRepository>();
        services.AddSingleton<IHubProjectRepository, HubProjectRepository>();
        services.AddSingleton<IRemoteNodeRepository, RemoteNodeRepository>();
        services.AddSingleton<IAgentSessionRepository, AgentSessionRepository>();
        services.AddSingleton<ISyncJobRepository, SyncJobRepository>();
        services.AddSingleton<PairingSecretService>();
        services.AddSingleton<ISecretVault, SecretVault>();
        services.AddSingleton<ModuleManifestReader>();
        services.AddSingleton<JsonCliRunner>();
        services.AddSingleton<IExportPathBuilder, ExportPathBuilder>();
        services.AddSingleton<IRunArtifactPathBuilder, RunArtifactPathBuilder>();
        services.AddSingleton<AtomicDirectoryService>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<IOneCCliAdapter, OneCCliAdapter>();

        return services;
    }
}
