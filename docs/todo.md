# Backlog — ConfigAdmin

Living task list. Current product state — [README.md](../README.md), architecture — [architecture.md](architecture.md).

---

## Admin Hub protocol (config-admin + host shell)

Protocol: [admin-hub/integration.md](admin-hub/integration.md). Implementation — phased, no big bang.

| Phase | Tasks | Status |
|-------|-------|--------|
| **Phase 1 (config-mcp)** | Hub tables in SQLite, MCP screen, `apply-registry` sync, post-export sync | **done** |
| **Phase 1 (config-admin CLI)** | `module.manifest.json`, `inventory --json`, `status --json`, JSON for `list-bases`/`list-runs` | not started |
| **Phase 2** | ConfigAdmin `export-registry`/`apply-registry`, export locks, JSON export/test-connection | not started |
| **Phase 3 (config-mcp CLI)** | `rebuild-index`, `rebuild-all`, `reconcile-markers`, `apply-registry --trigger-rebuild`, `indexReadiness` in status | **done** (2026-06-29) |
| **Phase 3 (Hub H6)** | orchestration **`rebuild-index` after export / Remote Sync** | **done** (2026-06-30, E2E ✅) |

### Instance-level MCP — E2E verified (2026-06-30)

**Scenario (manual check):** configure infobase and extensions in Hub → MCP screen: link to existing project + "Create new database" (before first export, planned `sourcePath`) → local extension export → post-export sync → `apply-registry` + automatic `rebuild-index` → extension visible in MCP.

| Step | Component | Status |
|------|-----------|--------|
| Link before export | `ConfigMcpProjectsJsonMerger` (planned path in `projects.json`) | ✅ |
| Post-export sync | `ConfigMcpSyncService.ApplyFragmentAsync` → `apply-registry` | ✅ |
| Index `.db` | H6: `rebuild-index` via `followUpOperations` / fallback by on-disk `sourcePath` | ✅ |
| `projects.json` encoding | `UnsafeRelaxedJsonEscaping` + UTF-8 without BOM in merger | ✅ |

Code: `ConfigMcpSyncService`, `ConfigMcpProjectsJsonMerger`, `ConfigMcpFragmentBuilder`, `ConfigMcpViewModel`, `ExportViewModel.TrySyncToMcpAsync`.

Regression checklist: [`admin-hub/integration.md`](admin-hub/integration.md) § Reference workflow.

### Registry mapping (config-mcp) — agreed 2026-06-28

Canon: [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md). Implementation — **registry R2** (code still R1).

| ID | Task | Status |
|----|------|--------|
| H1 | `config_mcp_project_id` on Client, auto-assign | not started |
| H2 | Fragment: Client + N databases | not started |
| H3 | Export: id and path on base + extensions | not started |
| H4 | Deprecate manual infobase→project link | not started |
| H5 | Documentation `registry-mapping.md` | **done** |
| H6 | Orchestration `rebuild-index` (apply → followUpOperations → subprocess) | **done** (2026-06-30, E2E ✅) |
| H7 | UI: "MCP container" instead of "MCP Project" | not started |

### Registry mapping (data-mcp) — agreed + ack 2026-07-01

Canon: [`admin-hub/registry-mapping-data-mcp.md`](admin-hub/registry-mapping-data-mcp.md), protocol [`group/shared/protocol-v1.0.4-addendum.md`](group/shared/protocol-v1.0.4-addendum.md). Hub Phase 1 tasks **ready** (code not started).

| ID | Task | Status |
|----|------|--------|
| D-H6 | Mapping doc + merge + Sub ack | **done** (2026-07-01) |
| D-H1 | SQLite schema | **ready** |
| D-H2 | WPF D-MCP settings | **ready** |
| D-H3 | Sealed file R/W + test vector | **ready** |
| D-H4 | DataMcpSyncService | **ready** |
| D-H5 | `resolve_infobase_context` | **ready** |

---

## Remote Sync (XML delivery from RDP)

Specification: [`remote-sync/README.md`](remote-sync/README.md). **Phase R-Ping — done** (2026-06-28).

| Phase | Tasks | Status |
|-------|-------|--------|
| **R-Ping** | Node schema, receiver, agent UI, Tailscale Funnel, E2E register/heartbeat | **done** |
| **R1 MVP** | Chunk upload, zip/resume, sync UI, E2E export | **done** (manual E2E ✅ 2026-06-28) |
| **R1.x** | Live 1C export progress (file count/size in work dir) | **done** |
| **R2** | Headless agent, **upload speed**, cleanup settings, **GUI simplification**, multi-PC Hub | not started |
| **R3** | Relay VPS, S3, scheduling | not started |

