using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ConfigAdmin.Infrastructure;

public static class LoggingSetup
{
    public static void Configure()
    {
        AppPaths.EnsureDirectories();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "configadmin-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }

    public static IServiceCollection AddConfigAdminLogging(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSerilog(dispose: true));
        return services;
    }
}
