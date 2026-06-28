# ConfigAdmin — утилита выгрузки конфигураций 1С

Локальная Windows-утилита для профилей баз 1С и выгрузки конфигурации/расширений в XML (`1cv8.exe DESIGNER /DumpConfigToFiles`). По протоколу v1.0.2 — **Admin Hub** экосистемы 1С AI tooling.

## Требования

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Платформа 1С (`1cv8.exe`)

## Быстрый старт

```powershell
git clone https://github.com/ai6i4yvu6147-coder/1c-config-admin-tool.git
cd 1c-config-admin-tool
dotnet build ConfigAdmin.sln
dotnet run --project src/ConfigAdmin.Wpf
```

Self-contained exe:

```powershell
build-all-single.bat   # dist\console-single\configadmin.exe, dist\wpf-single\ConfigAdmin.exe
```

## CLI (кратко)

```powershell
dotnet run --project src/ConfigAdmin.Console -- init-vault --password "..."
dotnet run --project src/ConfigAdmin.Console -- add-client --name ClientA --export-root D:\Exports
dotnet run --project src/ConfigAdmin.Console -- add-base --client ClientA --name BaseERP --platform "C:\...\1cv8.exe" --server "srv01\erp" --user Admin --base-password secret
dotnet run --project src/ConfigAdmin.Console -- export --base BaseERP --password "..."
```

Полный список команд: [`docs/cli.md`](docs/cli.md).

## Документация

| Раздел | Файл |
|--------|------|
| Оглавление | [`docs/README.md`](docs/README.md) |
| Агентам / политики | [`docs/agent-onboarding.md`](docs/agent-onboarding.md) |
| Архитектура | [`docs/architecture.md`](docs/architecture.md) |
| SQLite | [`docs/database.md`](docs/database.md) |
| Backlog | [`docs/todo.md`](docs/todo.md) |
| Admin Hub | [`docs/admin-hub/integration.md`](docs/admin-hub/integration.md) |
| Remote Sync | [`docs/remote-sync/status.md`](docs/remote-sync/status.md) |

Для агентов: [`AGENTS.md`](AGENTS.md).

## Тесты

```powershell
dotnet test tests/ConfigAdmin.Tests
```

## Безопасность

- Мастер-пароль не хранится на диске
- Пароли баз — AES-GCM (ключ Argon2id)
- В логах команд пароль маскируется (`/P"***"`)

## 1С

- [Параметры командной строки](https://its.1c.ru/db/v8317doc/bookmark/adm/TI000000493)
- [/DumpConfigToFiles](https://yellow-erp.com/help/1cv8/zif3_dumpconfigtofiles/?lang=ru)
