## Project documentation

Structured context for developers and agents. Root [README.md](../README.md) — quick start; details — here.

### Where to start (reading order)

1. [`agent-onboarding.md`](agent-onboarding.md) — project policies, **Head (H)** role, group `1c-cursor`.
2. [`todo.md`](todo.md) — backlog and roadmap (**including unprocessed packets in `group/inbox/`**).
3. [`architecture.md`](architecture.md) — solution layers and export flow.
4. [`database.md`](database.md) — SQLite, data storage, **NO_DB_MIGRATIONS**.
5. [`cli.md`](cli.md) — `configadmin.exe` commands.
6. [`onec-cli-reference.md`](onec-cli-reference.md) — `1cv8.exe DESIGNER` parameters used during export.
7. [`remote-sync/README.md`](remote-sync/README.md) — **remote delivery from RDP** (R-Ping done; upload — R1).

### Group 1c-cursor (Head)

8. [`group/README.md`](group/README.md) — Sub map, protocol state, inbox/outbox.
   - Packet processing: skills **`sync`** / **`sync-base`**
   - Delivery: operator — [`group/OPERATOR-HANDOFF.md`](group/OPERATOR-HANDOFF.md)
9. [`group/shared/README.md`](group/shared/README.md) — **canon** of shared protocol and registry mapping.
10. [`canons/group-sync.md`](canons/group-sync.md) — WI canon **2.4.0**: skills **`sync`** / **`sync-base`**, packet types, protocol states.

### Admin Hub (implementation in this repository)

11. [`admin-hub/README.md`](admin-hub/README.md) — integration, negotiation archive.
12. [`admin-hub/integration.md`](admin-hub/integration.md) — roadmap and ConfigAdmin ownership as Hub.
13. [`domain-model.md`](domain-model.md) — target entity model (design note).

Ecosystem protocol (canon for all group modules):

- [`group/shared/protocol-v1.md`](group/shared/protocol-v1.md)
- [`group/shared/protocol-v1.0.1-addendum.md`](group/shared/protocol-v1.0.1-addendum.md)
- [`group/shared/protocol-v1.0.2-addendum.md`](group/shared/protocol-v1.0.2-addendum.md)
- [`group/shared/protocol-v1.0.3-addendum.md`](group/shared/protocol-v1.0.3-addendum.md)
- [`group/shared/protocol-v1.0.4-addendum.md`](group/shared/protocol-v1.0.4-addendum.md)
- [`group/shared/protocol-v1.0.5-addendum.md`](group/shared/protocol-v1.0.5-addendum.md)
- [`group/shared/protocol-v1.0.6-addendum.md`](group/shared/protocol-v1.0.6-addendum.md)
- [`group/shared/registry-mapping.md`](group/shared/registry-mapping.md) (Hub ↔ config-mcp)

Hub ↔ data-mcp mapping (agreed 2026-07-01): [`admin-hub/registry-mapping-data-mcp.md`](admin-hub/registry-mapping-data-mcp.md).

On conflict: **v1.0.6 > v1.0.5 > v1.0.4 > v1.0.3 > v1.0.2 > v1.0.1 > v1**.

Admin-hub pointer stubs (compatibility): [`admin-hub/README.md`](admin-hub/README.md).

WI canons (local copy, **2.4.0**): [`canons/`](canons/). Normalize record: [`normalize-record.md`](normalize-record.md).
