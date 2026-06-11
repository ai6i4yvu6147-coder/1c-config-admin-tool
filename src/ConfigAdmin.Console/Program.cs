using ConfigAdmin.Application;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Infrastructure;
using ConfigAdmin.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;

var dbOption = new Option<string?>("--db", "Путь к файлу SQLite (по умолчанию %AppData%/ConfigAdmin/configadmin.db)");
var passwordOption = new Option<string?>("--password", "Мастер-пароль (init-vault, unlock или авто-разблокировка)");
var nameOption = new Option<string>("--name", "Имя клиента или базы") { IsRequired = true };
var exportRootOption = new Option<string>("--export-root", "Корневой каталог выгрузки");
var commentOption = new Option<string?>("--comment", "Комментарий");
var platformOption = new Option<string>("--platform", "Путь к 1cv8.exe");
var serverOption = new Option<string>("--server", "Строка подключения /S (сервер\\база)");
var fileOption = new Option<string>("--file", "Путь к файловой базе /F");
var userOption = new Option<string?>("--user", "Имя пользователя 1С");
var basePasswordOption = new Option<string?>("--base-password", "Пароль пользователя 1С");
var clientOption = new Option<string>("--client", "Имя клиента");
var baseOption = new Option<string>("--base", "Имя базы");
var limitOption = new Option<int>("--limit", () => 20, "Количество записей журнала");

var rootCommand = new RootCommand("ConfigAdmin — утилита выгрузки конфигураций 1С");
rootCommand.AddGlobalOption(dbOption);
rootCommand.AddGlobalOption(passwordOption);

var initVaultCommand = new Command("init-vault", "Инициализировать хранилище секретов");
initVaultCommand.SetHandler(async (password, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        var vault = sp.GetRequiredService<VaultSessionService>();
        if (await vault.CheckInitializedAsync())
            throw new InvalidOperationException("Хранилище уже инициализировано.");

        if (string.IsNullOrWhiteSpace(password))
            password = ReadSecret("Мастер-пароль: ");

        await vault.InitializeAsync(password!);
        Console.WriteLine("Хранилище инициализировано.");
        Console.WriteLine("Для следующих команд указывайте --password или выполните unlock.");
    });
}, passwordOption, dbOption);

var unlockCommand = new Command("unlock", "Разблокировать хранилище секретов");
unlockCommand.SetHandler(async (password, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        var vault = sp.GetRequiredService<VaultSessionService>();
        if (string.IsNullOrWhiteSpace(password))
            password = ReadSecret("Мастер-пароль: ");

        await vault.UnlockAsync(password!);
        Console.WriteLine("Хранилище разблокировано.");
    });
}, passwordOption, dbOption);

var addClientCommand = new Command("add-client", "Добавить или обновить клиента");
addClientCommand.AddOption(nameOption);
addClientCommand.AddOption(exportRootOption);
addClientCommand.AddOption(commentOption);
addClientCommand.SetHandler(async (name, exportRoot, comment, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
            throw new InvalidOperationException("Укажите --export-root.");

        var profiles = sp.GetRequiredService<ProfileService>();
        var client = await profiles.AddOrUpdateClientAsync(name, exportRoot, comment);
        Console.WriteLine($"Клиент сохранён: {client.Name} ({client.Id})");
    });
}, nameOption, exportRootOption, commentOption, dbOption);

var addBaseCommand = new Command("add-base", "Добавить или обновить профиль базы");
addBaseCommand.AddOption(clientOption);
addBaseCommand.AddOption(nameOption);
addBaseCommand.AddOption(platformOption);
addBaseCommand.AddOption(serverOption);
addBaseCommand.AddOption(fileOption);
addBaseCommand.AddOption(userOption);
addBaseCommand.AddOption(basePasswordOption);
addBaseCommand.SetHandler(async (InvocationContext context) =>
{
    var dbPath = context.ParseResult.GetValueForOption(dbOption);
    var masterPassword = context.ParseResult.GetValueForOption(passwordOption);
    var client = context.ParseResult.GetValueForOption(clientOption);
    var name = context.ParseResult.GetValueForOption(nameOption)!;
    var platform = context.ParseResult.GetValueForOption(platformOption);
    var server = context.ParseResult.GetValueForOption(serverOption);
    var file = context.ParseResult.GetValueForOption(fileOption);
    var user = context.ParseResult.GetValueForOption(userOption);
    var basePassword = context.ParseResult.GetValueForOption(basePasswordOption);

    await RunAsync(dbPath, async sp =>
    {
        await EnsureVaultUnlockedAsync(sp, masterPassword);

        if (string.IsNullOrWhiteSpace(client))
            throw new InvalidOperationException("Укажите --client.");
        if (string.IsNullOrWhiteSpace(platform))
            throw new InvalidOperationException("Укажите --platform.");

        ConnectionType connectionType;
        string connectionString;
        if (!string.IsNullOrWhiteSpace(server))
        {
            connectionType = ConnectionType.Server;
            connectionString = server;
        }
        else if (!string.IsNullOrWhiteSpace(file))
        {
            connectionType = ConnectionType.File;
            connectionString = file;
        }
        else
        {
            throw new InvalidOperationException("Укажите --server или --file.");
        }

        var profiles = sp.GetRequiredService<ProfileService>();
        var profile = await profiles.AddOrUpdateInfobaseAsync(
            client,
            name,
            platform,
            connectionType,
            connectionString,
            user,
            basePassword);

        Console.WriteLine($"База сохранена: {profile.Name} ({profile.Id})");
    });
});