Status and network setup: [`remote-sync/status.md`](remote-sync/status.md), [`remote-sync/network-setup.md`](remote-sync/network-setup.md).

---

## Product backlog

### 0. Remote Sync — UX and speed (after R1 E2E)

**Now:** E2E works, but GUI is overloaded and unclear; upload ~800 MB takes 30+ min via Funnel.

- Simplify Hub screens (Remote infobase, tunnel, job status) and Relay (pairing + progress on one screen).
  - **partial (2026-07-01):** `HubSettingsView` (listen URL separate from Tailscale URL for RDP); shell "← Back (Esc)" instead of duplicate buttons on child screens; shared styles; batch refresh of infobase list.
- R2.6: speed optimization — chunk size, direct Tailscale, parallel chunks.
- R2.10: **estimate and display upload speed on Hub** — MB/s (rolling average over sent chunks), ETA by `uploaded/total`; Relay UI and Hub job card.
- R2.7: cleanup on RDP — **two buttons** on Relay (B4) — **done** (2026-06-30): `AgentDataCleanupService`, `SyncAgentView`.
- **Full MCP cycle (H6 / R2.9):** **done** (2026-06-30) — Hub orchestration `rebuild-index` after export / sync. See [`admin-hub/integration.md`](admin-hub/integration.md) § Reference workflow.

Details: [`remote-sync/implementation-plan.md`](remote-sync/implementation-plan.md) R2.6–R2.10, [`remote-sync/status.md`](remote-sync/status.md).

### 1. Export all infobases

**Now:** `export-all` only in console.

- "Export all infobases" button in WPF.
- Per-infobase progress, summary report.

### 2. Data deletion and reset

**Now:** delete in `ProfileService`, not available in GUI.

