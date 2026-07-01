# Normalize record

- role: head
- group_id: 1c-cursor
- canon_version: 2.4.0
- bundle_version: 2.0.0
- agent_docs_lang: en
- last_apply: 2026-07-02
- method: agent-first (`initiators/head.md`, re-normalize 2.4.0)

## Removed (deprecations)

Source: `<WI>/normalize.deprecations.yaml`. Upgrade **2.3.0** → **2.4.0**: no new deprecation block.

| Path | Result |
|------|--------|
| Block **2.4.0** | (none in deprecations.yaml) |
| Block **2.3.0** | already removed (previous apply) |
| Block **2.2.0** | already absent |

## Materialized

| Element | Path |
|---------|------|
| WI canons (local copy, English) | `docs/canons/` (6 files, canon 2.4.0) |
| Group manifest | `group.manifest.yaml` |
| Group README | `docs/group/README.md` |
| Operator handoff | `docs/group/OPERATOR-HANDOFF.md` (human tier, Russian) |
| Group templates | `docs/group/templates/` (3 example files) |
| Group archive | `docs/group/archive/` |
| Shared canon | `docs/group/shared/` (English, agent-cache tier) |
| Scripts | `scripts/project-doctor.py`, `protocol-snapshot.py`, `sync-status.py` |
| Python deps | `requirements.txt` |
| Cursor skills | `.cursor/skills/` — normalize-project, canon-align, maintain-docs, sync, sync-base |
| Cursor agents | `.cursor/agents/doc-librarian.md` |
| Changelog | `CHANGELOG.md` |

## Not installed (intentional)

- `normalize-apply.py`, `templates/` mirror in repo
- `.cursor/hooks.json`

## Subordinates

- `1c-config-mcp` → `C:/projects/1c-config-mcp`
- `1c-data-mcp` → `C:/projects/1c-data-mcp`
- `1c-help-mcp` → `C:/projects/1c-help-mcp`

## Verification

```powershell
python scripts/project-doctor.py --repo . --type H
```
