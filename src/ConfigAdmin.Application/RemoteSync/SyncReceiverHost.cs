using System.Net;
using System.Text.Json;
using ConfigAdmin.Domain.RemoteSync;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncReceiverHost : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly SyncReceiverOptions _options;
    private readonly ILogger<SyncReceiverHost> _logger;
    private WebApplication? _app;

    public SyncReceiverHost(
        IServiceProvider serviceProvider,
        SyncReceiverOptions options,
        ILogger<SyncReceiverHost> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    public bool IsRunning => _app is not null;
    public string ListenUrl => _options.ListenUrl;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_app is not null)
            return;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = Array.Empty<string>()
        });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, _options.ListenUrl);

        var app = builder.Build();
        MapEndpoints(app, _serviceProvider);
        await app.StartAsync(ct);

        _app = app;
        _logger.LogInformation("Sync receiver listening on {ListenUrl}", _options.ListenUrl);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is null)
            return;

        await _app.StopAsync(ct);
        await _app.DisposeAsync();
        _app = null;
        _logger.LogInformation("Sync receiver stopped.");
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static void MapEndpoints(WebApplication app, IServiceProvider rootServices)
    {
        app.MapPost("/api/sync-agent/register", (Func<HttpContext, Task<IResult>>)(ctx => RegisterAsync(ctx, rootServices)));
        app.MapPost("/api/sync-agent/heartbeat", (Func<HttpContext, Task<IResult>>)(ctx => HeartbeatAsync(ctx, rootServices)));
        app.MapGet("/api/sync-agent/jobs", (Func<HttpContext, Guid, Task<IResult>>)((ctx, nodeId) => PollJobsAsync(ctx, nodeId, rootServices)));
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, IServiceProvider rootServices)
    {
        RegisterAgentRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<RegisterAgentRequest>(JsonOptions, context.RequestAborted);
        }
        catch (JsonException)
        {
            return Error(HttpStatusCode.BadRequest, "Invalid JSON body.", "INVALID_JSON");
        }

        if (request is null || request.NodeId == Guid.Empty || string.IsNullOrWhiteSpace(request.PairingPassword))
            return Error(HttpStatusCode.BadRequest, "nodeId and pairingPassword are required.", "INVALID_REQUEST");

        var hubService = rootServices.GetRequiredService<SyncAgentHubService>();
        var response = await hubService.RegisterAsync(request, context.RequestAborted);
        if (response is null)
            return Error(HttpStatusCode.Unauthorized, "Invalid pairing password or disabled node.", "AUTH_FAILED");

        return Results.Json(response, JsonOptions);
    }

    private static async Task<IResult> HeartbeatAsync(HttpContext context, IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        HeartbeatRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<HeartbeatRequest>(JsonOptions, context.RequestAborted);
        }
        catch (JsonException)
        {
            return Error(HttpStatusCode.BadRequest, "Invalid JSON body.", "INVALID_JSON");
        }

        if (request is null || request.NodeId == Guid.Empty)
            return Error(HttpStatusCode.BadRequest, "nodeId is required.", "INVALID_REQUEST");

        var hubService = rootServices.GetRequiredService<SyncAgentHubService>();
        var ok = await hubService.HeartbeatAsync(token, request, context.RequestAborted);
        if (!ok)
            return Error(HttpStatusCode.Unauthorized, "Invalid or expired token.", "AUTH_FAILED");

        return Results.Ok();
    }

    private static async Task<IResult> PollJobsAsync(HttpContext context, Guid nodeId, IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        if (nodeId == Guid.Empty)
            return Error(HttpStatusCode.BadRequest, "nodeId is required.", "INVALID_REQUEST");

        var hubService = rootServices.GetRequiredService<SyncAgentHubService>();
        var response = await hubService.PollJobsAsync(token, nodeId, context.RequestAborted);
        if (response is null)
            return Error(HttpStatusCode.Unauthorized, "Invalid or expired token.", "AUTH_FAILED");

        return Results.Json(response, JsonOptions);
    }

    private static bool TryGetBearerToken(HttpContext context, out string token)
    {
        token = string.Empty;
        if (!context.Request.Headers.TryGetValue("Authorization", out var header))
            return false;

        var value = header.ToString();
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        token = value[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    private static IResult Error(HttpStatusCode statusCode, string message, string code) =>
        Results.Json(
            new SyncAgentErrorResponse { Error = message, Code = code },
            JsonOptions,
            statusCode: (int)statusCode);
}
