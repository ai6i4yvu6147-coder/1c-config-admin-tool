using ConfigAdmin.Application;
using ConfigAdmin.Console.Hub;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ConfigAdmin.Console;

internal static class McpServeHost
{
    public static async Task RunAsync(string? dbPath, CancellationToken ct = default)
    {
        LoggingSetup.Configure();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services.AddConfigAdminApplication(dbPath);
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<HubMcpTools>();

        var host = builder.Build();
        await host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct);
        await host.RunAsync(ct);
    }
}
