## Agent onboarding (context for AI)

### Project type (WI canon)

| Field | Value |
|-------|-------|
| Role | **Head (H)** |
| Group | `1c-cursor` (`group.manifest.yaml`) |
| WI canon | **2.4.0** (`canon_version` in manifest) |
| Protocol canon | [`group/shared/`](group/shared/) |
| Sub map | [`group/README.md`](group/README.md) |

Subordinates: `1c-config-mcp`, `1c-data-mcp`, `1c-help-mcp` (paths in manifest).

### Group sync (Head)

| Action | Skill / tool |
|--------|--------------|
| Baseline / ripple | **`sync-base`** |
| Deltas, inbox, dispute / merge / ack | **`sync`** |
| Packet delivery | operator — [`group/OPERATOR-HANDOFF.md`](group/OPERATOR-HANDOFF.md) |

Sync canon: [`canons/group-sync.md`](canons/group-sync.md). Do **not** commit inbox/outbox; delete packets after processing.

### Project overview

**ConfigAdmin** — Windows utility for 1C infobase profiles and exporting main configuration and extensions to XML via `1cv8.exe DESIGNER /DumpConfigToFiles`. WPF UI and headless CLI (`configadmin.exe`).

Per protocol v1 + addenda through v1.0.4 ([`group/shared/`](group/shared/)) this repository is the **Admin Hub implementation** (control plane) and simultaneously a managed tool of type `config-admin`.

### Key policies (do not violate)

- **NO_DB_MIGRATIONS**: do not write migrations or conversions for existing `configadmin.db`. After incompatible schema changes the user recreates the DB (delete `%AppData%\ConfigAdmin\` or `--db` to a new file). Details: [`database.md`](database.md).
- **Minimum invasive unification**: Admin Hub integration — thin CLI/protocol layer over existing core; do not rewrite `ExportOrchestrator`, vault, and OneC adapter for the hub.
- **GUI is not the integration center**: managed tools are invoked via CLI/subprocess; WPF is a UI host, not an MCP button proxy.
- **In-process for self**: ConfigAdmin operations from Hub — via application services; external MCP — subprocess per manifest (protocol v1.0.2 §6; see [`group/shared/`](group/shared/) for current addenda).
- **Secrets**: plain-text passwords in registry sync are forbidden; vault and encrypted blobs are local-owned.
- **Protocol deviation**: deviations from the protocol must be documented explicitly (Deviation, Reason, Impact, Workaround).

### Repository structure

```text
src/
  ConfigAdmin.Domain/           — models, interfaces
  ConfigAdmin.Application/      — scenarios (export, profiles, vault, RemoteSync)
  ConfigAdmin.Infrastructure/   — SQLite, files, DI, SecretVault, RemoteSync repos
  ConfigAdmin.Integration.OneC/ — 1cv8.exe CLI adapter
  ConfigAdmin.Console/          — configadmin.exe
  ConfigAdmin.Wpf/              — ConfigAdmin.exe (GUI, Hub + Sync Agent UI)
tests/ConfigAdmin.Tests/
docs/                           — documentation (this directory)
group.manifest.yaml             — Head of group 1c-cursor
docs/group/shared/              — shared protocol canon (synced with Sub)
setup-tailscale-funnel.ps1      — Remote Sync: initial Funnel setup
start-sync-tunnel.bat           — Remote Sync: start Funnel on :18443
```

Runtime data is **not** in the repository: `%AppData%\ConfigAdmin\` (or override via `CONFIGADMIN_DATA_DIR` / `--db`).

### Quick links

- Architecture: [`architecture.md`](architecture.md)
- CLI: [`cli.md`](cli.md)
- SQLite: [`database.md`](database.md)
- Backlog: [`todo.md`](todo.md)
- Group (H): [`group/README.md`](group/README.md), protocol canon [`group/shared/`](group/shared/), sync [`canons/group-sync.md`](canons/group-sync.md), operator [`group/OPERATOR-HANDOFF.md`](group/OPERATOR-HANDOFF.md)
- Admin Hub (implementation): [`admin-hub/integration.md`](admin-hub/integration.md)
- Remote Sync: [`remote-sync/README.md`](remote-sync/README.md) — **R-Ping done**; status: [`remote-sync/status.md`](remote-sync/status.md)

### .NET build (agents)

The repository pins SDK in [`global.json`](../global.json) (currently **8.0.422**, `rollForward: latestFeature`).

On Windows, developer machines and **Cursor agent shell** often have two different `dotnet.exe` paths:

| Path | Typical contents |
|------|------------------|
| `C:\Program Files\dotnet\dotnet.exe` | Runtime / bootstrapper, **no SDK** |
| `%USERPROFILE%\.dotnet\dotnet.exe` | Installed **.NET SDK** (incl. 8.0.422) |

**False-error symptom:** `dotnet build` → `A compatible .NET SDK was not found` / `No .NET SDKs were found`, although build works in a normal IDE terminal.

**Do not:** blame "normalization removed dependencies" or stop build verification without PATH diagnosis.

**Before concluding "SDK missing"** run:

```powershell
where.exe dotnet
& "$env:USERPROFILE\.dotnet\dotnet.exe" --list-sdks
dotnet --list-sdks
```

If SDK appears only in the second command — **PATH order** issue, not the repository.

**Build from agent shell (PowerShell):**

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
dotnet build
dotnet test tests/ConfigAdmin.Tests
```

Alternative: explicit path `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.

**Incident context (2026-06-30):** agent reported missing SDK; after prioritizing `%USERPROFILE%\.dotnet` build succeeded, revealing real WPF compile errors (unrelated to normalization).

### Tests and config-mcp portable

Unit tests **must not** call real `1c-config-cli` and **must not** write to `C:\1c_config_mcp_server_Portable\projects.json` (or other production portable). Use `FakeConfigMcpToolClient` / temp root under `%TEMP%` (see `ConfigMcpSyncServiceTests`). If an integration test changes the registry — mandatory teardown: remove created `projectId` from `projects.json` in `finally`.
