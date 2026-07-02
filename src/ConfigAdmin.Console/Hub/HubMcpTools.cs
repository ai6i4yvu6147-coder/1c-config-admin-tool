using System.ComponentModel;
using System.Text.Json;
using ConfigAdmin.Application.Hub;
using ModelContextProtocol.Server;

namespace ConfigAdmin.Console.Hub;

[McpServerToolType]
public sealed class HubMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly InfobaseContextService _contextService;

    public HubMcpTools(InfobaseContextService contextService)
    {
        _contextService = contextService;
    }

    [McpServerTool]
    [Description("List Hub clients (refs only, no secrets).")]
    public async Task<string> ListClients(CancellationToken cancellationToken = default)
    {
        var clients = await _contextService.ListClientsAsync(cancellationToken);
        return JsonSerializer.Serialize(clients, JsonOptions);
    }

    [McpServerTool]
    [Description("List Hub infobases (refs only, no secrets).")]
    public async Task<string> ListInfobases(CancellationToken cancellationToken = default)
    {
        var infobases = await _contextService.ListInfobasesAsync(cancellationToken);
        return JsonSerializer.Serialize(infobases, JsonOptions);
    }

    [McpServerTool(Name = "resolve_infobase_context")]
    [Description(
        "Resolve passive Hub context for an infobase: config-mcp and data-mcp refs (projectFilter, extensionFilter, databaseId) plus credentialsState hint. No passwords or S3 keys.")]
    public async Task<string> ResolveInfobaseContext(
        [Description("Hub infobase UUID.")] string? infobaseId = null,
        [Description("Hub infobase name.")] string? infobaseName = null,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextService.ResolveAsync(infobaseId, infobaseName, cancellationToken);
        return JsonSerializer.Serialize(context, JsonOptions);
    }
}
