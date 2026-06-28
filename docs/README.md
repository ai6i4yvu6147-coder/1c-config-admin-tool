## Документация проекта

Структурированный контекст для разработчиков и агентов. Корневой [README.md](../README.md) — быстрый старт; детали — здесь.

### С чего начать (порядок чтения)

1. [`agent-onboarding.md`](agent-onboarding.md) — политики проекта и ключевой контекст.
2. [`todo.md`](todo.md) — backlog и roadmap.
3. [`architecture.md`](architecture.md) — слои решения и поток выгрузки.
4. [`database.md`](database.md) — SQLite, хранилище данных, **NO_DB_MIGRATIONS**.
5. [`cli.md`](cli.md) — команды `configadmin.exe`.
6. [`onec-cli-reference.md`](onec-cli-reference.md) — параметры `1cv8.exe DESIGNER`, используемые при выгрузке.
7. [`remote-sync/README.md`](remote-sync/README.md) — **удалённая доставка с RDP** (R-Ping готово; upload — R1).

### Admin Hub (единая админка экосистемы)

8. [`admin-hub/README.md`](admin-hub/README.md) — оглавление раздела.
9. [`admin-hub/integration.md`](admin-hub/integration.md) — направление разработки **этого репозитория** (ConfigAdmin как host + managed tool).
10. [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md) — согласованный mapping Hub ↔ config-mcp (agreed 2026-06-28).
11. [`domain-model.md`](domain-model.md) — целевая модель сущностей и meta-MCP (design note).
12. Протокол экосистемы (общий для всех модулей):
   - [`admin-hub/protocol-v1.md`](admin-hub/protocol-v1.md)
   - [`admin-hub/protocol-v1.0.1-addendum.md`](admin-hub/protocol-v1.0.1-addendum.md)
   - [`admin-hub/protocol-v1.0.2-addendum.md`](admin-hub/protocol-v1.0.2-addendum.md)

При конфликте между документами протокола: **v1.0.2 > v1.0.1 > v1**.
