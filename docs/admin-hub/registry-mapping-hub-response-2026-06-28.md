# Ответ ConfigAdmin / Admin Hub: согласование registry с 1c-config-mcp

**Дата:** 2026-06-28  
**Статус:** **agreed** (ответ config-mcp: OK без возражений, 2026-06-28)  
**Канон:** [`registry-mapping.md`](registry-mapping.md)

**В ответ на:** [ответы config-mcp](registry-mapping-config-mcp-response-2026-06-28.md)

---

## Резюме

**Подтверждаем:** config-mcp `project` — operational-контейнер, **целевой mapping 1:1 с Hub Client** (`clientId` в fragment); config-mcp `database` — **одна выгрузка** (base или extension). **Переименование `project` в config-mcp не планируем и не просим.** Hub `projects` (SQLite) **не материализуется** в `projects.json`. В fragment `infobaseId` = **`ConfigurationExport.id`**, не id подключения к инфобазе 1С. R1 — переходный; целевой fragment — **один project на Client, N databases**, patch после export. H6 (rebuild orchestration) — после P0 CLI config-mcp.

---

## Ответы на вопросы config-mcp (§ «Вопросы к Hub»)

### 1. Hub `project` vs config-mcp `project`

| Сущность | Роль |
|----------|------|
| **Hub Client** | Материализуется в config-mcp как `projects[]` (целевое 1:1) |
| **config-mcp project** | Operational: индексы, `active`, `project_filter` |
| **Hub `projects` (SQLite)** | Внутреннее; нет обязательного аналога в `projects.json` |

Исключение: 2 config-mcp project на одного Client (prod/dev, архив) — явный режим.

### 2. `projectId`

| Поле | Хранение (целевое) |
|------|-------------------|
| `clientId` | `clients.id` |
| `projectId` | `clients.config_mcp_project_id` (отдельный UUID) |
| `infobases.config_mcp_project_id` | deprecated (R1) |

Auto-sync без ручного линка; reconcile по `clientId` при существующем portable project.

### 3. `infobaseId` в fragment

Отдельный id **на каждый export**. Семантика: **database registry id** = `ConfigurationExport.id`. Поле `infobaseId` в JSON сохраняем для совместимости.

### 4. Rename

Не планируем breaking rename на стороне config-mcp. Hub UI: «MCP-контейнер», не «проект разработки».

### 5. Phase 3

Multi-database fragment — Hub параллельно. H6 — после P0 `rebuild-index` CLI.

---

## Таблица терминов

Принята таблица из ответов config-mcp (блок 6) без изменений. См. [`registry-mapping.md`](registry-mapping.md).

---

## Обязательства Hub

| ID | Задача | Фаза |
|----|--------|------|
| H1 | `config_mcp_project_id` на Client | registry R2 |
| H2 | Fragment Client + N databases | registry R2 |
| H3 | Export id/path на base + extensions | export R2 |
| H4 | Deprecate ручной линк | UI |
| H5 | `registry-mapping.md` | **done** |
| H6 | Orchestration rebuild | Phase 3, после CLI |
| H7 | UI «MCP-контейнер» | UI |

---

## Ссылки

| Документ | Путь |
|----------|------|
| Канон | `docs/admin-hub/registry-mapping.md` |
| Ответы config-mcp | `docs/admin-hub/registry-mapping-config-mcp-response-2026-06-28.md` |
| Интеграция | `docs/admin-hub/integration.md` |
| Fragment R1 | `src/ConfigAdmin.Application/Hub/ConfigMcpFragmentBuilder.cs` |
| Концепт | `docs/domain-model.md` |
