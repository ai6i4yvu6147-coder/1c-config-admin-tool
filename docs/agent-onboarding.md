## Agent onboarding (контекст для ИИ)

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
setup-tailscale-funnel.ps1      — Remote Sync: первичная настройка Funnel
start-sync-tunnel.bat           — Remote Sync: запуск Funnel на :18443
```

Runtime-данные **не** в репозитории: `%AppData%\ConfigAdmin\` (или override через `CONFIGADMIN_DATA_DIR` / `--db`).

### Быстрые ссылки

- Архитектура: [`architecture.md`](architecture.md)
- CLI: [`cli.md`](cli.md)
- SQLite: [`database.md`](database.md)
- Backlog: [`todo.md`](todo.md)
- Admin Hub (MCP instance-level + H6): [`admin-hub/integration.md`](admin-hub/integration.md) — **E2E расширения ✅ 2026-06-30**
- Remote Sync: [`remote-sync/README.md`](remote-sync/README.md) — **R-Ping готово**; статус: [`remote-sync/status.md`](remote-sync/status.md)
