# ConfigAdmin — 1C configuration export utility

Local Windows utility for 1C infobase profiles and exporting configuration/extensions to XML (`1cv8.exe DESIGNER /DumpConfigToFiles`). Per protocol v1.0.2 — **Admin Hub** of the 1C AI tooling ecosystem.

## Requirements

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 1C platform (`1cv8.exe`)

## Quick start

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

## CLI (brief)

```powershell
dotnet run --project src/ConfigAdmin.Console -- init-vault --password "..."
dotnet run --project src/ConfigAdmin.Console -- add-client --name ClientA --export-root D:\Exports
dotnet run --project src/ConfigAdmin.Console -- add-base --client ClientA --name BaseERP --platform "C:\...\1cv8.exe" --server "srv01\erp" --user Admin --base-password secret
dotnet run --project src/ConfigAdmin.Console -- export --base BaseERP --password "..."
```

Full command list: [`docs/cli.md`](docs/cli.md).

## Documentation

| Section | File |
|---------|------|
| Index | [`docs/README.md`](docs/README.md) |
| Agents / policies | [`docs/agent-onboarding.md`](docs/agent-onboarding.md) |
| Architecture | [`docs/architecture.md`](docs/architecture.md) |
| SQLite | [`docs/database.md`](docs/database.md) |
| Backlog | [`docs/todo.md`](docs/todo.md) |
| Admin Hub | [`docs/admin-hub/integration.md`](docs/admin-hub/integration.md) |
| Remote Sync | [`docs/remote-sync/status.md`](docs/remote-sync/status.md) |

For agents: [`AGENTS.md`](AGENTS.md).

## Tests

```powershell
dotnet test tests/ConfigAdmin.Tests
```

## Security

- Master password is not stored on disk
- Infobase passwords — AES-GCM (Argon2id key)
- Command logs mask passwords (`/P"***"`)

## 1C

- [Command-line parameters](https://its.1c.ru/db/v8317doc/bookmark/adm/TI000000493)
- [/DumpConfigToFiles](https://yellow-erp.com/help/1cv8/zif3_dumpconfigtofiles/?lang=ru)
