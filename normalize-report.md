# Report: Head re-normalize (2026-07-02)

Re-normalize per [`initiators/head.md`](C:\projects\Workspace improve\initiators\head.md) (agent-first, canon **2.4.0**).

## Result

| Check | Status |
|-------|--------|
| `python scripts/project-doctor.py --type H` | **OK** |
| Deprecations `normalize.deprecations.yaml` | **no new block for 2.4.0** |
| Agent-cache language migration | **OK** (`agent_docs_lang: en`) |
| Entry-point docs (doc-librarian) | **OK** |

## Removed (deprecations)

Upgrade **2.3.0** → **2.4.0**: no new paths in `normalize.deprecations.yaml`.

Blocks **2.3.0** and **2.2.0** were already applied in the previous cycle.

## Updated

- **Canons** `docs/canons/` — English copy from WI (canon 2.4.0)
- **`group.manifest.yaml`** — `canon_version: "2.4.0"`
- **`scripts/project-doctor.py`** — WI 2.4.0
- **`.cursor/skills/`**, **`.cursor/agents/doc-librarian.md`** — refreshed from WI templates
- **`docs/group/templates/`** — example packets refreshed
- **Agent-cache tier** — 14 files translated to English (README, docs entry points, `group/README`, `group/shared/**`)
- **`docs/normalize-record.md`** — `agent_docs_lang: en`

## Preserved

- **`AGENTS.md`** — `project-local:` marker (not translated)
- **`docs/group/OPERATOR-HANDOFF.md`** — human tier (Russian)
- **`docs/admin-hub/`**, **`docs/remote-sync/`**, domain docs — not in agent-cache tier for this pass
- **`data-mcp-integration-approval-report.md`** — historical archive

## Not installed (intentional)

- `normalize-apply.py`, `templates/` mirror in repo
- `.cursor/hooks.json`

Details: [`docs/normalize-record.md`](docs/normalize-record.md), [`docs/group/README.md`](docs/group/README.md).
