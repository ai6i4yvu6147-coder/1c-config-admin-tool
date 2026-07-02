using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Repositories;
using ConfigAdmin.Domain.Security;
using Microsoft.Extensions.Logging;

namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpSyncService
{
    private readonly ManagedToolRegistryService _registryService;
    private readonly IDataMcpSettingsRepository _settingsRepository;
    private readonly IDataConnectionRepository _connectionRepository;
    private readonly DataMcpFragmentBuilder _fragmentBuilder;
    private readonly IDataMcpToolClient _toolClient;
    private readonly ISecretVault _secretVault;
    private readonly ILogger<DataMcpSyncService> _logger;

    public DataMcpSyncService(
        ManagedToolRegistryService registryService,
        IDataMcpSettingsRepository settingsRepository,
        IDataConnectionRepository connectionRepository,
        DataMcpFragmentBuilder fragmentBuilder,
        IDataMcpToolClient toolClient,
        ISecretVault secretVault,
        ILogger<DataMcpSyncService> logger)
    {
        _registryService = registryService;
        _settingsRepository = settingsRepository;
        _connectionRepository = connectionRepository;
        _fragmentBuilder = fragmentBuilder;
        _toolClient = toolClient;
        _secretVault = secretVault;
        _logger = logger;
    }

    public async Task<DataMcpPortableSyncResult> SyncPortableAsync(
        DataMcpPortableSyncOptions options,
        CancellationToken ct = default)
    {
        var tool = await _registryService.GetOrCreateDataMcpInstanceAsync(ct);
        if (!Directory.Exists(tool.RootPath))
        {
            return new DataMcpPortableSyncResult
            {
                Success = true,
                Skipped = true,
                Message = "Portable каталог не найден — синхронизация с data-mcp пропущена."
            };
        }

        var settings = await _settingsRepository.GetByToolInstanceIdAsync(tool.Id, ct);
        if (settings is null)
            return Fail("Настройки data-mcp в Hub не найдены.");

        var connections = await _connectionRepository.GetAllAsync(ct);
        var warnings = new List<string>();
        string? credentialsPath = null;
        var secretsApplied = false;
        string? dmcpPassword = null;

        if (HasS3CredentialInput(options.AccessKeyId, options.SecretAccessKey))
        {
            if (string.IsNullOrWhiteSpace(options.AccessKeyId) || string.IsNullOrWhiteSpace(options.SecretAccessKey))
                return Fail("Укажите оба поля S3: Access Key ID и Secret Access Key.");

            try
            {
                dmcpPassword = ResolveDmcpPassword(options.DmcpPassword, settings.EncryptedDmcpPassword);
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }

            var secretsInput = new DataMcpApplySecretsInput
            {
                AccessKeyId = options.AccessKeyId.Trim(),
                SecretAccessKey = options.SecretAccessKey
            };

            if (!string.IsNullOrWhiteSpace(settings.SealedSecretsPath))
                secretsInput.CredentialsFile = settings.SealedSecretsPath.Trim();

            var (secretsResponse, secretsRaw) = await _toolClient.ApplySecretsAsync(secretsInput, dmcpPassword, ct);
            if (!secretsResponse.Success)
            {
                return Fail(
                    FormatCliErrors("apply-secrets", secretsResponse.Errors, secretsRaw.ExitCode),
                    warnings);
            }

            secretsApplied = true;
            credentialsPath = secretsResponse.CredentialsPath;
            warnings.AddRange(secretsResponse.Warnings);
            _logger.LogInformation("data-mcp apply-secrets OK: {Path}", credentialsPath);
        }

        var fragment = _fragmentBuilder.Build(settings, connections);
        var (registryResponse, registryRaw) = await _toolClient.ApplyRegistryAsync(fragment, ct);
        if (!registryResponse.Success)
        {
            return Fail(
                FormatCliErrors("apply-registry", registryResponse.Errors, registryRaw.ExitCode),
                warnings,
                secretsApplied,
                credentialsPath);
        }

        warnings.AddRange(registryResponse.Warnings);
        _logger.LogInformation(
            "data-mcp apply-registry OK: created={Created}, updated={Updated}, skipped={Skipped}",
            registryResponse.Changes?.Created ?? 0,
            registryResponse.Changes?.Updated ?? 0,
            registryResponse.Changes?.Skipped ?? 0);

        dmcpPassword ??= TryResolveDmcpPassword(options.DmcpPassword, settings.EncryptedDmcpPassword);
        var (validateResponse, validateRaw) = await _toolClient.ValidateConfigAsync(dmcpPassword, ct);
        warnings.AddRange(validateResponse.Checks.Where(c => !c.Ok).Select(c => $"{c.Name}: {c.Message}"));

        if (!validateResponse.Valid)
        {
            return Fail(
                FormatValidateErrors(validateResponse, validateRaw.ExitCode),
                warnings,
                secretsApplied,
                credentialsPath,
                registryApplied: true);
        }

        var message = BuildSuccessMessage(secretsApplied, credentialsPath);
        return new DataMcpPortableSyncResult
        {
            Success = true,
            Message = message,
            SecretsApplied = secretsApplied,
            CredentialsPath = credentialsPath,
            RegistryApplied = true,
            ConfigValid = true,
            Warnings = warnings
        };
    }

    private static bool HasS3CredentialInput(string? accessKeyId, string? secretAccessKey) =>
        !string.IsNullOrWhiteSpace(accessKeyId) || !string.IsNullOrWhiteSpace(secretAccessKey);

    private string ResolveDmcpPassword(string? enteredPassword, byte[]? encryptedPassword)
    {
        if (!string.IsNullOrEmpty(enteredPassword))
            return enteredPassword;

        if (encryptedPassword is { Length: > 0 })
            return _secretVault.Decrypt(encryptedPassword);

        throw new InvalidOperationException("Укажите D-MCP password для записи S3 credentials.");
    }

    private string? TryResolveDmcpPassword(string? enteredPassword, byte[]? encryptedPassword)
    {
        if (!string.IsNullOrEmpty(enteredPassword))
            return enteredPassword;

        if (encryptedPassword is { Length: > 0 })
        {
            try
            {
                return _secretVault.Decrypt(encryptedPassword);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string BuildSuccessMessage(bool secretsApplied, string? credentialsPath)
    {
        if (secretsApplied)
        {
            return string.IsNullOrWhiteSpace(credentialsPath)
                ? "Portable data-mcp обновлён (apply-secrets, apply-registry, validate-config)."
                : $"Portable data-mcp обновлён. Sealed-файл: {credentialsPath}";
        }

        return "Portable data-mcp обновлён (apply-registry, validate-config).";
    }

    private static string FormatCliErrors(string command, IReadOnlyList<string> errors, int exitCode)
    {
        var detail = errors.Count > 0 ? string.Join("; ", errors) : $"exit {exitCode}";
        return $"data-mcp {command} завершился с ошибкой: {detail}";
    }

    private static string FormatValidateErrors(DataMcpValidateConfigResponse response, int exitCode)
    {
        var failed = response.Checks.Where(c => !c.Ok).Select(c => $"{c.Name}: {c.Message}").ToList();
        if (failed.Count > 0)
            return $"data-mcp validate-config: {string.Join("; ", failed)}";

        return $"data-mcp validate-config завершился с кодом {exitCode}.";
    }

    private static DataMcpPortableSyncResult Fail(
        string message,
        IReadOnlyList<string>? warnings = null,
        bool secretsApplied = false,
        string? credentialsPath = null,
        bool registryApplied = false) =>
        new()
        {
            Success = false,
            Message = message,
            SecretsApplied = secretsApplied,
            CredentialsPath = credentialsPath,
            RegistryApplied = registryApplied,
            Warnings = warnings ?? []
        };
}
