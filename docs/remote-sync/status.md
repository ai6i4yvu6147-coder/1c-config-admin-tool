## Remote Sync — текущий статус

*Обновлено: 2026-06-28*

### Итог Phase R-Ping + R1 (E2E)

**Проверено end-to-end (transport):** register → heartbeat → poll job → chunk upload → complete → apply в ExportRoot (интеграционный тест `SyncUploadIntegrationTests`).

**Проверено в production-like сценарии (Phase R-Ping):** Передатчик на RDP → Tailscale Funnel → Hub; register, heartbeat, poll `job=null`.

**Проверен E2E R1 — transport (2026-06-28):** реальная база 1С на RDP → export (~34k файлов, ~1,1 GB, ~6 мин) → zip (~800 MB) → chunk upload через Funnel (~30+ мин) → `Completed` → конфигурация в `{ExportRoot}/{Client}/{Base}/Основная конфигурация` на Hub.

**R1 MVP (transport + apply на диск):** **готово**.

**Полный продуктовый цикл (transport + MCP):** доставка на Hub → `apply-registry` → **`rebuild-index` / парсинг XML в index config-mcp** — **ещё не автоматизирован** на Hub (Phase 3 orchestration). Если парсинг не запускался, config-mcp корректно показывает «нет базы» при уже лежащих на диске файлах — это **ожидаемо** для R1 E2E.

---

### Что готово

| Компонент | Где в коде |
|-----------|------------|
| Таблицы `remote_nodes`, `agent_sessions`, **`sync_jobs`** | `DatabaseInitializer.cs` |
| **`infobases`:** `export_location`, `remote_node_id`, `remote_export_path` | `InfobaseProfile`, `InfobaseRepository` |
| Pairing (Argon2id + Bearer token) | `PairingSecretService`, `SyncAgentHubService` |
| **Job credentials cipher** (AES-GCM, HKDF от accessToken) | `JobCredentialsCipher.cs` |
| Hub receiver (Kestrel `:18443`) | `SyncReceiverHost.cs` — register, heartbeat, jobs, **upload**, fail job |
| **Upload sessions** (chunks, resume, complete) | `SyncUploadSessionStore`, `SyncUploadHubService`, `SyncUploadCompleter` |
| **Hub orchestration** (vault unlock, create Pending job) | `RemoteSyncOrchestrator` |
| **Agent:** export 1С → zip → upload → cleanup | `RemoteConfigurationExportService`, `SyncAgentJobProcessor` |
| **Live-прогресс export** (file count + size на RDP) | `ExportDirectoryMonitor.cs` |
| **Журнал Передатчика:** routine progress без спама (UI + Hub heartbeat) | `JobProgressUpdate`, `SyncAgentConnectionService` |
| **Agent:** fail job reporting | `SyncAgentClient.FailJobAsync`, `POST .../jobs/{id}/fail` |
| HTTP-клиент Передатчика + DoH fallback | `SyncAgentClient.cs`, `PublicDnsResolver.cs` |
| Цикл poll/heartbeat + job processing | `SyncAgentConnectionService.cs` |
| WPF: Local/Remote в карточке базы | `BaseEditView`, `BaseEditViewModel` |
| WPF: «Выгрузить и синхронизировать с RDP» | `ExportView`, `ExportViewModel` |
| WPF: progress bar Передатчика | `SyncAgentView`, `SyncAgentViewModel` |
| WPF: CRUD RDP-узлов, online/offline | `RemoteNodesView` |
| Tailscale Funnel (скрипты) | `setup-tailscale-funnel.ps1`, `start-sync-tunnel.bat` |
| Тесты | `JobCredentialsCipherTests`, `SyncUploadIntegrationTests`, `ExportDirectoryMonitorTests`, … |

---

### Что не готово (Phase R2+)

- Headless agent / Windows Service, автозапуск
- Multi-PC Hub / central relay
- `uploadOnly` (копирование готовых XML без export на RDP)
- Расширения конфигурации в Remote sync (MVP — только основная конфигурация)
- Автопереподключение Передатчика при длительном обрыве
- **Оптимизация скорости upload** (~800 MB / 30+ мин через Funnel — см. [`implementation-plan.md`](implementation-plan.md) R2.6)
- **Настройка cleanup на RDP** (удалять work dir после успеха или оставлять для отладки/resume)
- **Упрощение GUI** Remote Sync (Hub + Передатчик — сейчас перегружен; проработка UX отдельно)
- **config-mcp:** orchestration `rebuild-index` после доставки (см. [`../admin-hub/integration.md`](../admin-hub/integration.md) — workflow export → MCP)

### Известные проблемы / фиксы

| Симптом | Причина | Статус |
|---------|---------|--------|
| `HttpClient.Timeout of 30 seconds` на upload | Chunk ~8 MB через Funnel дольше 30 с; клиент не совпадал с transport.md (120 s) | **исправлено:** chunk 180 s, complete 600 s, retry chunk ×3 |
| Плотный журнал Передатчика во время export | Каждый tick монитора (12 с) писался в `LogLines` | **исправлено:** routine progress только в CurrentProgress + heartbeat |
| Файлы на RDP после успешного sync | `TryCleanupWorkDir` после complete; возможен сбой (lock/права) или кастомный `remote_export_path` | **открыто:** диагностика + опция «удалять / оставить» (R2.7) |
| MCP не видит выгруженную конфигурацию («нет базы») | XML на диске есть, но **парсинг / `rebuild-index` не запускался**; R1 делает только apply-registry (опционально), без orchestration rebuild | **ожидаемо для R1;** полный цикл — Phase 3 Hub orchestration |

---

### Ручная проверка E2E (R1)

*Чеклист пройден 2026-06-28 (real RDP + 1С + Funnel).*

1. **Hub:** Tailscale + `setup-tailscale-funnel.ps1` → `sync-tunnel.url`.
2. **Hub:** ConfigAdmin → **Админка** → разблокировать **vault**.
3. **Hub:** создать RDP-узел (pairing-пароль), базу с **Remote** + узел + путь 1С.
4. **Hub:** `start-sync-tunnel.bat`.
5. **RDP:** ConfigAdmin → **Передатчик** → Hub URL, Node ID, pairing → **Подключиться**.
6. **Hub:** Выгрузка → «**Выгрузить и синхронизировать с RDP**» — пароль базы **не** спрашивается на RDP.
7. **Hub:** дождаться `Completed`; проверить `{ExportRoot}/{Client}/{Base}/Основная конфигурация`.
8. *(опционально)* оборвать upload и проверить resume через agent `resume/` state.

Подробнее: [`network-setup.md`](network-setup.md), [`transport.md`](transport.md) §2.3.
