## Agent onboarding (контекст для ИИ)

### Тип проекта (WI canon)

| Поле | Значение |
|------|----------|
| Роль | **Head (H)** |
| Группа | `1c-cursor` (`group.manifest.yaml`) |
| Канон протокола | [`group/shared/`](group/shared/) |
| Карта Sub | [`group/README.md`](group/README.md) |

Подчинённые: `1c-config-mcp`, `1c-data-mcp`, `1c-help-mcp` (пути в manifest).

### Коротко о проекте

**ConfigAdmin** — Windows-утилита для профилей баз 1С и выгрузки основной конфигурации и расширений в XML через `1cv8.exe DESIGNER /DumpConfigToFiles`. Есть WPF UI и headless CLI (`configadmin.exe`).

По протоколу v1.0.2 этот репозиторий — **реализация Admin Hub** (control plane) и одновременно managed tool типа `config-admin`.

### Ключевые политики (не нарушать)

- **NO_DB_MIGRATIONS**: не писать миграции и конвертации существующих `configadmin.db`. После несовместимых изменений схемы пользователь создаёт БД заново (удаление `%AppData%\ConfigAdmin\` или `--db` на новый файл). Подробнее: [`database.md`](database.md).
- **Minimum invasive unification**: интеграция с Admin Hub — thin CLI/protocol layer поверх существующего core; не переписывать `ExportOrchestrator`, vault и OneC adapter ради hub.
- **GUI не центр интеграции**: managed tools вызываются через CLI/subprocess; WPF — UI host, не proxy кнопок MCP.
- **In-process для себя**: операции ConfigAdmin из Hub — через application services; внешние MCP — subprocess по manifest (protocol v1.0.2 §6).
- **Секреты**: plain-text пароли в registry sync запрещены; vault и encrypted blobs — local-owned.
- **Protocol deviation**: отклонения от протокола документировать явно (Deviation, Reason, Impact, Workaround).

### Структура репозитория

```text
src/
  ConfigAdmin.Domain/           — модели, интерфейсы
  ConfigAdmin.Application/      — сценарии (export, profiles, vault, RemoteSync)
  ConfigAdmin.Infrastructure/   — SQLite, файлы, DI, SecretVault, RemoteSync repos
  ConfigAdmin.Integration.OneC/ — 1cv8.exe CLI adapter
  ConfigAdmin.Console/          — configadmin.exe
  ConfigAdmin.Wpf/              — ConfigAdmin.exe (GUI, Hub + Sync Agent UI)
tests/ConfigAdmin.Tests/
docs/                           — документация (этот каталог)
group.manifest.yaml             — Head группы 1c-cursor
docs/group/shared/              — канон общего протокола (синхронизация с Sub)
setup-tailscale-funnel.ps1      — Remote Sync: первичная настройка Funnel
start-sync-tunnel.bat           — Remote Sync: запуск Funnel на :18443
```

Runtime-данные **не** в репозитории: `%AppData%\ConfigAdmin\` (или override через `CONFIGADMIN_DATA_DIR` / `--db`).

### Быстрые ссылки

- Архитектура: [`architecture.md`](architecture.md)
- CLI: [`cli.md`](cli.md)
- SQLite: [`database.md`](database.md)
- Backlog: [`todo.md`](todo.md)
- Группа (H): [`group/README.md`](group/README.md), канон протокола [`group/shared/`](group/shared/)
- Admin Hub (реализация): [`admin-hub/integration.md`](admin-hub/integration.md)
- Remote Sync: [`remote-sync/README.md`](remote-sync/README.md) — **R-Ping готово**; статус: [`remote-sync/status.md`](remote-sync/status.md)

### Сборка .NET (агенты)

Репозиторий закрепляет SDK в [`global.json`](../global.json) (сейчас **8.0.422**, `rollForward: latestFeature`).

На Windows у разработчика и в **shell Cursor-агента** часто два разных `dotnet.exe`:

| Путь | Типичное содержимое |
|------|---------------------|
| `C:\Program Files\dotnet\dotnet.exe` | Runtime / bootstrapper, **без SDK** |
| `%USERPROFILE%\.dotnet\dotnet.exe` | Установленный **.NET SDK** (в т.ч. 8.0.422) |

**Симптом ложной ошибки:** `dotnet build` → `A compatible .NET SDK was not found` / `No .NET SDKs were found`, хотя в обычном терминале IDE сборка проходит.

**Не делать:** не списывать на «нормализацию удалила зависимости», не останавливать проверку сборки без диагностики PATH.

**Перед выводом «SDK нет»** выполнить:

```powershell
where.exe dotnet
& "$env:USERPROFILE\.dotnet\dotnet.exe" --list-sdks
dotnet --list-sdks
```

Если SDK виден только во второй команде — проблема в **порядке PATH**, не в репозитории.

**Сборка из shell агента (PowerShell):**

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
dotnet build
dotnet test tests/ConfigAdmin.Tests
```

Альтернатива: явный путь `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.

**Контекст инцидента (2026-06-30):** агент сообщил об отсутствии SDK; после приоритизации `%USERPROFILE%\.dotnet` сборка прошла, вскрылись реальные ошибки компиляции в WPF (не связанные с нормализацией).
