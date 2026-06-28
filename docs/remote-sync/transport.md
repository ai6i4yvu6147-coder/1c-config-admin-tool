## Remote Sync Transport — HTTPS Chunk Upload v1

Normative протокол доставки XML с RDP (Передатчик) на Hub (Админка).

**Свойства:** outbound-only с RDP, resume после обрыва, SHA-256, UTF-8 JSON API.

---

## 1. Общие правила

| Канал | Формат | Кодировка |
|-------|--------|-----------|
| Request/response body (JSON) | JSON | UTF-8 без BOM |
| Chunk body | raw bytes | binary |
| Errors | JSON `{ "error": "...", "code": "..." }` | UTF-8 |

- **TLS:** HTTPS обязателен в production. Для dev допустим `http://` + Tailscale/local only.
- **Auth:** заголовок `Authorization: Bearer <pairing-token>` — token = HMAC или derived secret после pairing (см. §2).
- **Timeouts:** client connect 30s; chunk PUT 120s; idle session TTL 24h.

---

## 2. Pairing и аутентификация

### 2.1. Создание узла (Hub)

Hub генерирует `node_id` (UUID) и пользователь задаёт **pairing-пароль** (plain, один раз).

Hub хранит `pairing_secret_verifier` = Argon2id(password) — как vault verifier.

### 2.2. Register (Agent → Hub)

```http
POST /api/sync-agent/register
Content-Type: application/json

{
  "nodeId": "uuid",
  "pairingPassword": "user-entered",
  "agentVersion": "1.0.0",
  "machineName": "RDP-SERVER-01"
}
```

**Response 200:**

```json
{
  "accessToken": "opaque-token-rotatable",
  "pollIntervalMs": 10000,
  "hubVersion": "1.0.0"
}
```

**Response 401:** неверный пароль или disabled node.

Дальнейшие запросы: `Authorization: Bearer <accessToken>`.

Token TTL: 7 дней (renew on register); хранится в agent local settings (DPAPI).

### 2.3. Job credentials (encryptedConnectionPassword)

Пароль подключения к базе 1С **authoritative на Hub** (vault + `infobases.encrypted_password`). Передатчик **не** запрашивает пароль у пользователя.

**Предусловие Hub:** vault разблокирован. Без unlock sync недоступен (ошибка «Разблокируйте vault»).

При выдаче job в poll response Hub включает поле `encryptedConnectionPassword` — **AES-GCM blob**, не plain text:

```text
blob = nonce(12) || tag(16) || ciphertext
```

**Ключ (Hub ↔ Agent, valid accessToken):**

```text
key = HKDF-SHA256(
  ikm = SHA256(accessToken),
  salt = jobId (16 bytes, UUID),
  info = "configadmin-sync-job-v1",
  length = 32)
```

- **Hub** (claim job): `_secretVault.Decrypt(profile.EncryptedPassword)` → `JobCredentialsCipher.Encrypt(...)` → DTO.
- **Agent:** `JobCredentialsCipher.Decrypt(accessToken, jobId, nodeId, blob)`.
- **AAD:** `jobId` (16 bytes) || `nodeId` (16 bytes) — защита от подмены job.
- Plain password **не логировать**, не писать в resume JSON; zeroize после export.

Pairing-пароль **не** используется для шифрования job (одноразовый при register). Канал защищён TLS + Bearer; cipher — defense in depth против утечки в логах/прокси.

**Poll job response (фрагмент):**

```json
{
  "job": {
    "jobId": "uuid",
    "nodeId": "uuid",
    "action": "exportAndUpload",
    "remoteExportPath": "",
    "packaging": "zip",
    "maxChunkSizeBytes": 8388608,
    "export": {
      "platformPath": "C:\\Program Files\\1cv8\\8.3.24.1234\\bin\\1cv8.exe",
      "connectionType": "File",
      "connectionString": "C:\\Bases\\Demo",
      "username": null,
      "exportConfiguration": true,
      "exportFormat": "Hierarchical"
    },
    "encryptedConnectionPassword": "<base64 or raw bytes in JSON>"
  }
}
```

`remoteExportPath` пустой → Agent использует temp: `%AppData%\ConfigAdmin\agent\work\{jobId}\Основная конфигурация` (cleanup после upload).

---

## 3. Poll jobs (Agent → Hub)

```http
GET /api/sync-agent/jobs?nodeId={uuid}
Authorization: Bearer ...
```

**Response 200 — есть job:**

```json
{
  "job": {
    "jobId": "uuid",
    "infobaseId": "uuid",
    "remoteSourcePath": "D:\\Exports\\Client\\Base\\Основная конфигурация",
    "packaging": "zip",
    "maxChunkSizeBytes": 8388608
  }
}
```

**Response 200 — пусто:**

```json
{ "job": null }
```

Hub переводит job `Pending` → `Claimed` при первой выдаче agent'u.

### 3.1. Fail job (Agent → Hub)

При ошибке export/upload на RDP:

```http
POST /api/sync-agent/jobs/{jobId}/fail
Authorization: Bearer ...

{ "errorMessage": "DumpConfigToFiles failed: ..." }
```

**Response 200:** job → `Failed`, `error_message` сохранён.

