using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;

namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpSyncService
{
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IHubProjectRepository _hubProjectRepository;
    private readonly ConfigMcpFragmentBuilder _fragmentBuilder;
    private readonly ConfigMcpToolClient _toolClient;
    private readonly ManagedToolRegistryService _registryService;

    public ConfigMcpSyncService(
        IInfobaseRepository infobaseRepository,
        IClientRepository clientRepository,
        IHubProjectRepository hubProjectRepository,
        ConfigMcpFragmentBuilder fragmentBuilder,
        ConfigMcpToolClient toolClient,
        ManagedToolRegistryService registryService)
    {
        _infobaseRepository = infobaseRepository;
        _clientRepository = clientRepository;
        _hubProjectRepository = hubProjectRepository;
        _fragmentBuilder = fragmentBuilder;
        _toolClient = toolClient;
        _registryService = registryService;
    }

    public async Task<ConfigMcpSyncResult> SyncInfobaseAsync(Guid infobaseId, CancellationToken ct = default)
    {
        var infobase = await _infobaseRepository.GetByIdAsync(infobaseId, ct);
        if (infobase is null)
            return Fail("База не найдена.");

        if (infobase.ConfigMcpProjectId is null || infobase.ConfigMcpProjectId == Guid.Empty)
            return Fail("База не привязана к проекту config-mcp.");

        var projectName = await ResolveProjectNameAsync(infobase, ct);
        var fragment = await _fragmentBuilder.BuildForInfobaseAsync(
            infobase,
            infobase.ConfigMcpProjectId.Value,
            projectName,
            ct);

        var (response, raw) = await _toolClient.ApplyRegistryAsync(fragment, ct);
        return MapResponse(response, raw.ExitCode);
    }

    public async Task LinkInfobaseToMcpProjectAsync(
        Guid infobaseId,
        Guid configMcpProjectId,
        string projectName,
        CancellationToken ct = default)
    {
        var infobase = await _infobaseRepository.GetByIdAsync(infobaseId, ct)
            ?? throw new InvalidOperationException("База не найдена.");

        var hubProject = await EnsureHubProjectAsync(infobase.ClientId, projectName, ct);
        infobase.ProjectId = hubProject.Id;
        infobase.ConfigMcpProjectId = configMcpProjectId;
        await _infobaseRepository.SaveAsync(infobase, ct);
    }

    public Task<ToolInstanceProfile> EnsureConfigMcpToolAsync(CancellationToken ct = default) =>
        _registryService.GetOrCreateConfigMcpInstanceAsync(ct);

    public Task SaveConfigMcpRootPathAsync(string rootPath, CancellationToken ct = default) =>
        _registryService.SaveConfigMcpRootPathAsync(rootPath, ct);

    public Task<ConfigMcpStatusResponse> GetStatusAsync(CancellationToken ct = default) =>
        _toolClient.GetStatusAsync(ct);

    private async Task<string> ResolveProjectNameAsync(InfobaseProfile infobase, CancellationToken ct)
    {
        if (infobase.ProjectId is Guid hubProjectId)
        {
            var hubProject = await _hubProjectRepository.GetByIdAsync(hubProjectId, ct);
            if (hubProject is not null)
                return hubProject.Name;
        }

        try
        {
            var status = await _toolClient.GetStatusAsync(ct);
            var match = status.Projects.FirstOrDefault(p =>
                string.Equals(p.ProjectId, infobase.ConfigMcpProjectId?.ToString(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Name;
        }
        catch
        {
            // fall back to infobase name
        }

        return infobase.Name;
    }

    private async Task<HubProjectProfile> EnsureHubProjectAsync(
        Guid clientId,
        string projectName,
        CancellationToken ct)
    {
        var projects = await _hubProjectRepository.GetAllAsync(ct);
        var existing = projects.FirstOrDefault(p =>
            p.ClientId == clientId &&
            string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing;

        var created = new HubProjectProfile
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = projectName,
            Active = true
        };
        await _hubProjectRepository.SaveAsync(created, ct);
        return created;
    }

    private static ConfigMcpSyncResult MapResponse(ConfigMcpApplyRegistryResponse response, int exitCode)
    {
        var followUps = response.PostApplyActions?.FollowUpOperations
            .Select(op => new ConfigMcpFollowUpHint
            {
                Command = op.Command,
                Reason = op.Reason ?? string.Empty,
                Blocking = op.Blocking,
                DisplayText = FormatFollowUp(op)
            })
            .ToList() ?? [];

        if (response.Success)
        {
            var changes = response.Changes;
            var summary = changes is null
                ? "Синхронизация с config-mcp выполнена."
                : $"Синхронизация выполнена: создано {changes.Created}, обновлено {changes.Updated}, пропущено {changes.Skipped}.";

            return new ConfigMcpSyncResult
            {
                Success = true,
                Message = summary,
                Warnings = response.Warnings,
                FollowUpHints = followUps
            };
        }

        var errors = response.Errors.Count > 0
            ? response.Errors
            : [$"config-mcp apply-registry завершился с кодом {exitCode}."];

        return new ConfigMcpSyncResult
        {
            Success = false,
            Message = string.Join("; ", errors),
            Errors = errors,
            Warnings = response.Warnings,
            FollowUpHints = followUps
        };
    }

    private static string FormatFollowUp(ConfigMcpFollowUpOperationDto op)
    {
        var args = op.Args is null || op.Args.Count == 0
            ? string.Empty
            : " " + string.Join(" ", op.Args.Select(kv => $"{kv.Key}={kv.Value}"));

        return $"{op.Command}{args}: {op.Reason}";
    }

    private static ConfigMcpSyncResult Fail(string message) => new()
    {
        Success = false,
        Message = message,
        Errors = [message]
    };
}
