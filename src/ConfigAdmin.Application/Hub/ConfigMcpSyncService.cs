using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Application.Services;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.Hub;

public sealed class ConfigMcpSyncService
{
    private readonly IInfobaseRepository _infobaseRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IHubProjectRepository _hubProjectRepository;
    private readonly IConfigurationInstanceRepository _instanceRepository;
    private readonly ConfigMcpFragmentBuilder _fragmentBuilder;
    private readonly IConfigMcpToolClient _toolClient;
    private readonly ManagedToolRegistryService _registryService;
    private readonly InfobaseConfigurationService _configurationService;
    private readonly ConfigMcpProjectsJsonMerger _projectsJsonMerger;
    private readonly ILogger<ConfigMcpSyncService> _logger;

    public ConfigMcpSyncService(
        IInfobaseRepository infobaseRepository,
        IClientRepository clientRepository,
        IHubProjectRepository hubProjectRepository,
        IConfigurationInstanceRepository instanceRepository,
        ConfigMcpFragmentBuilder fragmentBuilder,
        IConfigMcpToolClient toolClient,
        ManagedToolRegistryService registryService,
        InfobaseConfigurationService configurationService,
        ConfigMcpProjectsJsonMerger projectsJsonMerger,
        ILogger<ConfigMcpSyncService> logger)
    {
        _infobaseRepository = infobaseRepository;
        _clientRepository = clientRepository;
        _hubProjectRepository = hubProjectRepository;
        _instanceRepository = instanceRepository;
        _fragmentBuilder = fragmentBuilder;
        _toolClient = toolClient;
        _registryService = registryService;
        _configurationService = configurationService;
        _projectsJsonMerger = projectsJsonMerger;
        _logger = logger;
    }

