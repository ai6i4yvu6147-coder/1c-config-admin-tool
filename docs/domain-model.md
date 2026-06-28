# Модель данных и рабочая область (целевое)

**Статус:** design note (продуктовая цель; в коде реализована частично).  
**Интеграция с config-mcp:** согласованный mapping — [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md) (agreed 2026-06-28).

---

## Роль админки

- **Source of truth и control plane** для экосистемы 1С + MCP: клиенты, инфобазы, конфигурации, выгрузки, готовность инструментов.
- Без тяжёлой сущности «проект разработки» — опора на **реальные объекты** и **задачи**.
- **config-mcp `project`** — operational-контейнер индекса (1:1 с Client Hub), не задача разработчика.
- В перспективе — **meta-MCP**: операционный контекст для агента (готовность export/index, `resolve_task_context`, `suggest_next_steps`), не прикладные данные 1С.

---

## Сущности

### Client

Фактический клиент; верхняя группировка инфобаз и доменных шаблонов конфигурации.

### Infobase

Экземпляр базы 1С (prod, dev, ЗУП…): подключение, платформа, план выгрузки, ссылки на MCP (config/data/help), настройки export.

### ConfigurationTemplate

Логический блок кода: основная конфа, расширение оперучёта и т.д. Не привязан к конкретной инфобазе. Портфолио (описания, кейсы, векторный индекс) строится вокруг шаблонов.

### ConfigurationInstance

Связь «какой шаблон установлен в какой инфобазе»: тип (основная/расширение), статус, версия.

### ConfigurationExport

Артефакт выгрузки: `infobase × template × время`.

Поля (целевые): `id`, `infobaseId`, `configurationTemplateId`, `sourcePath`, `sourceKind`, `exportedAt`, `isCurrent`, `contentHash`, статусы export/parse/index.

В fragment config-mcp **`ConfigurationExport.id`** уходит в поле `infobaseId` (database registry id). См. [`registry-mapping.md`](admin-hub/registry-mapping.md).

### Task

Единица работы: багфикс, анализ, адаптация, sync, feature. Привязка: client, infobase, template/instance, тип, статус, `problemSummary`.

Точка входа для пользователя и агента; сюда же — ход работы с ИИ, ссылки на MCP-операции, выжимка для knowledge.

---

## Векторный индекс (опыт, не сырой код)

1. По завершении задачи агент формирует структурированную выжимку (проблема, причина, решение, проверка, риски).
2. `aiSummaryDraft` → правка пользователем → `approvedSummary`.
3. **Knowledge Entry:** embeddings + metadata (`clientId`, `configurationTemplateId`, `taskType`, дата, теги).

SQL-хранилище — задачи, связи, MCP-состояния, операции. Векторный слой — поиск похожих решённых кейсов и портфолио шаблонов.

---

## Meta-MCP (перспектива)

| Операция | Назначение |
|----------|------------|
| `list_clients` / `list_infobases` / `list_configuration_templates` | Где можно работать |
| `get_infobase_state` / `get_tool_readiness` | Свежий export, индекс, help, data-connection |
| `resolve_task_context` | Полный контекст задачи для агента |
| `suggest_next_steps` | «Обнови export», «rebuild index», и т.д. |
| `search_knowledge_by_context` | Похожие кейсы по фильтрам |

---

**В одной фразе:** реестр реальных объектов + задачи как единицы работы + MCP-навигация + векторная память по решённым задачам и шаблонам.