var testConnectionCommand = new Command("test-connection", "Проверить подключение к базе");
testConnectionCommand.AddOption(baseOption);
testConnectionCommand.SetHandler(async (baseName, password, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        await EnsureVaultUnlockedAsync(sp, password);
        if (string.IsNullOrWhiteSpace(baseName))
            throw new InvalidOperationException("Укажите --base.");

        var tester = sp.GetRequiredService<ConnectionTestService>();
        var result = await tester.TestByNameAsync(baseName);
        Console.WriteLine(result.CommandLineMasked);
        Console.WriteLine($"Код возврата: {result.ExitCode}");
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            Console.WriteLine(result.StandardOutput);
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            Console.WriteLine(result.StandardError);

        if (!result.Success)
            Environment.ExitCode = result.ExitCode == 0 ? 1 : result.ExitCode;
    });
}, baseOption, passwordOption, dbOption);

var exportCommand = new Command("export", "Выгрузить одну базу");
exportCommand.AddOption(baseOption);
exportCommand.SetHandler(async (baseName, password, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        await EnsureVaultUnlockedAsync(sp, password);
        if (string.IsNullOrWhiteSpace(baseName))
            throw new InvalidOperationException("Укажите --base.");

        var exporter = sp.GetRequiredService<ExportService>();
        var progress = new Progress<ConfigAdmin.Domain.Models.ExportProgress>(p =>
            Console.WriteLine($"[{p.CompletedSteps}/{p.TotalSteps}] {p.Stage} {p.Detail}".Trim()));

        var result = await exporter.ExportByNameAsync(baseName, progress: progress);
        PrintExportResult(result);
    });
}, baseOption, passwordOption, dbOption);

var exportAllCommand = new Command("export-all", "Выгрузить все базы");
exportAllCommand.SetHandler(async (password, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        await EnsureVaultUnlockedAsync(sp, password);
        var exporter = sp.GetRequiredService<ExportService>();
        var results = await exporter.ExportAllAsync();
        foreach (var result in results)
            PrintExportResult(result);
    });
}, passwordOption, dbOption);

var listRunsCommand = new Command("list-runs", "Показать журнал выгрузок");
listRunsCommand.AddOption(baseOption);
listRunsCommand.AddOption(limitOption);
listRunsCommand.SetHandler(async (baseName, limit, dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        var query = sp.GetRequiredService<ExportRunQueryService>();
        var runs = string.IsNullOrWhiteSpace(baseName)
            ? await query.GetAllAsync(limit)
            : await query.GetByBaseNameAsync(baseName!, limit);

        foreach (var run in runs)
        {
            Console.WriteLine($"{run.StartedAt:u} | success={run.Success} | exit={run.ExitCode} | {run.OutputPath}");
            if (!string.IsNullOrWhiteSpace(run.ErrorMessage))
                Console.WriteLine($"  error: {run.ErrorMessage}");
        }
    });
}, baseOption, limitOption, dbOption);

var listBasesCommand = new Command("list-bases", "Показать список баз");
listBasesCommand.SetHandler(async (dbPath) =>
{
    await RunAsync(dbPath, async sp =>
    {
        var profiles = sp.GetRequiredService<ProfileService>();
        var bases = await profiles.GetInfobasesAsync();
        foreach (var profile in bases)
            Console.WriteLine($"{profile.Name} | {profile.ConnectionType} | last={profile.LastExportStatus}");
    });
}, dbOption);

rootCommand.AddCommand(initVaultCommand);
rootCommand.AddCommand(unlockCommand);
rootCommand.AddCommand(addClientCommand);
rootCommand.AddCommand(addBaseCommand);
rootCommand.AddCommand(testConnectionCommand);
rootCommand.AddCommand(exportCommand);
rootCommand.AddCommand(exportAllCommand);
rootCommand.AddCommand(listRunsCommand);
rootCommand.AddCommand(listBasesCommand);

return await rootCommand.InvokeAsync(args);

static async Task RunAsync(string? dbPath, Func<IServiceProvider, Task> action)
{
    LoggingSetup.Configure();

    var host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging => logging.ClearProviders())
        .ConfigureServices(services =>
        {
            services.AddConfigAdminApplication(dbPath);
        })
        .Build();

    await host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    await action(host.Services);
}

static async Task EnsureVaultUnlockedAsync(IServiceProvider sp, string? masterPassword)
{
    var vault = sp.GetRequiredService<VaultSessionService>();
    if (vault.IsUnlocked)
        return;

    if (!string.IsNullOrWhiteSpace(masterPassword))
    {
        await vault.UnlockAsync(masterPassword);
        return;
    }

    throw new InvalidOperationException(
        "Хранилище заблокировано. Укажите --password \"мастер-пароль\" или выполните unlock.");
}

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    var secret = string.Empty;
    ConsoleKeyInfo key;
    do
    {
        key = Console.ReadKey(intercept: true);
        if (key.Key is ConsoleKey.Backspace)
        {
            if (secret.Length > 0)
                secret = secret[..^1];
            continue;
        }
        if (!char.IsControl(key.KeyChar))
            secret += key.KeyChar;
    } while (key.Key != ConsoleKey.Enter);

    Console.WriteLine();
    return secret;
}

static void PrintExportResult(ConfigAdmin.Domain.Models.ExportResult result)
{
    Console.WriteLine($"success={result.Success} exit={result.ExitCode} duration={result.Duration}");
    if (!string.IsNullOrWhiteSpace(result.OutputPath))
        Console.WriteLine($"output: {result.OutputPath}");
    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        Console.WriteLine($"error: {result.ErrorMessage}");

    foreach (var step in result.Steps)
        Console.WriteLine($"  step {step.StepName}: success={step.Success} exit={step.ExitCode}");
}
