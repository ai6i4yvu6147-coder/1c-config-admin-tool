using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using ConfigAdmin.Domain.RemoteSync;

namespace ConfigAdmin.Application.RemoteSync;

public sealed class SyncAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public SyncAgentClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    public static HttpClient CreateDefaultHttpClient() =>
        new(CreateHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    private static SocketsHttpHandler CreateHandler() =>
        new()
        {
            ConnectCallback = ConnectPreferIpv4Async
        };

    private static async ValueTask<Stream> ConnectPreferIpv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        IPAddress[] addresses;
        try
        {
            addresses = await PublicDnsResolver.ResolveHostAddressesAsync(host, cancellationToken);
        }
        catch (SyncAgentClientException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SyncAgentClientException(
                $"DNS ошибка для «{host}»: {ex.Message}",
                statusCode: 0,
                code: "DNS_ERROR");
        }

        if (addresses.Length == 0)
        {
            throw new SyncAgentClientException(
                $"DNS не вернул адрес для «{host}». Проверьте доступ к интернету и имя Hub URL.",
                statusCode: 0,
                code: "DNS_EMPTY");
        }

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                lastError = ex;
                socket.Dispose();
            }
        }

        throw new SyncAgentClientException(
            FormatConnectError(host, port, lastError),
            statusCode: 0,
            code: "CONNECT_FAILED");
    }

    private static string FormatDnsError(string host, SocketException ex) =>
        ex.SocketErrorCode == SocketError.HostNotFound || ex.NativeErrorCode == 11004
            ? $"DNS не может разрешить «{host}» с RDP (код {ex.NativeErrorCode}). " +
              "Системный DNS заблокирован; попытка через публичный DNS тоже не удалась."
            : $"DNS ошибка для «{host}»: {ex.Message}";

    private static string FormatConnectError(string host, int port, Exception? lastError)
    {
        if (lastError is SocketException { NativeErrorCode: 11004 } sex)
            return FormatDnsError(host, sex);

        return lastError is null
            ? $"Не удалось подключиться к {host}:{port}."
            : $"Не удалось подключиться к {host}:{port}: {lastError.Message}";
    }

    public async Task<RegisterAgentResponse> RegisterAsync(
        string hubUrl,
        RegisterAgentRequest request,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            BuildUri(hubUrl, "/api/sync-agent/register"),
            request,
            JsonOptions,
            ct);

        await EnsureSuccessOrThrowAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<RegisterAgentResponse>(JsonOptions, ct))!;
    }

    public async Task HeartbeatAsync(
        string hubUrl,
        string accessToken,
        HeartbeatRequest request,
        CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(hubUrl, "/api/sync-agent/heartbeat"))
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(message, ct);
        await EnsureSuccessOrThrowAsync(response, ct);
    }

    public async Task<PollJobsResponse> PollJobsAsync(
        string hubUrl,
        string accessToken,
        Guid nodeId,
        CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUri(hubUrl, $"/api/sync-agent/jobs?nodeId={nodeId:D}"));
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(message, ct);
        await EnsureSuccessOrThrowAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<PollJobsResponse>(JsonOptions, ct))!;
    }

    private static Uri BuildUri(string hubUrl, string path)
    {
        var baseUrl = hubUrl.TrimEnd('/');
        return new Uri($"{baseUrl}{path}");
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        SyncAgentErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<SyncAgentErrorResponse>(JsonOptions, ct);
        }
        catch (JsonException)
        {
            // ignore parse errors
        }

        var message = error?.Error ?? response.ReasonPhrase ?? "Sync agent request failed.";
        throw new SyncAgentClientException(message, (int)response.StatusCode, error?.Code);
    }
}

public sealed class SyncAgentClientException : Exception
{
    public SyncAgentClientException(string message, int statusCode, string? code)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string? Code { get; }
}
