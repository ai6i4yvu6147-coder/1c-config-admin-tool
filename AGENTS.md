## Agent hints

Полный контекст проекта — в `docs/`:

- начать с [`docs/agent-onboarding.md`](docs/agent-onboarding.md)
- оглавление: [`docs/README.md`](docs/README.md)
- backlog: [`docs/todo.md`](docs/todo.md)
- Admin Hub (этот репозиторий = Hub + config-admin): [`docs/admin-hub/integration.md`](docs/admin-hub/integration.md)
- протокол экосистемы: `docs/admin-hub/protocol-v1.md` + addendum v1.0.1 + v1.0.2

При изменениях схемы SQLite — **NO_DB_MIGRATIONS**: не писать миграции существующих `configadmin.db`. См. [`docs/database.md`](docs/database.md) и [`.cursor/rules/no-db-migrations.md`](.cursor/rules/no-db-migrations.md).

При доработках под Admin Hub — следовать `docs/admin-hub/integration.md` и addendum; WPF не использовать как integration API для внешних MCP.

При доработках Remote Sync — [`docs/remote-sync/status.md`](docs/remote-sync/status.md), transport: [`docs/remote-sync/transport.md`](docs/remote-sync/transport.md).