**Response 400:** job не найден или не принадлежит узлу.

---

## 4. Upload session

### 4.1. Create session

Agent после подготовки payload (zip или manifest):

```http
POST /api/sync-upload/sessions
Authorization: Bearer ...

{
  "jobId": "uuid",
  "fileName": "configuration.zip",
  "totalBytes": 524288000,
  "sha256": "hex-lowercase",
  "chunkSizeBytes": 8388608
}
```

**Response 201:**

```json
{
  "sessionId": "uuid",
  "chunkSizeBytes": 8388608,
  "acceptedChunks": []
}
```

### 4.2. Upload chunk

```http
PUT /api/sync-upload/sessions/{sessionId}/chunks/{chunkIndex}
Authorization: Bearer ...
Content-Type: application/octet-stream
Content-Length: ...

<raw bytes>
```

**Response 200:**

```json
{
  "chunkIndex": 3,
  "receivedBytes": 8388608,
  "sessionReceivedBytes": 33554432
}
```

- `chunkIndex` — 0-based.
- Последний chunk может быть меньше `chunkSizeBytes`.
- **Идемпотентность:** повторная отправка того же chunkIndex с тем же hash — 200 OK (replace or skip).

### 4.3. Resume (после обрыва)

```http
GET /api/sync-upload/sessions/{sessionId}
Authorization: Bearer ...
```

**Response 200:**

```json
{
  "sessionId": "uuid",
  "totalBytes": 524288000,
  "sha256": "hex",
  "chunkSizeBytes": 8388608,
  "receivedChunkIndexes": [0, 1, 2, 5],
  "sessionReceivedBytes": 41943040,
  "expiresAt": "2026-06-29T12:00:00Z"
}
```

Agent вычисляет missing indexes и шлёт только их.

### 4.4. Complete

```http
POST /api/sync-upload/sessions/{sessionId}/complete
Authorization: Bearer ...
```

**Response 200:**

```json
{
  "success": true,
  "appliedPath": "C:\\Exports\\Local\\Client\\Base\\Основная конфигурация",
  "jobStatus": "Completed"
}
```

Hub:

1. Проверяет все chunks present.
2. Склеивает temp file.
3. Verifies SHA-256.
4. Распаковывает zip → atomic replace целевого каталога (`AtomicDirectoryService`).
5. Job → `Completed`.

**Response 409:** hash mismatch / incomplete chunks.

---

## 5. Heartbeat

```http
POST /api/sync-agent/heartbeat
Authorization: Bearer ...

{ "nodeId": "uuid", "status": "idle" | "exporting" | "uploading", "currentJobId": null, "progressMessage": "optional detail for Hub UI" }
```

Hub обновляет `remote_nodes.last_seen_at`. Если указаны `currentJobId` и `status` (`exporting` / `uploading`), job переводится в соответствующий статус; `progressMessage` показывается в UI Hub (in-memory, без записи в БД).

---

## 6. Hub-side storage layout

```text
%AppData%\ConfigAdmin\sync\
  sessions\{sessionId}\
    meta.json
    chunks\{index}.part
  incoming\{sessionId}.zip   (assembled)
```

Cleanup: удалять sessions старше 24h или после Complete.

---

## 7. Agent-side resume state

```text
%AppData%\ConfigAdmin\agent\
  settings.json          (hub URL, nodeId, encrypted token)
  resume\
    {sessionId}.json     (jobId, sha256, sentChunkIndexes[])
```

При старте agent проверяет незавершённые sessions и продолжает upload.

---

## 8. Packaging MVP

**Рекомендация v1:** zip всего каталога `remote_source_path`.

- Проще hash и resume (один file stream).
- Agent: `System.IO.Compression.ZipFile` перед upload.
- Hub: extract после verify.

Phase R2: file-tree manifest без zip для incremental (optional).

---

## 9. Ошибки и retry

| Code | HTTP | Agent action |
|------|------|--------------|
| `NETWORK_ERROR` | — | exponential backoff, resume session |
| `CHUNK_REJECTED` | 400 | retry chunk до 3 раз |
| `SESSION_EXPIRED` | 410 | создать новую session, начать upload заново |
| `JOB_CANCELLED` | 409 | abort, clear resume |
| `HASH_MISMATCH` | 409 | fail job, user notification |

---

## 10. Почему это «100% на RDP»

1. **Только исходящие HTTPS** — не нужны inbound ports на RDP.
2. **Не требует admin rights** — обычный user может читать export path и слать HTTPS.
3. **Resume** — переживает обрывы Wi‑Fi/RDP disconnect.
4. **Не зависит от RDP clipboard/drive redirection** — отдельный канал поверх TCP 443 (или Tailscale).

**Не гарантирует** доставку если корпоративный FW блокирует **весь** outbound HTTPS к вашему IP — тогда Tailscale/VPN (см. [`overview.md`](overview.md)).

---

## 11. Deviation от generic Admin Hub protocol

Private API ConfigAdmin до Phase R2. Поля JSON совместимы по духу с protocol v1 (UTF-8, exit semantics через HTTP status).

Будущее: `export-registry` fragment может включать `remoteNodeId` + last sync metadata.
