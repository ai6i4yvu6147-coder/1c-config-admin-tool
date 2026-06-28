## Admin Hub — документация интеграции

Материалы о роли **ConfigAdmin** в экосистеме 1С AI tooling: managed tool `config-admin` и **реализация Admin Hub** (protocol v1.0.2).

### Документы

| Документ | Для кого | Содержание |
|----------|----------|------------|
| [`integration.md`](integration.md) | разработчики **этого репо** | принципы, статус, roadmap, ownership |
| [`registry-mapping.md`](registry-mapping.md) | Hub + config-mcp | **согласованный** mapping registry (agreed 2026-06-28) |
| [`protocol-v1.md`](protocol-v1.md) | все модули | Consolidated Protocol v1 |
| [`protocol-v1.0.1-addendum.md`](protocol-v1.0.1-addendum.md) | все модули | schemas, discovery, exit codes, sync |
| [`protocol-v1.0.2-addendum.md`](protocol-v1.0.2-addendum.md) | все модули | Hub persistence, reconcile, IDs, workflows |

При конфликте: **v1.0.2 > v1.0.1 > v1**.

### Порядок чтения

1. `integration.md` — что делать в **1c-admin-tool**.
2. Протокол v1 + addendum — полный контракт при реализации CLI/manifest/sync.

### Связь с backlog

Задачи по протоколу — в [`../todo.md`](../todo.md) (секция Admin Hub protocol).
