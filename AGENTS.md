<!-- project-local: не перезаписывать при normalize -->

## Agent hints

Полный контекст проекта — в `docs/`:

- начать с [`docs/agent-onboarding.md`](docs/agent-onboarding.md)
- оглавление: [`docs/README.md`](docs/README.md)
- backlog: [`docs/todo.md`](docs/todo.md)
- Admin Hub (этот репозиторий = Hub + config-admin): [`docs/admin-hub/integration.md`](docs/admin-hub/integration.md)
- mapping Hub ↔ config-mcp (agreed): [`docs/admin-hub/registry-mapping.md`](docs/admin-hub/registry-mapping.md)
- целевая модель данных: [`docs/domain-model.md`](docs/domain-model.md)
- протокол экосистемы: `docs/group/shared/protocol-v1.md` + addendum v1.0.1 + v1.0.2 (канон группы; указатели в `docs/admin-hub/`)

При изменениях схемы SQLite — **NO_DB_MIGRATIONS**: не писать миграции существующих `configadmin.db`. См. [`docs/database.md`](docs/database.md).

При доработках под Admin Hub — следовать `docs/admin-hub/integration.md` и addendum; WPF не использовать как integration API для внешних MCP.

При доработках Remote Sync — [`docs/remote-sync/status.md`](docs/remote-sync/status.md), transport: [`docs/remote-sync/transport.md`](docs/remote-sync/transport.md).

**Сборка .NET в shell агента (Windows):** не считать «SDK не установлен», если `dotnet build` падает с `No .NET SDKs were found` — часто в PATH первым стоит `Program Files\dotnet` без SDK, а SDK лежит в `%USERPROFILE%\.dotnet`. См. [`docs/agent-onboarding.md`](docs/agent-onboarding.md) § «Сборка .NET (агенты)».
