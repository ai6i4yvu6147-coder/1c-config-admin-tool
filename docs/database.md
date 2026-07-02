## База данных (SQLite)

### Расположение

По умолчанию: `%AppData%\ConfigAdmin\configadmin.db`.

Переопределение:
- CLI: `--db <path>`
- env: `CONFIGADMIN_DATA_DIR` (каталог; файл `configadmin.db` внутри) — normative в protocol v1.0.2

Инициализация: `DatabaseInitializer` при первом запуске (`CREATE TABLE IF NOT EXISTS ...`).

### Главное правило: NO_DB_MIGRATIONS

**Запрет — на конвертацию данных** уже развёрнутых `configadmin.db`: нет скриптов upgrade, backfill и автоматического переноса между версиями схемы.

Проект в активной разработке: **схему SQLite меняем свободно** через актуальный bootstrap в `DatabaseInitializer` (`CREATE TABLE IF NOT EXISTS`, при необходимости `ALTER TABLE ADD COLUMN` для совместимости с уже созданными dev-БД).

При несовместимых изменениях persistence пользователь может **создать БД заново** (удаление `%AppData%\ConfigAdmin\` или новый `--db`). Выгруженные XML на диске не затрагиваются.

### Таблицы (текущая схема)

| Таблица | Назначение |
|---------|------------|
| `clients` | Клиент: id, name, comment, export_root_path |
| `projects` | Hub-проект: id, client_id, name, active |
| `tool_instances` | Managed tool: module_id (unique), root_path, enabled |
| `data_mcp_settings` | D-MCP global bucket profile per tool_instance: endpoint, region, bucket, default_prefix, sealed_secrets_path, encrypted_dmcp_password |
| `data_connections` | D-MCP pairing: id (dataConnectionId), infobase_id (unique), databaseid, display_name |
| `infobases` | База 1С: подключение, platform_path, план выгрузки, last_export_*, project_id, config_mcp_project_id (legacy R1) |
| `configuration_instances` | План выгрузки per infobase; MCP: `config_mcp_project_id`, `config_mcp_database_id` |
| `configuration_exports` | Артефакт выгрузки (instance × время); id → `infobaseId` в config-mcp fragment |
| `export_runs` | Журнал запусков выгрузки (включая meta_json_path) |
| `vault_meta` | Salt и verifier мастер-пароля (не сам пароль) |
| `remote_nodes` | RDP-узел: pairing verifier, hub URL, last_seen, enabled |
| `agent_sessions` | Bearer token hash для активных сессий Передатчика |

`sync_jobs` и поля Remote на `infobases` — **запланированы**, см. [`remote-sync/status.md`](remote-sync/status.md).

`tool_instances` seed при первом открытии экрана MCP: `module_id=1c-config-mcp`, `root_path=C:\1c_config_mcp_server_Portable`.

Согласованный mapping Hub ↔ config-mcp (`project`, `database`, fragment): [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md).

Код: `src/ConfigAdmin.Infrastructure/Data/DatabaseInitializer.cs`, репозитории в `Infrastructure/Repositories/`.

### Vault и пароли баз

- Мастер-пароль **не** хранится на диске.
- Пароли баз — AES-GCM blob в `infobases.encrypted_password`, ключ от Argon2id (`SecretVault`).
- Разблокировка — in-memory на время сессии процесса.

### Журнал выгрузок

- SQLite: `export_runs` — индекс для UI/CLI.
- Файлы: `%AppData%\ConfigAdmin\runs\{Client}\{Base}\{runId}\` — meta JSON, out.log, dumpresult per step.
