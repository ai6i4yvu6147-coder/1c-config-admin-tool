## Admin Hub — документация интеграции

Материалы о роли **ConfigAdmin** в экосистеме 1С AI tooling: managed tool `config-admin` и **реализация Admin Hub** (protocol v1.0.2).

### Канон протокола группы

Общий протокол и registry mapping — **канон группы** в [`../group/shared/`](../group/shared/):

| Документ | Путь (канон) |
|----------|----------------|
| Protocol v1 | [`../group/shared/protocol-v1.md`](../group/shared/protocol-v1.md) |
| Addendum v1.0.1 | [`../group/shared/protocol-v1.0.1-addendum.md`](../group/shared/protocol-v1.0.1-addendum.md) |
| Addendum v1.0.2 | [`../group/shared/protocol-v1.0.2-addendum.md`](../group/shared/protocol-v1.0.2-addendum.md) |
| Addendum v1.0.3 | [`../group/shared/protocol-v1.0.3-addendum.md`](../group/shared/protocol-v1.0.3-addendum.md) |
| Registry mapping | [`../group/shared/registry-mapping.md`](../group/shared/registry-mapping.md) |

Локальные файлы `protocol-v1*.md` и `registry-mapping.md` в этой папке — **указатели** на `shared/` (совместимость ссылок).

### Документы Hub (только этот репозиторий)

| Документ | Содержание |
|----------|------------|
| [`integration.md`](integration.md) | принципы, статус, roadmap, ownership |
| [`registry-mapping-config-mcp-response-2026-06-28.md`](registry-mapping-config-mcp-response-2026-06-28.md) | архив ответа config-mcp |
| [`registry-mapping-hub-response-2026-06-28.md`](registry-mapping-hub-response-2026-06-28.md) | архив ответа Hub |

### Порядок чтения

1. [`integration.md`](integration.md) — что делать в **1c-admin-tool**.
2. [`../group/shared/`](../group/shared/) — контракт для всех модулей группы.

### Связь с backlog

Задачи по протоколу — в [`../todo.md`](../todo.md) (секция Admin Hub protocol).

Карта группы — [`../group/README.md`](../group/README.md).