    public async Task<ConfigMcpSyncResult> SyncInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct);
        if (instance is null)
            return Fail("Экземпляр конфигурации не найден.");

        if (!instance.IsMcpLinked)
            return Fail("Экземпляр не привязан к проекту config-mcp.");

        var projectName = await ResolveProjectNameAsync(instance, ct);
        try
        {
            var fragment = await _fragmentBuilder.BuildForInstanceAsync(instanceId, projectName, ct);
            var registryPath = await ResolveProjectsJsonPathAsync(ct);
            return await ApplyFragmentAsync(fragment, registryPath, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(ex.Message);
        }
    }

    public async Task<ConfigMcpSyncResult> SyncAllLinkedInstancesAsync(CancellationToken ct = default)
    {
        var instances = (await _instanceRepository.GetAllAsync(ct))
            .Where(i => i.IsMcpLinked)
            .ToList();

        if (instances.Count == 0)
            return Fail("Нет привязанных экземпляров.");

        var projectNames = new Dictionary<Guid, string>();
        foreach (var projectId in instances.Select(i => i.ConfigMcpProjectId!.Value).Distinct())
        {
            var sample = instances.First(i => i.ConfigMcpProjectId == projectId);
            projectNames[projectId] = await ResolveProjectNameAsync(sample, ct);
        }

        var fragment = await _fragmentBuilder.BuildForInstancesAsync(
            instances.Select(i => i.Id).ToList(),
            projectNames,
            ct);

        var registryPath = await ResolveProjectsJsonPathAsync(ct);
        return await ApplyFragmentAsync(fragment, registryPath, ct);
    }

    /// <summary>
    /// Sync linked instances from an explicit list (e.g. session export plan).
    /// </summary>
    public async Task<ConfigMcpSyncResult> SyncInstancesAsync(
        IEnumerable<Guid> instanceIds,
        CancellationToken ct = default)
    {
        var instances = new List<ConfigurationInstance>();
        foreach (var id in instanceIds.Distinct())
        {
            var instance = await _instanceRepository.GetByIdAsync(id, ct);
            if (instance is not null && instance.IsMcpLinked)
                instances.Add(instance);
        }

        return await SyncInstancesCoreAsync(instances, "Нет привязанных экземпляров для синхронизации.", ct);
    }

    /// <summary>
    /// Sync all linked instances for one infobase (used after full export).
    /// </summary>
    public async Task<ConfigMcpSyncResult> SyncInfobaseAsync(Guid infobaseId, CancellationToken ct = default)
    {
        var instances = (await _configurationService.GetInstancesAsync(infobaseId, ct))
            .Where(i => i.IsMcpLinked && i.ExportEnabled)
            .ToList();

        return await SyncInstancesCoreAsync(instances, "Нет привязанных экземпляров для этой базы.", ct);
    }

    private async Task<ConfigMcpSyncResult> SyncInstancesCoreAsync(
        IReadOnlyList<ConfigurationInstance> instances,
        string emptyMessage,
        CancellationToken ct)
    {
        if (instances.Count == 0)
            return Fail(emptyMessage);

        if (instances.Count == 1)
            return await SyncInstanceAsync(instances[0].Id, ct);

        var projectNames = new Dictionary<Guid, string>();
        foreach (var projectId in instances.Select(i => i.ConfigMcpProjectId!.Value).Distinct())
        {
            var sample = instances.First(i => i.ConfigMcpProjectId == projectId);
            projectNames[projectId] = await ResolveProjectNameAsync(sample, ct);
        }

        var fragment = await _fragmentBuilder.BuildForInstancesAsync(
            instances.Select(i => i.Id).ToList(),
            projectNames,
            ct);

        var registryPath = await ResolveProjectsJsonPathAsync(ct);
        return await ApplyFragmentAsync(fragment, registryPath, ct);
    }

    private async Task<ConfigMcpSyncResult> ApplyFragmentAsync(
        ConfigMcpRegistryFragmentDocument fragment,
        string registryPath,
        CancellationToken ct)
    {
        var (response, raw) = await _toolClient.ApplyRegistryAsync(fragment, ct);
        var result = MapResponse(response, raw.ExitCode, registryPath);

        if (ShouldTryPlannedPathMerge(response, result))
        {
            if (_projectsJsonMerger.TryMergePlannedRegistry(registryPath, fragment, out var merged, out var mergeError))
            {
                return new ConfigMcpSyncResult
                {
                    Success = true,
                    Message = $"Запись добавлена в projects.json по planned path ({merged} database). " +
                              "Каталог создастся при выгрузке; индекс — после rebuild-index.",
                    Warnings = result.Warnings,
                    FollowUpHints = result.FollowUpHints,
                    ChangesCreated = merged,
                    ChangesUpdated = result.ChangesUpdated,
                    ChangesSkipped = result.ChangesSkipped,
                    RegistryPath = registryPath
                };
            }

            return Fail(
                $"Не удалось записать planned path в projects.json: {mergeError}",
                result);
        }

        if (!result.Success)
            return result;

        return await RunIndexRebuildsAfterApplyAsync(result, response, fragment, ct);
    }

    private async Task<ConfigMcpSyncResult> RunIndexRebuildsAfterApplyAsync(
        ConfigMcpSyncResult applyResult,
        ConfigMcpApplyRegistryResponse response,
        ConfigMcpRegistryFragmentDocument fragment,
        CancellationToken ct)
    {
        var databaseIds = CollectRebuildDatabaseIds(response, fragment);
        if (databaseIds.Count == 0)
            return applyResult;

        var warnings = applyResult.Warnings.ToList();
        var succeeded = 0;
        var failed = 0;
        var details = new List<string>();

        foreach (var databaseId in databaseIds)
        {
            try
            {
                var (rebuildResponse, raw) = await _toolClient.RebuildIndexAsync(databaseId, ct);
                if (raw.ExitCode == 0 && rebuildResponse.Success)
                {
                    succeeded++;
                    var fileHint = string.IsNullOrWhiteSpace(rebuildResponse.DbFile)
                        ? string.Empty
                        : $" → {rebuildResponse.DbFile}";
                    details.Add($"{databaseId}: OK{fileHint}");
                    warnings.AddRange(rebuildResponse.Warnings);
                }
                else
                {
                    failed++;
                    var error = rebuildResponse.Errors.Count > 0
                        ? string.Join("; ", rebuildResponse.Errors)
                        : $"CLI exit {raw.ExitCode}";
                    details.Add($"{databaseId}: {error}");
                    warnings.Add($"rebuild-index {databaseId}: {error}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"{databaseId}: {ex.Message}");
                warnings.Add($"rebuild-index {databaseId}: {ex.Message}");
            }
        }

        var indexSummary = failed == 0
            ? $" Индекс MCP: построено {succeeded}."
            : $" Индекс MCP: успешно {succeeded}, ошибок {failed} ({string.Join("; ", details)}).";

        var remainingFollowUps = applyResult.FollowUpHints
            .Where(h => !string.Equals(h.Command, "rebuild-index", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ConfigMcpSyncResult
        {
            Success = applyResult.Success && failed == 0,
            Message = applyResult.Message + indexSummary,
            Warnings = warnings,
            Errors = failed > 0
                ? [$"Не удалось построить индекс MCP для {failed} database."]
                : applyResult.Errors,
            FollowUpHints = remainingFollowUps,
            ChangesCreated = applyResult.ChangesCreated,
            ChangesUpdated = applyResult.ChangesUpdated,
            ChangesSkipped = applyResult.ChangesSkipped,
            RegistryPath = applyResult.RegistryPath,
            IndexRebuildsSucceeded = succeeded,
            IndexRebuildsFailed = failed
        };
    }

    internal static List<string> CollectRebuildDatabaseIds(
        ConfigMcpApplyRegistryResponse response,
        ConfigMcpRegistryFragmentDocument fragment)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in response.PostApplyActions?.FollowUpOperations ?? [])
        {
            if (!string.Equals(operation.Command, "rebuild-index", StringComparison.OrdinalIgnoreCase))
                continue;

            var databaseId = ExtractDatabaseIdFromArgs(operation.Args);
            if (!string.IsNullOrWhiteSpace(databaseId))
                ids.Add(databaseId);
        }

        if (ids.Count > 0)
            return ids.ToList();

        foreach (var project in fragment.RegistryFragment.Projects)
        {
            foreach (var database in project.Databases)
            {
                if (string.IsNullOrWhiteSpace(database.SourcePath))
                    continue;
                if (!Directory.Exists(database.SourcePath))
                    continue;
                if (!string.IsNullOrWhiteSpace(database.InfobaseId))
                    ids.Add(database.InfobaseId);
            }
        }

        return ids.ToList();
    }

    internal static string? ExtractDatabaseIdFromArgs(Dictionary<string, string>? args)
    {
        if (args is null)
            return null;

        foreach (var key in new[] { "db-id", "dbId", "databaseId", "infobaseId" })
        {
            if (args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    internal static bool ShouldTryPlannedPathMerge(
        ConfigMcpApplyRegistryResponse response,
        ConfigMcpSyncResult result)
    {
        return response.Warnings.Any(HasPathNotFoundWarning);
    }

    internal static bool HasPathNotFoundWarning(string warning) =>
        warning.Contains("directory not found", StringComparison.OrdinalIgnoreCase) ||
        (warning.Contains("sourcepath", StringComparison.OrdinalIgnoreCase) &&
         warning.Contains("not found", StringComparison.OrdinalIgnoreCase));

    public async Task LinkInstanceAsync(
        Guid instanceId,
        ConfigMcpLinkRequest request,
        CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct)
            ?? throw new InvalidOperationException("Экземпляр конфигурации не найден.");

        var infobase = await _infobaseRepository.GetByIdAsync(instance.InfobaseId, ct)
            ?? throw new InvalidOperationException("Инфобаза не найдена.");

        switch (request.Mode)
        {
            case ConfigMcpLinkMode.ExistingDatabase:
                if (request.ProjectId is not Guid projectId || projectId == Guid.Empty)
                    throw new InvalidOperationException("Не выбран проект config-mcp.");
                if (request.DatabaseId is not Guid databaseId || databaseId == Guid.Empty)
                    throw new InvalidOperationException("Не выбрана database config-mcp.");
                instance.ConfigMcpProjectId = projectId;
                instance.ConfigMcpDatabaseId = databaseId;
                break;

            case ConfigMcpLinkMode.NewDatabaseInProject:
                if (request.ProjectId is not Guid newDbProjectId || newDbProjectId == Guid.Empty)
                    throw new InvalidOperationException("Не выбран проект config-mcp.");
                instance.ConfigMcpProjectId = newDbProjectId;
                instance.ConfigMcpDatabaseId = null;
                break;

            case ConfigMcpLinkMode.NewProject:
                if (string.IsNullOrWhiteSpace(request.ProjectName))
                    throw new InvalidOperationException("Укажите имя нового проекта.");
                var existingMcpProjectId = await FindExistingMcpProjectIdForClientAsync(infobase.ClientId, ct);
                var newProjectId = existingMcpProjectId ?? Guid.NewGuid();
                var hubProject = await EnsureHubProjectAsync(infobase.ClientId, request.ProjectName.Trim(), ct);
                infobase.ProjectId = hubProject.Id;
                await _infobaseRepository.SaveAsync(infobase, ct);
                instance.ConfigMcpProjectId = newProjectId;
                instance.ConfigMcpDatabaseId = null;
                break;

            default:
                throw new InvalidOperationException($"Неизвестный режим привязки: {request.Mode}.");
        }

        if (request.Mode != ConfigMcpLinkMode.NewProject && !string.IsNullOrWhiteSpace(request.ProjectName))
        {
            var hubProject = await EnsureHubProjectAsync(infobase.ClientId, request.ProjectName.Trim(), ct);
            infobase.ProjectId = hubProject.Id;
            await _infobaseRepository.SaveAsync(infobase, ct);
        }

        await _instanceRepository.SaveAsync(instance, ct);
    }

    public static ConfigMcpLinkRequest BuildLinkRequest(
        ConfigMcpLinkSelection selection,
        string defaultProjectName)
    {
        if (!selection.CreateNewProject && !selection.CreateNewDatabase)
        {
            if (selection.ProjectId is not Guid projectId || projectId == Guid.Empty)
                throw new InvalidOperationException("Не выбран проект config-mcp.");
            if (selection.DatabaseId is not Guid databaseId || databaseId == Guid.Empty)
                throw new InvalidOperationException("Не выбрана database config-mcp.");

            return new ConfigMcpLinkRequest
            {
                Mode = ConfigMcpLinkMode.ExistingDatabase,
                ProjectId = projectId,
                ProjectName = selection.ProjectName,
                DatabaseId = databaseId
            };
        }

        if (!selection.CreateNewProject && selection.CreateNewDatabase)
        {
            if (selection.ProjectId is not Guid projectId || projectId == Guid.Empty)
                throw new InvalidOperationException("Не выбран проект config-mcp.");

            return new ConfigMcpLinkRequest
            {
                Mode = ConfigMcpLinkMode.NewDatabaseInProject,
                ProjectId = projectId,
                ProjectName = selection.ProjectName
            };
        }

        if (string.IsNullOrWhiteSpace(defaultProjectName))
            throw new InvalidOperationException("Не удалось определить имя нового проекта.");

        var projectName = defaultProjectName.Trim();
        if (projectName.Length < 3)
        {
            throw new InvalidOperationException(
                $"Слишком короткое имя нового проекта MCP: «{projectName}». " +
                "Выберите существующий проект (например «Трансгаз») и строку «— Создать новую database —».");
        }

        return new ConfigMcpLinkRequest
        {
            Mode = ConfigMcpLinkMode.NewProject,
            ProjectName = projectName
        };
    }

    public Task<string?> ValidateNewProjectLinkAsync(
        string defaultProjectName,
        Guid instanceId,
        CancellationToken ct = default) =>
        ValidateNewProjectCreationAsync(defaultProjectName, instanceId, ct);

    public async Task<ConfigMcpSyncResult> LinkAndSyncInstanceAsync(
        Guid instanceId,
        ConfigMcpLinkSelection selection,
        string defaultProjectName,
        CancellationToken ct = default)
    {
        if (selection.CreateNewProject)
        {
            var validationError = await ValidateNewProjectCreationAsync(defaultProjectName, instanceId, ct);
            if (validationError is not null)
                return Fail(validationError);
        }

        var request = BuildLinkRequest(selection, defaultProjectName);
        _logger.LogInformation(
            "MCP link: instance={InstanceId}, mode={Mode}, defaultProject={DefaultProject}, " +
            "createNewProject={CreateNewProject}, createNewDatabase={CreateNewDatabase}, " +
            "projectId={ProjectId}, projectName={ProjectName}",
            instanceId,
            request.Mode,
            defaultProjectName,
            selection.CreateNewProject,
            selection.CreateNewDatabase,
            selection.ProjectId,
            selection.ProjectName);

        var before = await _instanceRepository.GetByIdAsync(instanceId, ct);

        HashSet<string>? projectIdsBeforeLink = null;
        string? registryPathForRollback = null;
        try
        {
            registryPathForRollback = await ResolveProjectsJsonPathAsync(ct);
            projectIdsBeforeLink = ConfigMcpProjectsJsonMerger
                .LoadProjectIds(registryPathForRollback)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать projects.json перед привязкой MCP");
        }

        await LinkInstanceAsync(instanceId, request, ct);
        var afterLink = await _instanceRepository.GetByIdAsync(instanceId, ct);
        var linkedProjectId = afterLink?.ConfigMcpProjectId;
        var export = await _configurationService.GetOrCreateCurrentExportAsync(instanceId, ct);

        var result = await SyncInstanceAsync(instanceId, ct);
        if (!result.Success)
        {
            await RestoreInstanceLinkAsync(instanceId, before, ct);
            TryRollbackRegistryProjectAsync(registryPathForRollback, projectIdsBeforeLink, linkedProjectId);
            return result;
        }

        if (!IsLinkRegistrySatisfied(request.Mode, result))
        {
            await RestoreInstanceLinkAsync(instanceId, before, ct);
            TryRollbackRegistryProjectAsync(registryPathForRollback, projectIdsBeforeLink, linkedProjectId);
            return Fail(BuildNoRegistryChangesMessage(request.Mode, result), result);
        }

        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct);
        if (instance is not null && instance.ConfigMcpDatabaseId is null)
        {
            instance.ConfigMcpDatabaseId = export.Id;
            await _instanceRepository.SaveAsync(instance, ct);
        }

        if (result.ChangesSkipped > 0 && result.ChangesCreated + result.ChangesUpdated == 0)
        {
            return new ConfigMcpSyncResult
            {
                Success = true,
                Message = $"Привязка выполнена: запись уже есть в projects.json (пропущено {result.ChangesSkipped}). {result.Message}",
                Warnings = result.Warnings,
                FollowUpHints = result.FollowUpHints,
                ChangesCreated = result.ChangesCreated,
                ChangesUpdated = result.ChangesUpdated,
                ChangesSkipped = result.ChangesSkipped,
                RegistryPath = result.RegistryPath
            };
        }

        return result;
    }

    private async Task RestoreInstanceLinkAsync(
        Guid instanceId,
        ConfigurationInstance? snapshot,
        CancellationToken ct)
    {
        if (snapshot is null)
            return;

        var current = await _instanceRepository.GetByIdAsync(instanceId, ct);
        if (current is null)
            return;

        current.ConfigMcpProjectId = snapshot.ConfigMcpProjectId;
        current.ConfigMcpDatabaseId = snapshot.ConfigMcpDatabaseId;
        await _instanceRepository.SaveAsync(current, ct);
    }

    /// <summary>
    /// R1 migration: copy infobase-level project link to base instance if instance has no link.
    /// </summary>
    public async Task MigrateLegacyInfobaseLinksAsync(CancellationToken ct = default)
    {
        var infobases = await _infobaseRepository.GetAllAsync(ct);
        foreach (var infobase in infobases)
        {
            if (infobase.ConfigMcpProjectId is not Guid legacyProjectId || legacyProjectId == Guid.Empty)
                continue;

            await _configurationService.EnsureBaseInstanceAsync(infobase.Id, ct);
            var instances = await _instanceRepository.GetByInfobaseIdAsync(infobase.Id, ct);
            var baseInstance = instances.FirstOrDefault(i => i.Kind == ConfigurationKind.Base);
            if (baseInstance is null || baseInstance.IsMcpLinked)
                continue;

            baseInstance.ConfigMcpProjectId = legacyProjectId;
            await _instanceRepository.SaveAsync(baseInstance, ct);
        }
    }

    public Task<ToolInstanceProfile> EnsureConfigMcpToolAsync(CancellationToken ct = default) =>
        _registryService.GetOrCreateConfigMcpInstanceAsync(ct);

    public Task SaveConfigMcpRootPathAsync(string rootPath, CancellationToken ct = default) =>
        _registryService.SaveConfigMcpRootPathAsync(rootPath, ct);

    public Task<ConfigMcpStatusResponse> GetStatusAsync(CancellationToken ct = default) =>
        _toolClient.GetStatusAsync(ct);

    private async Task<string> ResolveProjectNameAsync(ConfigurationInstance instance, CancellationToken ct)
    {
        var infobase = await _infobaseRepository.GetByIdAsync(instance.InfobaseId, ct);
        if (infobase?.ProjectId is Guid hubProjectId)
        {
            var hubProject = await _hubProjectRepository.GetByIdAsync(hubProjectId, ct);
            if (hubProject is not null)
                return hubProject.Name;
        }

        try
        {
            var status = await _toolClient.GetStatusAsync(ct);
            var match = status.Projects.FirstOrDefault(p =>
                string.Equals(p.ProjectId, instance.ConfigMcpProjectId?.ToString(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Name;
        }
        catch
        {
            // fall back
        }

        if (infobase is not null)
            return infobase.Name;

        return instance.DisplayName;
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

    private async Task<string> ResolveProjectsJsonPathAsync(CancellationToken ct)
    {
        var tool = await _registryService.GetOrCreateConfigMcpInstanceAsync(ct);
        return ManagedToolRegistryService.ResolveProjectsJsonPath(tool.RootPath);
    }

    /// <summary>
    /// apply-registry идемпотентен: skipped означает совпадение данных, но не отклонение database (см. warnings).
    /// </summary>
    internal static bool IsLinkRegistrySatisfied(ConfigMcpLinkMode mode, ConfigMcpSyncResult result)
    {
        if (result.ChangesCreated + result.ChangesUpdated > 0)
            return true;

        if (result.ChangesSkipped > 0)
        {
            return mode is ConfigMcpLinkMode.NewDatabaseInProject
                or ConfigMcpLinkMode.ExistingDatabase
                or ConfigMcpLinkMode.NewProject;
        }

        return false;
    }

    private static string BuildNoRegistryChangesMessage(ConfigMcpLinkMode mode, ConfigMcpSyncResult result)
    {
        var registryHint = string.IsNullOrWhiteSpace(result.RegistryPath)
            ? string.Empty
            : $" Файл: {result.RegistryPath}.";

        var changesHint = result.ChangesSkipped > 0
            ? $" CLI пропустил {result.ChangesSkipped} записей (данные уже совпадают)."
            : " CLI не сообщил об изменениях (created/updated/skipped = 0).";

        var modeHint = mode switch
        {
            ConfigMcpLinkMode.NewProject =>
                " Проверьте projects.json — возможно проект уже создан ранее.",
            ConfigMcpLinkMode.NewDatabaseInProject =>
                " Проверьте projects.json в выбранном проекте — database с этим export id может уже существовать.",
            _ =>
                " Проверьте путь к portable и выбранную database."
        };

        return "projects.json не изменился:" + changesHint + modeHint + registryHint;
    }

    private static ConfigMcpSyncResult MapResponse(
        ConfigMcpApplyRegistryResponse response,
        int exitCode,
        string? registryPath = null)
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

        var created = response.Changes?.Created ?? 0;
        var updated = response.Changes?.Updated ?? 0;
        var skipped = response.Changes?.Skipped ?? 0;
        var registrySuffix = string.IsNullOrWhiteSpace(registryPath)
            ? string.Empty
            : $" ({registryPath})";

        if (exitCode != 0)
        {
            var exitMessage = response.Errors.Count > 0
                ? string.Join("; ", response.Errors)
                : $"config-mcp apply-registry завершился с кодом {exitCode}{registrySuffix}.";

            return new ConfigMcpSyncResult
            {
                Success = false,
                Message = exitMessage,
                Errors = [exitMessage],
                Warnings = response.Warnings,
                FollowUpHints = followUps,
                ChangesCreated = created,
                ChangesUpdated = updated,
                ChangesSkipped = skipped,
                RegistryPath = registryPath
            };
        }

        if (response.Success)
        {
            var summary = response.Changes is null
                ? $"Синхронизация с config-mcp выполнена{registrySuffix}."
                : $"Синхронизация выполнена{registrySuffix}: создано {created}, обновлено {updated}, пропущено {skipped}.";

            return new ConfigMcpSyncResult
            {
                Success = true,
                Message = summary,
                Warnings = response.Warnings,
                FollowUpHints = followUps,
                ChangesCreated = created,
                ChangesUpdated = updated,
                ChangesSkipped = skipped,
                RegistryPath = registryPath
            };
        }

        var errors = response.Errors.Count > 0
            ? response.Errors
            : [$"config-mcp apply-registry отклонён{registrySuffix}."];

        return new ConfigMcpSyncResult
        {
            Success = false,
            Message = string.Join("; ", errors),
            Errors = errors,
            Warnings = response.Warnings,
            FollowUpHints = followUps,
            ChangesCreated = created,
            ChangesUpdated = updated,
            ChangesSkipped = skipped,
            RegistryPath = registryPath
        };
    }

    private static string FormatFollowUp(ConfigMcpFollowUpOperationDto op)
    {
        var args = op.Args is null || op.Args.Count == 0
            ? string.Empty
            : " " + string.Join(" ", op.Args.Select(kv => $"{kv.Key}={kv.Value}"));

        return $"{op.Command}{args}: {op.Reason}";
    }

    private async Task<string?> ValidateNewProjectCreationAsync(
        string defaultProjectName,
        Guid instanceId,
        CancellationToken ct)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct);
        if (instance is null)
            return "Экземпляр не найден.";

        var infobase = await _infobaseRepository.GetByIdAsync(instance.InfobaseId, ct);
        var client = infobase is not null
            ? await _clientRepository.GetByIdAsync(infobase.ClientId, ct)
            : null;

        if (client is null)
            return null;

        var clientName = client.Name ?? string.Empty;
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(defaultProjectName))
            candidates.Add(defaultProjectName.Trim());
        if (!string.IsNullOrWhiteSpace(clientName))
            candidates.Add(clientName.Trim());
        candidates.Add(client.Id.ToString());

        try
        {
            var registryPath = await ResolveProjectsJsonPathAsync(ct);
            foreach (var project in ConfigMcpProjectsJsonMerger.LoadProjects(registryPath))
            {
                if (string.Equals(project.ClientId, client.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return $"У клиента «{clientName}» уже есть проект в config-mcp («{project.Name}»). " +
                           "Выберите существующий проект и строку «— Создать новую database —».";
                }

                if (candidates.Contains(project.Name))
                {
                    return $"Проект «{project.Name}» уже есть в config-mcp. " +
                           "Выберите существующий проект и строку «— Создать новую database —».";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить projects.json перед созданием проекта MCP");
        }

        try
        {
            var status = await _toolClient.GetStatusAsync(ct);
            foreach (var project in status.Projects)
            {
                if (candidates.Contains(project.Name))
                {
                    return $"Проект «{project.Name}» уже есть в config-mcp. " +
                           "Выберите существующий проект и строку «— Создать новую database —».";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить status config-mcp перед созданием проекта MCP");
        }

        return null;
    }

    private async Task<Guid?> FindExistingMcpProjectIdForClientAsync(Guid clientId, CancellationToken ct)
    {
        try
        {
            var registryPath = await ResolveProjectsJsonPathAsync(ct);
            var match = ConfigMcpProjectsJsonMerger.LoadProjects(registryPath)
                .FirstOrDefault(p => string.Equals(p.ClientId, clientId.ToString(), StringComparison.OrdinalIgnoreCase));

            if (match is not null && Guid.TryParse(match.ProjectId, out var projectId))
                return projectId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось найти существующий проект MCP для clientId {ClientId}", clientId);
        }

        return null;
    }

    private void TryRollbackRegistryProjectAsync(
        string? registryPath,
        HashSet<string>? projectIdsBeforeLink,
        Guid? linkedProjectId)
    {
        if (registryPath is null || projectIdsBeforeLink is null || linkedProjectId is not Guid projectId)
            return;

        if (projectIdsBeforeLink.Contains(projectId.ToString()))
            return;

        if (!_projectsJsonMerger.TryRemoveProjects(registryPath, [projectId.ToString()], out _, out var error))
        {
            if (error is not null)
            {
                _logger.LogWarning(
                    "Не удалось удалить проект MCP {ProjectId} после отката привязки: {Error}",
                    projectId,
                    error);
            }
        }
    }

    private static ConfigMcpSyncResult Fail(string message, ConfigMcpSyncResult? source = null) => new()
    {
        Success = false,
        Message = message,
        Errors = [message],
        Warnings = source?.Warnings ?? [],
        FollowUpHints = source?.FollowUpHints ?? [],
        ChangesCreated = source?.ChangesCreated ?? 0,
        ChangesUpdated = source?.ChangesUpdated ?? 0,
        ChangesSkipped = source?.ChangesSkipped ?? 0,
        RegistryPath = source?.RegistryPath
    };
}
