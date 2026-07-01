# Группа: 1c-cursor

Карта подчинённых проектов экосистемы 1С + Cursor. Общий протокол — в `shared/`.

## Протокол группы

| Поле | Значение |
|------|----------|
| protocol_epoch | 0 |
| group_protocol_state | stable |

## Подчинённые

| id | epoch | state | last_ack | Путь |
|----|-------|-------|----------|------|
| `1c-config-mcp` | 0 | stable | 2026-06-30T06:30:05Z | `C:/projects/1c-config-mcp` |
| `1c-data-mcp` | 0 | stable | 2026-06-30T06:46:43Z | `C:/projects/1c-data-mcp` |
| `1c-help-mcp` | 0 | stable | 2026-06-30T06:49:49Z | `C:/projects/1c-help-mcp` |

`state`: `negotiating` | `stable` | `stale` | `defer_manual`

## Общая документация

`shared/` — **канон** общих спек группы (протокол, registry mapping).

- Baseline: skill `export-group-protocol` → snapshot + `protocol_offer`
- Дельты после stable: `emit-group-sync-packet` (`kind: sync_delta`)
- Доставка: `python scripts/sync-relay.py --deliver --repo .`

Hub-специфика реализации Admin Hub — в [`../admin-hub/`](../admin-hub/).

## Inbox от Sub

`inbox/<sub-id>/` — пакеты от подчинённых; обработать через `process-group-inbox` → `doc-librarian` (+ `group-sync-arbitrator` для `protocol_dispute`). Удалить файл после обработки.

## Outbox к Sub

`outbox/<sub-id>/` — исходящие пакеты и snapshot-каталоги. Не коммитить.
