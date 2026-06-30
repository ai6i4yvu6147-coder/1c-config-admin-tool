## Remote Sync — текущий статус

*Обновлено: 2026-06-30*

### Итог Phase R-Ping + R1 (E2E)

**Проверено end-to-end (transport):** register → heartbeat → poll job → chunk upload → complete → apply в ExportRoot (интеграционный тест `SyncUploadIntegrationTests`).

**Проверено в production-like сценарии (Phase R-Ping):** Передатчик на RDP → Tailscale Funnel → Hub; register, heartbeat, poll `job=null`.

**Проверен E2E R1 — transport (2026-06-28):** реальная база 1С на RDP → export (~34k файлов, ~1,1 GB, ~6 мин) → zip (~800 MB) → chunk upload через Funnel (~30+ мин) → `Completed` → конфигурация в `{ExportRoot}/{Client}/{Base}/Основная конфигурация` на Hub.

**Проверен E2E MCP — локальная выгрузка расширения (2026-06-30):** hub-first привязка MCP → выгрузка расширения на Hub → `apply-registry` + H6 `rebuild-index` → расширение в MCP.

**R1 MVP (transport + apply на диск):** **готово**.

**Полный продуктовый цикл (transport + MCP):** доставка/выгрузка на Hub → `apply-registry` → **`rebuild-index`** (H6 на Hub, **готово** 2026-06-30).

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
- **Cleanup на RDP (B4):** две кнопки на Передатчике — job-каталоги / полная очистка `%AppData%\ConfigAdmin\`; см. [`../todo.md`](../todo.md) § B4
- **Упрощение GUI** Remote Sync (Hub + Передатчик — сейчас перегружен; проработка UX отдельно)
- **Hub H6:** orchestration `rebuild-index` — **готово** (2026-06-30)

### Известные проблемы / фиксы

| Симптом | Причина | Статус |
|---------|---------|--------|
| `HttpClient.Timeout of 30 seconds` на upload | Chunk ~8 MB через Funnel дольше 30 с; клиент не совпадал с transport.md (120 s) | **исправлено:** chunk 180 s, complete 600 s, retry chunk ×3 |
| Плотный журнал Передатчика во время export | Каждый tick монитора (12 с) писался в `LogLines` | **исправлено:** routine progress только в CurrentProgress + heartbeat |
| Файлы на RDP после успешного sync | `TryCleanupWorkDir` только текущий job | **открыто (B4):** 2 кнопки — job dirs / полная очистка ConfigAdmin на RDP |
| MCP не видит выгруженную конфигурацию | `rebuild-index` не запускался | **исправлено (H6, 2026-06-30)** |
| Hub вылетает при повторном открытии карточки базы | та же ИБ, без сообщений; debug | **открыто (B2, P0)** |

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
