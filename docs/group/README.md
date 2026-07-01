# Group: 1c-cursor

Map of subordinate projects in the 1C + Cursor ecosystem. Shared protocol — in `shared/`.

## Group protocol

| Field | Value |
|-------|-------|
| protocol_epoch | 0 |
| group_protocol_state | stable |

## Subordinates

| id | epoch | state | last_ack | Path |
|----|-------|-------|----------|------|
| `1c-config-mcp` | 0 | stable | 2026-06-30T06:30:05Z | `C:/projects/1c-config-mcp` |
| `1c-data-mcp` | 0 | stable | 2026-07-01T172000Z | `C:/projects/1c-data-mcp` |
| `1c-help-mcp` | 0 | stable | 2026-06-30T06:49:49Z | `C:/projects/1c-help-mcp` |

`state`: `negotiating` | `stable` | `stale` | `defer_manual`

## Shared documentation

`shared/` — **canon** of group-wide specs (protocol, registry mapping).

- Baseline / ripple: skill **`sync-base`** → `protocol_offer` + snapshot in outbox
- Deltas after stable: skill **`sync`** (`kind: sync_delta`)
- Delivery: operator (see [`OPERATOR-HANDOFF.md`](OPERATOR-HANDOFF.md))

Hub-specific Admin Hub implementation — in [`../admin-hub/`](../admin-hub/).

## Ripple (epoch bump)

When `protocol_epoch` in `shared/` changes for all Subs — `protocol_ripple` + snapshot (`sync-base`) to each lagging Sub. See [`../canons/group-sync.md`](../canons/group-sync.md).

## Inbox from Sub

`inbox/<sub-id>/` — packets from subordinates; process with skill **`sync`**. Delete the file after processing.

## Outbox to Sub

`outbox/<sub-id>/` — outgoing packets and snapshot directories. Do not commit.

## Operator

[`OPERATOR-HANDOFF.md`](OPERATOR-HANDOFF.md) — outbox ↔ inbox copy paths.
