## Remote Sync — план реализации

**Предусловие:** Phase 1 config-mcp (локальный sync) — **готово**.

---

## Phase R-Ping — **готово** (2026-06-28)

**Цель:** исходящее подключение RDP → Hub без upload.

| # | Задача | Статус |
|---|--------|--------|
| R-P.1 | `remote_nodes`, `agent_sessions`, pairing | ✅ |
| R-P.2 | `SyncReceiverHost`: register, heartbeat, jobs=null | ✅ |
| R-P.3 | `SyncAgentClient` + `SyncAgentConnectionService` | ✅ |
| R-P.4 | WPF: mode selector, RemoteNodes, SyncAgentView | ✅ |
| R-P.5 | Tailscale Funnel + `network-setup.md` | ✅ |
| R-P.6 | DoH fallback для DNS на RDP (`PublicDnsResolver`) | ✅ |
| R-P.7 | E2E manual: real RDP register + heartbeat | ✅ |

---

## Phase R0 — модель (остаток)

| # | Задача | Статус |
|---|--------|--------|
| R0.1 | `remote_nodes`, `agent_sessions` | ✅ |
| R0.2 | Domain + repositories | ✅ |
| R0.3 | `sync_jobs`, поля `infobases` (Remote) | ⬜ |
| R0.4 | `ExportLocation` enum, orchestrator stub | ⬜ |

---

## Phase R1 — MVP E2E (upload)

Оценка: **2–4 недели** после R0 remainder.

### R1.A Hub — модель и UI

| # | Задача | Статус |
|---|--------|--------|
| R1.A.1 | `RemoteNodeRepository`, CRUD | ✅ |
| R1.A.2 | WPF: RemoteNodesView | ✅ |
| R1.A.3 | BaseEdit: Local/Remote, node, remote path | ⬜ |
| R1.A.4 | Main: «Синхронизировать с RDP», job status | ⬜ |

### R1.B Mode selector + Agent UI

| # | Задача | Статус |
|---|--------|--------|
| R1.B.1 | Startup: Hub vs Agent (+ remember) | ✅ |
| R1.B.2 | SyncAgentView — register, status, log | ✅ |
| R1.B.3 | Agent settings (DPAPI token) | ✅ |

### R1.C Transport — Hub receiver

| # | Задача | Статус |
|---|--------|--------|
| R1.C.1 | Kestrel in Hub mode | ✅ |
| R1.C.2 | Agent endpoints (register/heartbeat/jobs) | ✅ |
| R1.C.3 | Upload sessions + chunks on disk | ⬜ |
| R1.C.4 | Complete → unzip → atomic apply | ⬜ |

### R1.D Transport — Agent client

| # | Задача | Статус |
|---|--------|--------|
| R1.D.1 | register, poll | ✅ |
| R1.D.2 | sessions, chunks, resume | ⬜ |
| R1.D.3 | Zip from `remote_source_path` | ⬜ |

### R1.E Integration

| # | Задача | Статус |
|---|--------|--------|
| R1.E.1 | `RemoteSyncOrchestrator` — job lifecycle | ⬜ |
| R1.E.2 | Optional MCP sync after success | ⬜ |
| R1.E.3 | Manual test plan [`mvp-spec.md`](mvp-spec.md) | ⬜ (ping ✅) |

**DoD:** полный [`mvp-spec.md`](mvp-spec.md) Definition of Done.

---

## Phase R2 — удобство

| # | Задача |
|---|--------|
| R2.1 | `configadmin.exe sync-agent` headless |
| R2.2 | Windows Service / tray, автопереподключение |
| R2.3 | Export на RDP из Hub |
| R2.4 | Multi-PC Hub / central relay (проработка) |

---

## Phase R3 — масштаб

| # | Задача |
|---|--------|
| R3.1 | Relay VPS |
| R3.2 | S3 async |
| R3.3 | Scheduled sync |

---

## Зависимости

```mermaid
flowchart LR
  RP[R-Ping done]
  R0[R0 remainder]
  R1C[R1 upload receiver]
  R1D[R1 agent upload]
  R1E[R1 integration]

  RP --> R0
  R0 --> R1C
  R0 --> R1D
  R1C --> R1E
  R1D --> R1E
```

---

## Риски

| Риск | Митигация |
|------|-----------|
| Hub недоступен с RDP | Tailscale Funnel + DoH — **проверено** |
| Корпоративный DNS | `PublicDnsResolver` (DoH) |
| Большие конфигурации | zip + chunk + resume (R1) |
| Несколько рабочих ПC | один канонический Hub — см. [`overview.md`](overview.md) |

*Обновлено: 2026-06-28*
