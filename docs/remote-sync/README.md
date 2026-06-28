## Remote Sync — удалённая доставка выгрузок

Документация для **синхронизации конфигураций с RDP-узлов** на локальный Hub (ваш ПК) без ручного копирования через RDP-сессию.

MCP **всегда локально**; на RDP работает только **Передатчик** (чтение XML с диска и доставка).

### Статус

**Phase R-Ping — готово** (register / heartbeat / poll, Tailscale Funnel, E2E с реального RDP).  
**Phase R1 upload — не начато** (chunk transport, sync_jobs, E2E выгрузка).

Подробности: [`status.md`](status.md).

### С чего начать

1. [`status.md`](status.md) — что сделано и что дальше.
2. [`network-setup.md`](network-setup.md) — Tailscale Funnel, скрипты, диагностика DNS на RDP.
3. [`overview.md`](overview.md) — видение продукта, один exe, режимы «Админка» / «Передатчик».
4. [`architecture.md`](architecture.md) — сущности, потоки, связь с Hub и config-mcp.
5. [`transport.md`](transport.md) — протокол HTTPS chunk upload (normative для upload-фазы).
6. [`mvp-spec.md`](mvp-spec.md) — полный E2E MVP: экраны, API, критерии приёмки.
7. [`implementation-plan.md`](implementation-plan.md) — фазы R0–R3, порядок работ.

### Связанные материалы

- [`../admin-hub/integration.md`](../admin-hub/integration.md) — Admin Hub и config-mcp.
- [`../database.md`](../database.md) — NO_DB_MIGRATIONS.
