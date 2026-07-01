# Normalize record

- role: head
- group_id: 1c-cursor
- canon_version: 2.2.0
- bundle_version: 2.0.0
- last_apply: 2026-06-30
- method: agent-first (`initiators/head.md`, без `normalize-apply.py`)

## Материализовано

| Элемент | Путь |
|---------|------|
| Каноны WI (локальная копия) | `docs/canons/` (5 файлов) |
| Group manifest | `group.manifest.yaml` |
| Group README | `docs/group/README.md` |
| Shared canon | `docs/group/shared/` (protocol v1 + addenda, registry-mapping) |
| Scripts | `scripts/project-doctor.py`, `sync-relay.py`, `protocol-snapshot.py`, `sync-status.py` |
| Python deps | `requirements.txt` |
| Cursor skills | `.cursor/skills/` (9 skills, H role) |
| Cursor agents | `.cursor/agents/doc-librarian.md`, `group-sync-arbitrator.md` |
| Changelog | `CHANGELOG.md` |

## Не установлено (намеренно)

- `normalize-apply.py`, зеркало `templates/`
- `.cursor/hooks.json`
- Protocol reconcile / `sync-relay --deliver`

## Subordinates

- `1c-config-mcp` → `C:/projects/1c-config-mcp`
- `1c-data-mcp` → `C:/projects/1c-data-mcp`
- `1c-help-mcp` → `C:/projects/1c-help-mcp`

## Проверка

```powershell
python scripts/project-doctor.py --repo . --type H
```
