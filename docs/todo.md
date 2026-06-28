# Backlog — ConfigAdmin

Живой список задач. Текущее состояние продукта — [README.md](../README.md), архитектура — [architecture.md](architecture.md).

---

## Admin Hub protocol (config-admin + host shell)

Протокол: [admin-hub/integration.md](admin-hub/integration.md). Реализация — по фазам, без big bang.

| Фаза | Задачи | Статус |
|------|--------|--------|
| **Phase 1 (config-mcp)** | Hub tables в SQLite, MCP screen, `apply-registry` sync, post-export sync | **готово** |
| **Phase 1 (config-admin CLI)** | `module.manifest.json`, `inventory --json`, `status --json`, JSON для `list-bases`/`list-runs` | не начато |
| **Phase 2** | ConfigAdmin `export-registry`/`apply-registry`, export locks, JSON export/test-connection | не начато |
| **Phase 3** | help-mcp / data-mcp links, orchestration **`rebuild-index` после export / Remote Sync** (H6; после P0 CLI config-mcp) | не начато |

### Registry mapping (config-mcp) — agreed 2026-06-28

Канон: [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md). Реализация — **registry R2** (код пока R1).

| ID | Задача | Статус |
|----|--------|--------|
| H1 | `config_mcp_project_id` на Client, auto-assign | не начато |
| H2 | Fragment: Client + N databases | не начато |
| H3 | Export: id и path на base + extensions | не начато |
| H4 | Deprecate ручной линк infobase→project | не начато |
| H5 | Документация `registry-mapping.md` | **готово** |
| H6 | Orchestration `rebuild-index` | не начато (config-mcp P0 CLI) |
| H7 | UI: «MCP-контейнер» вместо «MCP Project» | не начато |

---

## Remote Sync (доставка XML с RDP)

Спецификация: [`remote-sync/README.md`](remote-sync/README.md). **Phase R-Ping — готово** (2026-06-28).

| Фаза | Задачи | Статус |
|------|--------|--------|
| **R-Ping** | Schema узлов, receiver, agent UI, Tailscale Funnel, E2E register/heartbeat | **готово** |
| **R1 MVP** | Chunk upload, zip/resume, sync UI, E2E выгрузка | **готово** (manual E2E ✅ 2026-06-28) |
| **R1.x** | Live-прогресс export 1С (file count/size в work dir) | **готово** |
| **R2** | Headless agent, **скорость upload**, cleanup-настройка, **упрощение GUI**, multi-PC Hub | не начато |
| **R3** | Relay VPS, S3, расписание | не начато |

Статус и инструкция по сети: [`remote-sync/status.md`](remote-sync/status.md), [`remote-sync/network-setup.md`](remote-sync/network-setup.md).

---

## Продуктовый backlog

### 0. Remote Sync — UX и скорость (после R1 E2E)

**Сейчас:** E2E работает, но GUI перегружен и неочевиден; upload ~800 MB занимает 30+ мин через Funnel.

- Упростить экраны Hub (Remote-база, tunnel, статус job) и Передатчик (pairing + прогресс на одном экране).
- R2.6: оптимизация скорости — chunk size, метрики, direct Tailscale, параллельные chunks.
- R2.7: настройка «удалять файлы на RDP после успеха» / оставить для отладки.
- **Полный MCP-цикл:** после доставки на Hub — `rebuild-index` (парсинг); R1 = transport only.
- config-mcp: `rebuild-index` по directory `sourcePath` (см. [`admin-hub/integration.md`](admin-hub/integration.md)).

Детали: [`remote-sync/implementation-plan.md`](remote-sync/implementation-plan.md) R2.6–R2.8, [`remote-sync/status.md`](remote-sync/status.md).

### 1. Выгрузка всех баз

**Сейчас:** `export-all` только в консоли.

- Кнопка «Выгрузить все базы» в WPF.
- Прогресс по базе, итоговый отчёт.

### 2. Удаление и сброс данных

**Сейчас:** delete в `ProfileService`, в GUI недоступно.

- Удаление базы/клиента из GUI.
- Сброс профиля (очистка `%AppData%\ConfigAdmin\`).
- Предупреждение: XML на диске не удаляются.

### 3. Дерево клиентов/баз

**Сейчас:** плоский список `Клиент / База`.

- Редактирование клиента.
- UI-дерево: клиент → базы.
- Export root на уровне клиента.

### 4. Журнал выгрузок — **выполнено**

Serilog, `runs/`, meta JSON, экран логов в WPF, `/Out` и `/DumpResult`.

### 5. Формат выгрузки: XML или архив

- Настройка на уровне базы: каталог vs архив.
- Spike CLI 1С для `/DumpCfg` и расширений.

### 6. Автообнаружение расширений

- Список расширений из базы без ручного ввода.
- Исключение патчей 1С по умолчанию.

---

## Вне scope (пока)

- Расписание выгрузок, checksum снимков.
- CI с установленной 1С.
- Миграции существующих `configadmin.db` (политика NO_DB_MIGRATIONS).

---

## Приоритеты

| P | Задача |
|---|--------|
| P0 | ConfigAdmin protocol CLI (Phase 1) |
| P0 | config-mcp integration (Phase 1) — **done** |
| P1 | Remote Sync R2: UX/GUI + скорость upload (после R1 E2E **done**) |
| P1 | Дерево клиентов/баз + редактирование клиента |
| P1 | Удаление и сброс |
| P2 | Выгрузить все базы (GUI) |
| P2 | ConfigAdmin registry sync (Phase 2) |
| P3 | XML vs архив, автообнаружение расширений |
| P3 | help/data MCP + rebuild orchestration (Phase 3) |

*Обновлено: 2026-06-28*

