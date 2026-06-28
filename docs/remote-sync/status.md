## Remote Sync — текущий статус

*Обновлено: 2026-06-28*

### Итог Phase R-Ping

**Проверено end-to-end:** Передатчик на RDP (без прав администратора) → исходящий HTTPS → Hub (Админка) через **Tailscale Funnel**. Register, heartbeat, poll `job=null` работают; узел отображается **online** в списке RDP-узлов.

Transport для **выгрузки файлов** (chunk upload, sync_jobs) **ещё не реализован** — следующий этап R1.

---

### Что готово

| Компонент | Где в коде |
|-----------|------------|
| Таблицы `remote_nodes`, `agent_sessions` | `DatabaseInitializer.cs` |
| Pairing (Argon2id verifier + Bearer token) | `PairingSecretService`, `SyncAgentHubService` |
| Hub receiver (Kestrel `:18443`) | `SyncReceiverHost.cs` — `register`, `heartbeat`, `GET jobs` → `{ job: null }` |
| HTTP-клиент Передатчика + DoH fallback | `SyncAgentClient.cs`, `PublicDnsResolver.cs` |
| Цикл poll/heartbeat | `SyncAgentConnectionService.cs` |
| WPF: выбор режима Админка / Передатчик | `HubModeSelectorView` |
| WPF: CRUD RDP-узлов, online/offline | `RemoteNodesView`, `RemoteNodeEditViewModel` |
| WPF: UI Передатчика (лог, копирование) | `SyncAgentView`, `SyncAgentViewModel` |
| Старт receiver в режиме Админка | `HubRuntimeService` |
| Подсказка Hub URL из `%AppData%\ConfigAdmin\sync-tunnel.url` | `SyncTunnelUrlStore` |
| Tailscale Funnel (скрипты) | `setup-tailscale-funnel.ps1`, `start-sync-tunnel.bat` |
| Тесты | `SyncAgentHubServiceTests`, `SyncReceiverIntegrationTests`, `PairingSecretServiceTests`, `PublicDnsResolverTests` |

---

### Что не готово (R1 MVP)

- Таблица `sync_jobs`, поля `infobases` (Local/Remote, `remote_source_path`)
- Chunk upload API и `SyncUploadSessionStore`
- Упаковка zip на RDP, resume, atomic apply в ExportRoot
- Кнопка «Синхронизировать с RDP» в UI Hub
- Headless agent, автозапуск, multi-PC Hub — Phase R2+

См. [`implementation-plan.md`](implementation-plan.md), [`mvp-spec.md`](mvp-spec.md).

---

### Ручная проверка (как сейчас)

1. **Hub:** установить Tailscale → `setup-tailscale-funnel.ps1` → сохранится `sync-tunnel.url`.
2. **Hub:** запустить ConfigAdmin → **Админка** → создать RDP-узел (pairing-пароль).
3. **Hub:** `start-sync-tunnel.bat` (funnel на `:18443`).
4. **RDP:** portable `ConfigAdmin.exe` → **Передатчик** → Hub URL из `sync-tunnel.url`, Node ID, pairing-пароль → **Подключиться**.
5. **Hub:** RDP-узлы → **Last seen** обновляется, статус **online**.

Подробнее о сети: [`network-setup.md`](network-setup.md).

---

### Открытые вопросы (не блокируют R1 upload)

- Несколько рабочих ПC с админками — один канонический Hub vs central relay ([`overview.md`](overview.md)).
- Автопереподключение Передатчика при обрыве — по желанию, Phase R2.
