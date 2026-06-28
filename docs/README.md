## Документация проекта

Структурированный контекст для разработчиков и агентов. Корневой [README.md](../README.md) — быстрый старт; детали — здесь.

### С чего начать (порядок чтения)

1. [`agent-onboarding.md`](agent-onboarding.md) — политики проекта и ключевой контекст.
2. [`todo.md`](todo.md) — backlog и roadmap.
3. [`architecture.md`](architecture.md) — слои решения и поток выгрузки.
4. [`database.md`](database.md) — SQLite, хранилище данных, **NO_DB_MIGRATIONS**.
5. [`cli.md`](cli.md) — команды `configadmin.exe`.
6. [`remote-sync/README.md`](remote-sync/README.md) — **удалённая доставка с RDP** (R-Ping готово; upload — R1).

### Admin Hub (единая админка экосистемы)

7. [`admin-hub/README.md`](admin-hub/README.md) — оглавление раздела.
8. [`admin-hub/integration.md`](admin-hub/integration.md) — направление разработки **этого репозитория** (ConfigAdmin как host + managed tool).
9. Протокол экосистемы (общий для всех модулей):
   - [`admin-hub/protocol-v1.md`](admin-hub/protocol-v1.md)
   - [`admin-hub/protocol-v1.0.1-addendum.md`](admin-hub/protocol-v1.0.1-addendum.md)
   - [`admin-hub/protocol-v1.0.2-addendum.md`](admin-hub/protocol-v1.0.2-addendum.md)

При конфликте между документами протокола: **v1.0.2 > v1.0.1 > v1**.
