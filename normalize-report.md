# Отчёт: нормализация Head (2026-06-30)

Правильная нормализация по [`initiators/head.md`](C:\projects\Workspace improve\initiators\head.md) (agent-first, canon 2.2.0).

## Результат

| Проверка | Статус |
|----------|--------|
| `python scripts/project-doctor.py --type H` | **OK** (0 errors, 0 warnings) |
| `dotnet build` | **OK** (0 errors) |

## Сделано

- **Роль H**, группа `1c-cursor`, три Sub в `group.manifest.yaml`
- `docs/canons/` — локальная копия WI (5 канонов)
- `docs/group/shared/` — канон протокола и registry-mapping
- `docs/admin-hub/` — Hub-спеки; protocol/registry — указатели на `shared/`
- `scripts/` — `project-doctor`, `sync-relay`, `protocol-snapshot`, `sync-status`
- `.cursor/skills/` (9), `.cursor/agents/` (doc-librarian, group-sync-arbitrator)
- `CHANGELOG.md`, `docs/normalize-record.md`
- `AGENTS.md` — маркер `project-local:`

## Не установлено (намеренно)

- `normalize-apply.py`, каталог `templates/` в репо
- `.cursor/hooks.json`
- Protocol reconcile / deliver к Sub

## Отличие от инцидента утром

| Инцидент | Эта нормализация |
|----------|------------------|
| `normalize-apply.py --upgrade-wi` | Агент по чеклисту bundle 2.0.0 |
| Плейсхолдеры `<group-id>` | `1c-cursor` + реальные пути Sub |
| Дубликат протокола без связи | `shared/` = канон, `admin-hub/` = указатели + integration |
| hooks на каждый prompt | без hooks |

## Следующие шаги (вне scope)

1. Нормализовать Sub (`1c-config-mcp`, `1c-data-mcp`, `1c-help-mcp`) ролью Sub.
2. `export-group-protocol` → baseline offer → `sync-relay --deliver`.
3. Sub: `import-group-protocol` → `protocol_ack` → state `stable`.

Детали: [`docs/normalize-record.md`](docs/normalize-record.md), [`docs/group/README.md`](docs/group/README.md).
