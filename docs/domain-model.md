# Модель данных и рабочая область (целевое)

**Статус:** design note; **ConfigurationTemplate / Instance / Export** — в реализации (2026-06).  
**Интеграция с config-mcp:** согласованный mapping — [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md) (agreed 2026-06-28).  
**Интеграция с data-mcp:** согласованный mapping — [`admin-hub/registry-mapping-data-mcp.md`](admin-hub/registry-mapping-data-mcp.md) (agreed + ack 2026-07-01).

---

## Роль админки

- **Source of truth и control plane** для экосистемы 1С + MCP: клиенты, инфобазы, конфигурации, выгрузки, готовность инструментов.
- Без тяжёлой сущности «проект разработки» — опора на **реальные объекты** и **задачи**.
- **config-mcp `project`** — operational-контейнер индекса (1:1 с Client Hub), не задача разработчика.
- В перспективе — **meta-MCP**: операционный контекст для агента (готовность export/index, `resolve_task_context`, `suggest_next_steps`), не прикладные данные 1С.

---

## Сущности

### Client

Фактический клиент; верхняя группировка инфobаз. Не владеет шаблонами конфигураций (шаблоны глобальны для Hub).

### Infobase

Экземпляр базы 1С (prod, dev, ЗУП…): подключение, платформа, ссылки на MCP (config/data/help). **План выгрузки** — через `ConfigurationInstance` на этой базе.

### ConfigurationTemplate

**Глобальный** (кросс-клиентский) логический блок кода — близок по смыслу к «реальному проекту» в портфолио: «Оперативный учёт», «Доработки», «Основная конфигурация».

| Поле | Назначение |
|------|------------|
| `id` | Стабильный UUID |
| `name` | Display name для людей и MCP |
| `kind` | `base` \| `extension` |
| `is_system` | Системный шаблон «Основная конфигурация» (не удаляется) |

Один шаблон может иметь **экземпляры** на базах **разных** клиентов. Редактируется в отдельном окне каталога (редко меняется).

### ConfigurationInstance

Привязка «что выгружать из этой инфobазы». Единый тип для основной конфы, шаблонных и **локальных** расширений.

| Поле | Назначение |
|------|------------|
| `infobase_id` | База 1С |
| `template_id` | **Nullable.** `NULL` = локальное расширение без шаблона |
| `kind` | `base` \| `extension` |
| `display_name` | Имя в UI и config-mcp `database.name` |
| `designer_name` | Имя для `-Extension` в 1С; **null** для `kind=base` |
| `export_enabled` | Включено в план выгрузки |
| `sort_order` | Порядок; `base` всегда первый (`0`) |

**Три варианта instance:**

| Вариант | `template_id` | `designer_name` |
|---------|---------------|-----------------|
| Основная конфа | → системный template `kind=base` | — |
| Шаблонное расширение | → global template | обязательно, per infobase |
| Локальное расширение | `NULL` | обязательно + свой `display_name` |

**Инварианты:** один `base` на инфobазу; `UNIQUE(infobase_id, template_id)` при `template_id NOT NULL`; `UNIQUE(infobase_id, designer_name)` для extensions.

**Единый список выгрузки** = все instances с `export_enabled`, без отдельной галки «основная конфа». **1 instance = 1 вызов 1С = 1 Remote Sync job = 1 config-mcp database.**

### ConfigurationExport

Артефакт выгрузки: `instance × время`.

Поля: `id`, `instanceId`, `sourcePath`, `sourceKind`, `exportedAt`, `isCurrent`, статусы export/parse/index.

В fragment config-mcp **`ConfigurationExport.id`** уходит в поле `infobaseId` (database registry id). См. [`registry-mapping.md`](admin-hub/registry-mapping.md).

### Task

Единица работы: багфикс, анализ, адаптация, sync, feature. Привязка: client, infobase, template/instance, тип, статус, `problemSummary`.

Локальные расширения (`template_id=NULL`) не участвуют в knowledge по `configurationTemplateId`.

---

## UI (целевое)

| Экран | Содержимое |
|-------|------------|
| Главный | Независимые блоки: клиенты, инфobазы, RDP-узлы |
| Шаблоны конфигураций | CRUD глобального каталога (отдельное окно) |
| Настройки инфobазы | Instances: шаблонные + локальные, `display_name` + `designer_name`, галочки export |
| Выгрузка | Запуск по настроенному плану instances |

---

## Векторный индекс (опыт, не сырой код)

1. По завершении задачи агент формирует структурированную выжимку (проблема, причина, решение, проверка, риски).
2. `aiSummaryDraft` → правка пользователем → `approvedSummary`.
3. **Knowledge Entry:** embeddings + metadata (`clientId`, `configurationTemplateId`, `taskType`, дата, теги).

---

## Meta-MCP (перспектива)

| Операция | Назначение |
|----------|------------|
| `list_clients` / `list_infobases` / `list_configuration_templates` | Где можно работать |
| `get_infobase_state` / `get_tool_readiness` | Свежий export, индекс, help, data-connection |
| `resolve_infobase_context` | Полный контекст задачи для агента: C-MCP `projectFilter` / `extensionFilter`, D-MCP `databaseId`, `credentialsState` (**без S3 credentials**) — см. [`admin-hub/registry-mapping-data-mcp.md`](admin-hub/registry-mapping-data-mcp.md) |
| `suggest_next_steps` | «Обнови export», «rebuild index», и т.д. |
| `search_knowledge_by_context` | Похожие кейсы по фильтрам |

---

**В одной фразе:** глобальный каталог шаблонов + instances на инфobазах (в т.ч. локальные расширения) + единый export pipeline + MCP-навигация.
