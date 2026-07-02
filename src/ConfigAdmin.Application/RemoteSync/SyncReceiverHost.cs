using System.Net;
using System.Text.Json;
using ConfigAdmin.Application.Hub;
using ConfigAdmin.Domain.RemoteSync;
using ConfigAdmin.Domain.Repositories;
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

        var app = _app;
        _app = null;

        try
        {
            using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stopCts.CancelAfter(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync receiver stop interrupted");
        }
        finally
        {
            try
            {
                await app.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync receiver dispose failed");
            }

            _logger.LogInformation("Sync receiver stopped.");
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static void MapEndpoints(WebApplication app, IServiceProvider rootServices)
    {
        app.MapPost("/api/sync-agent/register", (Delegate)(async (HttpContext ctx) => await RegisterAsync(ctx, rootServices)));
        app.MapPost("/api/sync-agent/heartbeat", (Delegate)(async (HttpContext ctx) => await HeartbeatAsync(ctx, rootServices)));
        app.MapGet("/api/sync-agent/jobs", (Delegate)(async (HttpContext ctx, Guid nodeId) => await PollJobsAsync(ctx, nodeId, rootServices)));
        app.MapPost("/api/sync-agent/jobs/{jobId:guid}/fail", (Delegate)(async (HttpContext ctx, Guid jobId) =>
            await FailJobAsync(ctx, jobId, rootServices)));

        app.MapPost("/api/sync-upload/sessions", (Delegate)(async (HttpContext ctx) => await CreateUploadSessionAsync(ctx, rootServices)));
        app.MapPut("/api/sync-upload/sessions/{sessionId:guid}/chunks/{chunkIndex:int}", CreateUploadChunkHandler(rootServices));
        app.MapGet("/api/sync-upload/sessions/{sessionId:guid}", (Delegate)(async (HttpContext ctx, Guid sessionId) =>
            await GetUploadSessionAsync(ctx, sessionId, rootServices)));
        app.MapPost("/api/sync-upload/sessions/{sessionId:guid}/complete", (Delegate)(async (HttpContext ctx, Guid sessionId) =>
            await CompleteUploadAsync(ctx, sessionId, rootServices)));
    }

    private static Delegate CreateUploadChunkHandler(IServiceProvider rootServices) =>
        async (HttpContext ctx, Guid sessionId, int chunkIndex) =>
            await PutChunkAsync(ctx, sessionId, chunkIndex, rootServices);

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

    private static async Task<IResult> FailJobAsync(HttpContext context, Guid jobId, IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        FailJobRequest? request = null;
        try
        {
            request = await context.Request.ReadFromJsonAsync<FailJobRequest>(JsonOptions, context.RequestAborted);
        }
        catch (JsonException)
        {
            // optional body
        }

        var hubService = rootServices.GetRequiredService<SyncAgentHubService>();
        var ok = await hubService.FailJobAsync(
            token,
            jobId,
            request?.ErrorMessage ?? "Job failed on agent.",
            context.RequestAborted);
        if (!ok)
            return Error(HttpStatusCode.BadRequest, "Unable to fail job.", "JOB_FAIL");

        return Results.Ok();
    }

    private static async Task<IResult> CreateUploadSessionAsync(HttpContext context, IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        CreateUploadSessionRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<CreateUploadSessionRequest>(JsonOptions, context.RequestAborted);
        }
        catch (JsonException)
        {
            return Error(HttpStatusCode.BadRequest, "Invalid JSON body.", "INVALID_JSON");
        }

        if (request is null || request.JobId == Guid.Empty)
            return Error(HttpStatusCode.BadRequest, "jobId is required.", "INVALID_REQUEST");

        var uploadService = rootServices.GetRequiredService<SyncUploadHubService>();
        var response = await uploadService.CreateSessionAsync(token, request, context.RequestAborted);
        if (response is null)
            return Error(HttpStatusCode.Unauthorized, "Invalid token or job.", "AUTH_FAILED");

        return Results.Json(response, JsonOptions, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> PutChunkAsync(
        HttpContext context,
        Guid sessionId,
        int chunkIndex,
        IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        var uploadService = rootServices.GetRequiredService<SyncUploadHubService>();
        var response = await uploadService.PutChunkAsync(
            token, sessionId, chunkIndex, context.Request.Body, context.RequestAborted);
        if (response is null)
            return Error(HttpStatusCode.BadRequest, "Invalid session or token.", "CHUNK_REJECTED");

        return Results.Json(response, JsonOptions);
    }

    private static async Task<IResult> GetUploadSessionAsync(
        HttpContext context,
        Guid sessionId,
        IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        var uploadService = rootServices.GetRequiredService<SyncUploadHubService>();
        var response = await uploadService.GetSessionAsync(token, sessionId, context.RequestAborted);
        if (response is null)
            return Error(HttpStatusCode.Gone, "Session not found or expired.", "SESSION_EXPIRED");

        return Results.Json(response, JsonOptions);
    }

    private static async Task<IResult> CompleteUploadAsync(
        HttpContext context,
        Guid sessionId,
        IServiceProvider rootServices)
    {
        if (!TryGetBearerToken(context, out var token))
            return Error(HttpStatusCode.Unauthorized, "Missing bearer token.", "AUTH_REQUIRED");

        if (await rootServices.GetRequiredService<SyncAgentHubService>().ValidateTokenAsync(token, context.RequestAborted) is null)
            return Error(HttpStatusCode.Unauthorized, "Invalid or expired token.", "AUTH_FAILED");

        try
        {
            var sessionStore = rootServices.GetRequiredService<SyncUploadSessionStore>();
            var meta = sessionStore.GetSession(sessionId);
            if (meta is null)
                return Error(HttpStatusCode.Gone, "Session not found or expired.", "SESSION_EXPIRED");

            var completer = rootServices.GetRequiredService<SyncUploadCompleter>();
            var result = await completer.CompleteAsync(sessionId, context.RequestAborted);

            var jobRepo = rootServices.GetRequiredService<ISyncJobRepository>();
            var job = await jobRepo.GetByIdAsync(meta.JobId, context.RequestAborted);
            if (job?.SyncMcpAfterComplete == true)
            {
                var mcp = rootServices.GetRequiredService<ConfigMcpSyncService>();
                if (job.ConfigurationInstanceId is Guid instanceId)
                    _ = await mcp.SyncInstanceAsync(instanceId, context.RequestAborted);
                else
                    _ = await mcp.SyncInfobaseAsync(job.InfobaseId, context.RequestAborted);
            }

            return Results.Json(result, JsonOptions);
        }
        catch (SyncUploadException ex)
        {
            return Error((HttpStatusCode)ex.StatusCode, ex.Message, ex.Code);
        }
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
