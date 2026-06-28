## Remote Sync MVP — спецификация первого E2E

Документ для **kickoff**: что должно работать в первой поставке, чтобы снять боль копирования с RDP.

---

## User stories

### US-1. Настройка узла (Hub)

**Как** администратор на локальном ПК  
**Я хочу** создать «RDP-узел» для клиента с pairing-паролем  
**Чтобы** передатчик на удалённой машине мог безопасно подключиться.

**Acceptance:**

- CRUD узлов: имя, клиент, pairing-пароль (задать/сменить).
- Отображаются `node_id` (для ввода на RDP) и рекомендуемый URL Hub.
- `last_seen_at` обновляется после register/heartbeat.

### US-2. Remote-база (Hub)

**Как** администратор  
**Я хочу** пометить базу как Remote и указать путь на RDP  
**Чтобы** Hub знал, что забирать и куда класть локально.

**Acceptance:**

- Поле `export_location`: Local | Remote.
- Remote: выбор узла + `remote_source_path` (абсолютный, валидация непустой строки).
- Локальный target показывается read-only: `{ExportRoot}/{Client}/{Base}/Основная конфигурация`.

### US-3. Передатчик на RDP

**Как** пользователь на RDP  
**Я хочу** запустить тот же exe в режиме «Передатчик»  
**Чтобы** не ставить отдельную программу.

**Acceptance:**

- Стартовый экран: **Админка** | **Передатчик**.
- Форма: Hub URL, node ID, pairing-пароль, [Подключиться].
- Статус: Connected / ошибка auth / uploading / idle.
- Лог последних операций (scroll, 100 строк).

### US-4. Запрос sync (Hub)

**Как** администратор  
**Я хочу** нажать «Синхронизировать с RDP» для remote-базы  
**Чтобы** XML появился локально без ручного копирования.

**Acceptance:**

- Кнопка только для Remote-бases с заполненным node + path.
- Создаётся `sync_job` Pending.
- UI: прогресс (bytes, %), статус, ошибка.
- Успех: файлы в локальном configuration folder (atomic replace).
- Agent offline: сообщение «узел не в сети (last seen …)».

### US-5. Resume

**Как** пользователь при нестабильной сети  
**Я хочу** чтобы upload продолжился после обрыва  
**Чтобы** не начинать multi-GB заново.

**Acceptance:**

- Обрыв на chunk N → reconnect → досланы только missing chunks.
- Complete только после full SHA-256 match.

### US-6. MCP (optional checkbox)

**Как** администратор  
**Я хочу** после успешной доставки опционально sync MCP  
**Чтобы** config-mcp увидел новый sourcePath.

**Acceptance:** переиспользует существующий `ConfigMcpSyncService` если база привязана к MCP project.

---

## UI — экраны (Hub)

### Старт exe

| Элемент | Действие |
|---------|----------|
| Админка | → Vault → Main (как сейчас) |
| Передатчик | → SyncAgentView |
| ☐ Запомнить режим | `appsettings.local.json` |

### Новый: Remote nodes (меню / кнопка на Main)

DataGrid: Client, Name, Node ID (copy), Last seen, Enabled  
Actions: Add, Edit, Disable

Edit dialog: Name, Client, Pairing password, Hub URL hint

### Расширение: Base edit

- Radio: Локальная / Удалённая
- Remote: Combo узлов клиента, TextBox `remote_source_path`, Browse (disabled on Hub — path on RDP)

### Расширение: Main или Base context

- **«Синхронизировать с RDP»** (Remote only)
- Progress panel / modal

---

## UI — Передатчик

Minimal WPF:

```
[ Hub URL: https://100.x.x.x:8443/sync ]
[ Node ID: ........ ]
[ Pairing password: **** ]
[ Подключиться ]

Status: ● Connected | last job: ...
[ Лог ▼ ]
```

Polling в фоне; при job — progress bar upload.

---

## API surface (Hub embedded Kestrel)

| Method | Path | Назначение |
|--------|------|------------|
| POST | `/api/sync-agent/register` | pairing |
| POST | `/api/sync-agent/heartbeat` | liveness |
| GET | `/api/sync-agent/jobs` | poll |
| POST | `/api/sync-upload/sessions` | start upload |
| PUT | `/api/sync-upload/sessions/{id}/chunks/{n}` | chunk |
| GET | `/api/sync-upload/sessions/{id}` | resume state |
| POST | `/api/sync-upload/sessions/{id}/complete` | finalize |

Spec: [`transport.md`](transport.md).

**Binding:** по умолчанию `https://0.0.0.0:18443` (configurable в Hub settings). Dev cert / Tailscale.

---

## SQLite bootstrap (новые объекты)

См. [`architecture.md`](architecture.md). Добавить в `DatabaseInitializer` через CREATE TABLE (NO_DB_MIGRATIONS = без конвертации данных, bootstrap OK).

```sql
CREATE TABLE IF NOT EXISTS remote_nodes (...);
CREATE TABLE IF NOT EXISTS sync_jobs (...);
-- infobases: export_location, remote_node_id, remote_source_path
```

---

## Out of scope MVP

- Export 1С на RDP из Hub
- Windows Service / autostart agent
- Расширения (только «Основная конфигурация»)
- Relay VPS
- Расписание cron
- Multi-file без zip

---

## Test plan (manual)

**Phase R-Ping (выполнено):**

1. Hub: создать RDP-узел, funnel + Админка.
2. RDP: Передатчик register — Hub shows last seen, online.
3. Heartbeat каждые ~10 s, poll `job=null`.
4. Wrong pairing password — 401, clear message.

**Phase R1 upload (осталось):**

1. Hub: remote base, path.
2. Hub: Sync — progress → Completed.
3. Verify local folder contents match remote.
4. Kill network mid-upload — restore — resume.
5. Optional: MCP sync after delivery.

---

## Definition of Done (MVP)

- [x] Mode selector at exe startup
- [x] `remote_nodes` + `agent_sessions` + pairing
- [ ] `sync_jobs` + extended `infobases` (Remote)
- [x] Hub UI: nodes CRUD, online status
- [ ] Hub UI: remote base fields, sync button + progress
- [x] Sync Agent UI + poll (register/heartbeat; upload — нет)
- [ ] Hub receiver: sessions, chunks, complete, atomic apply
- [ ] Manual test plan passed on one real RDP via Tailscale (ping ✅, upload ⬜)
- [x] `dotnet test` green (agent hub, receiver, pairing, DNS)
