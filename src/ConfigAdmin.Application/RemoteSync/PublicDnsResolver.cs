using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace ConfigAdmin.Application.RemoteSync;

/// <summary>
/// Резолв hostname: сначала системный DNS, при сбое — публичный DNS over HTTPS
/// (обход корпоративного DNS, который блокирует *.ts.net на RDP).
/// </summary>
public static class PublicDnsResolver
{
    private static readonly HttpClient DohClient = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly (string BaseUrl, string Accept)[] DohEndpoints =
    [
        ("https://cloudflare-dns.com/dns-query", "application/dns-json"),
        ("https://dns.google/resolve", "application/json")
    ];

    public static async Task<IPAddress[]> ResolveHostAddressesAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
            return [literal];

        try
        {
            var system = await Dns.GetHostAddressesAsync(host, cancellationToken);
            if (system.Length > 0)
                return OrderPreferIpv4(system);
        }
        catch (SocketException)
        {
            // корпоративный DNS часто возвращает 11004 для *.ts.net
        }

        Exception? lastError = null;
        foreach (var (baseUrl, accept) in DohEndpoints)
        {
            try
            {
                var addresses = await QueryDohAsync(baseUrl, accept, host, cancellationToken);
                if (addresses.Length > 0)
                    return addresses;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new SyncAgentClientException(
            lastError is null
                ? $"DNS не может разрешить «{host}». Системный DNS и публичный DNS (DoH) не вернули адрес."
                : $"DNS не может разрешить «{host}». Системный DNS заблокирован; публичный DNS тоже недоступен: {lastError.Message}",
            statusCode: 0,
            code: "DNS_ERROR");
    }

    internal static IPAddress[] ParseDohResponse(DohJsonResponse? response)
    {
        if (response is null || response.Status != 0 || response.Answer is null)
            return [];

        return response.Answer
            .Where(static a => a.Type == 1 && !string.IsNullOrWhiteSpace(a.Data))
            .Select(static a => IPAddress.Parse(a.Data!))
            .Distinct()
            .ToArray();
    }

    private static async Task<IPAddress[]> QueryDohAsync(
        string baseUrl,
        string acceptHeader,
        string host,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}?name={Uri.EscapeDataString(host)}&type=A";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", acceptHeader);

        using var response = await DohClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DohJsonResponse>(cancellationToken: cancellationToken);
        return OrderPreferIpv4(ParseDohResponse(payload));
    }

    private static IPAddress[] OrderPreferIpv4(IReadOnlyList<IPAddress> addresses) =>
        addresses
            .OrderBy(static a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .ToArray();

    internal sealed class DohJsonResponse
    {
        [JsonPropertyName("Status")]
        public int Status { get; set; }

        [JsonPropertyName("Answer")]
        public DohAnswer[]? Answer { get; set; }
    }

    internal sealed class DohAnswer
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}