- Delete infobase/client from GUI.
- Profile reset (clear `%AppData%\ConfigAdmin\`).
- Warning: on-disk XML is not deleted.

### 3. Export plan on Export screen (B3) — **done (2026-06-30)**

**Was:** plan read-only; `ExportEnabled` only on infobase card.

**Done:** "Export" checkboxes in `ExportView` grid — **single run only**; `configuration_instances.export_enabled` in SQLite **not changed**. Session override in `ExportViewModel` → `ExportOrchestrator` and `RemoteSyncOrchestrator.RequestSyncAsync(planOverride)`.

- [x] Checkboxes in plan grid on Export screen
- [x] Filter export plan / remote jobs by session override
- [x] UI hint: changes are not saved to infobase settings

### 4. Client/infobase tree

**Now:** flat list `Client / Infobase`.

- Edit client.
- UI tree: client → infobases.
- Export root at client level.

### 5. Export journal — **done**

Serilog, `runs/`, meta JSON, log screen in WPF, `/Out` and `/DumpResult`.

### 6. Export format: XML or archive

- Per-infobase setting: directory vs archive.
- Spike 1C CLI for `/DumpCfg` and extensions.

### 7. Unified UI event journal — **partial (2026-06-29)**

**Now:** "Events" tab in journal (`UiActivityLog`), MCP errors written to memory + `%AppData%\ConfigAdmin\logs\`; copyable text on MCP screen.

**Target:** unified `IUiNotificationService` for all screens, auto-navigate to journal on ERROR, duplicate to Serilog, optional toast; remove "broken" MCP projects from UI (`apply-registry` remove / Admin GUI config-mcp).

- [x] `UiActivityLog` + "Events" tab
- [x] MCP → journal + copyable status
- [ ] Wire Export, Remote Sync, BaseEdit and other screens
- [ ] UI to remove project/database in portable (see B1)

### 8. ConfigurationTemplate / Instance — **in progress**

Canon: [`domain-model.md`](domain-model.md). Global templates + instances on infobase (template and local extensions); unified export plan; Remote 1 job = 1 instance.

- [x] Design note and SQLite schema
- [x] Instance-level MCP link + post-export sync (E2E 2026-06-30)
- [ ] WPF: template catalog, instance editor on infobase
- [ ] Export / Remote Sync / MCP fragment (H3 partial)

**Follow-up:** auto-discover extensions from 1C → suggest instance.

---

## Known bugs and UX gaps

*Updated: 2026-07-01 (B2, B3, B4 closed; B1 — partial protection; WPF UI review — 2026-07-01).*

### Closed (2026-06-30)

| ID | Symptom | Fix |
|----|---------|-----|
| **B2** | Hub **crashes** on reopening infobase card | **fixed:** singleton `BaseEditViewModel` + detached WPF bindings on `Clients.Clear()`. `PrepareEditAsync`/`PrepareCreateAsync` before navigation; `_suppressClientChange` during prepare; reuse `BaseEditView` + `DataContext = null` on leave (`NavigationService`); `DispatcherUnhandledException` in `App.xaml.cs`. |
| **B3** | Export plan read-only on Export screen | **fixed:** see §3 above. |
| **B4** | Sync artifacts on RDP after job | **fixed:** two buttons on Relay; `AgentDataCleanupService`; confirmation; active job untouched. |

### WPF UI review — closed (2026-07-01)

| Topic | Fix |
|-------|-----|
| Vault lock — stale master password | `VaultViewModel.ResetForLock()` before `SetRoot<VaultViewModel>` |
| Client rename → duplicate | `ProfileService.AddOrUpdateClientAsync(..., clientId)` |
| Test connection resets export settings | `ConnectionTestService.TestDraftAsync` — no persist until Save |
| `ExportViewModel.Begin` async void | `BeginAsync` + await in `MainViewModel` |
| ConfigMcp double refresh | `_initialized` in `RefreshOnNavigateAsync`; removed `OnLoaded` |
| Zombie ConfigAdmin.exe after close | graceful shutdown + `Environment.Exit` only on timeout |
| N+1 on infobase list refresh | `GetExportSummariesForAllBasesAsync()` |

Code: `NavigationService`, `PasswordBoxBindingBehavior`, `BusyViewModelBase`, `HubSettingsView`, `App.xaml.cs`.

### Open

| ID | Symptom | Context | Priority |
|----|---------|---------|----------|
| **B1** | Junk projects "Р" / "P" in `projects.json` | Partial protection 2026-06-30 (log, duplicate, default UI). Delete UI — open (§7) | P1 |

### B4 — Relay cleanup (R2.7, closed)

**Two buttons** (confirmation + result in Relay log):

| Button | Scope | Do not touch |
|--------|-------|--------------|
| **Clear job directories** | `%AppData%\ConfigAdmin\agent\work\` (all `{jobId}\`), optionally `agent\resume\` | active job (export/upload in progress) |
| **Full software trace cleanup** | entire `%AppData%\ConfigAdmin\` on this machine: `agent\` (work, resume, `settings.json`), `logs\`, local `configadmin.db` if present | Hub `ExportRoot` on Hub PC; RDP usually has no Hub exports |

Code: `AgentDataCleanupService`, `SyncAgentViewModel`, `AgentSettingsStore`; per-job — `SyncAgentJobProcessor.TryCleanupWorkDir`.

Optional later: automatic cleanup after success (R2.7 setting).

### B1 — junk MCP projects (in addition to §7)

- [x] Link log: `ConfigMcpSyncService.LinkAndSyncInstanceAsync`, UI journal in `ConfigMcpViewModel` (2026-06-30)
- [x] Block duplicate project by client name in `projects.json` (2026-06-30)
- [x] Default: not "Create project" if MCP already has project with client name (2026-06-30)
- [x] UI hint: manual cleanup / reconcile CLI (2026-06-30)
- [ ] UI to remove project/database in portable or reconcile via CLI
- [ ] Manual cleanup of existing "Р" / "P" in `projects.json` or Admin GUI config-mcp

---

## Out of scope (for now)

- Scheduled exports, snapshot checksums.
- CI with installed 1C.
- Migrations of existing `configadmin.db` (NO_DB_MIGRATIONS policy).

---

## Priorities

| P | Task |
|---|------|
| P0 | **B2:** crash on reopening infobase card — **done** (2026-06-30) |
| P0 | ConfigAdmin protocol CLI (Phase 1) |
| P0 | config-mcp integration (Phase 1) — **done** |
| P1 | **B1** junk MCP projects "Р" — **partial** (2026-06-30) |
| P1 | **B3** plan on Export screen — **done** (2026-06-30) |
| P1 | **B4** cleanup work dirs on Relay — **done** (2026-06-30) |
| P1 | **ConfigurationTemplate / Instance** (export plan, Remote extensions) |
| P1 | **H6** — **done** (2026-06-30) |
| P1 | Remote Sync R2: UX/GUI + upload speed — **partial** (Hub settings, shell nav, 2026-07-01) |
| P1 | Client/infobase tree + client editing |
| P1 | Deletion and reset |
| P2 | Export all infobases (GUI) |
| P2 | ConfigAdmin registry sync (Phase 2) |
| P3 | XML vs archive, auto-discover extensions |
| P3 | help-mcp links (Phase 3) |
| P3 | **data-mcp integration** — mapping **agreed + ack** (2026-07-01); Hub Phase 1 (D-H1…D-H5) **ready** — [`registry-mapping-data-mcp.md`](admin-hub/registry-mapping-data-mcp.md) |

*Updated: 2026-07-02*
