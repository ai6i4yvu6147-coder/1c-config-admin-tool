## CLI (`configadmin.exe`)

Entry point: `src/ConfigAdmin.Console/Program.cs`.

Сборка:

```powershell
dotnet run --project src/ConfigAdmin.Console -- <command> [options]
# или после build-all-single.bat:
dist\console-single\configadmin.exe <command> [options]
```

### Глобальные опции

| Опция | Описание |
|-------|----------|
| `--db <path>` | Путь к SQLite (default: `%AppData%/ConfigAdmin/configadmin.db`) |
| `--password <secret>` | Мастер-пароль (init-vault, unlock, export с vault) |

### Vault

```powershell
configadmin init-vault --password "YourMasterPassword"
configadmin unlock --password "YourMasterPassword"
```

### Профили

```powershell
configadmin add-client --name ClientA --export-root D:\Exports [--comment "..."]

configadmin add-base `
  --client ClientA `
  --name BaseERP `
  --platform "C:\Program Files\1cv8\8.3.24.0\bin\1cv8.exe" `
  --server "srv01\erp" `
  --user Admin `
  --base-password secret

configadmin list-bases
```

Файловая база: `--file "D:\Bases\MyBase"` вместо `--server`.

### Операции

```powershell
configadmin test-connection --base BaseERP --password "..."
configadmin export --base BaseERP --password "..."
configadmin export-all --password "..."
configadmin list-runs [--base BaseERP] [--limit 20]
```

Exit code: `0` — успех; иначе код ошибки процесса или `1` при failure.

### Admin Hub protocol (planned)

По [`admin-hub/integration.md`](admin-hub/integration.md) будут добавлены:

```powershell
configadmin inventory --json
configadmin status --json
configadmin export-registry --json
configadmin apply-registry --input registry.json --json
```

и JSON-обёртки для `list-bases`, `list-runs`, `test-connection`, `export`.

Сейчас stdout — human-readable text; protocol Phase 1 переведёт machine ops на JSON (stdout) + diagnostics (stderr).

### WPF UI

```powershell
dotnet run --project src/ConfigAdmin.Wpf
```

GUI не заменяет CLI для automation и Admin Hub orchestration.
