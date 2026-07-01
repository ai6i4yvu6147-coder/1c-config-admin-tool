# Shared — group canon 1c-cursor

Shared specifications for Head and Sub (`1c-config-mcp`, `1c-data-mcp`, `1c-help-mcp`).

| Document | Contents |
|----------|----------|
| [`protocol-v1.md`](protocol-v1.md) | Consolidated Protocol v1 |
| [`protocol-v1.0.1-addendum.md`](protocol-v1.0.1-addendum.md) | schemas, discovery, exit codes |
| [`protocol-v1.0.2-addendum.md`](protocol-v1.0.2-addendum.md) | Hub persistence, reconcile, IDs |
| [`protocol-v1.0.3-addendum.md`](protocol-v1.0.3-addendum.md) | UTF-8 JSON CLI encoding (no BOM) |
| [`protocol-v1.0.4-addendum.md`](protocol-v1.0.4-addendum.md) | data-mcp sealed credentials, Hub D-MCP password (merge 2026-07-01) |
| [`registry-mapping.md`](registry-mapping.md) | Hub ↔ config-mcp mapping (agreed 2026-06-28) |

Hub ↔ data-mcp mapping: [`../../admin-hub/registry-mapping-data-mcp.md`](../../admin-hub/registry-mapping-data-mcp.md) (agreed + Sub ack 2026-07-01).

On protocol version conflict: **v1.0.4 > v1.0.3 > v1.0.2 > v1.0.1 > v1**.

Canon edits — Head only; delivery to Sub — packets via `docs/group/outbox/` and operator (see [`../OPERATOR-HANDOFF.md`](../OPERATOR-HANDOFF.md)).

Hub-specific implementation (integration, negotiation archive) — [`../admin-hub/`](../admin-hub/).
