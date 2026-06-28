## Remote Sync — сеть и Tailscale Funnel

Настройка **доступности Hub с RDP** без проброса портов на роутере и без прав администратора на RDP.

Передатчик использует только **исходящий HTTPS (443)**. На RDP **не нужен** Tailscale.

---

### Схема

```text
RDP (Передатчик)  --HTTPS outbound-->  Tailscale Funnel (*.ts.net:443)
                                              |
                                              v
Hub ПК (Админка)  <--proxy--  127.0.0.1:18443  SyncReceiverHost (Kestrel)
```

---

### Hub (один раз)

1. Установить [Tailscale for Windows](https://tailscale.com/download/windows).
2. Из корня репозитория:
   ```powershell
   powershell -ExecutionPolicy Bypass -File setup-tailscale-funnel.ps1
   ```
   Войти в аккаунт Tailscale, если попросит. Funnel включается на tailnet.
3. URL сохранится в `%AppData%\ConfigAdmin\sync-tunnel.url`, например:
   `https://<your-machine>.<tailnet>.ts.net`
4. Указать этот URL в карточке RDP-узла (поле Hub URL / подсказка).

### Hub (каждый рабочий сеанс)

1. ConfigAdmin в режиме **Админка** (receiver слушает `:18443`).
2. Запустить funnel:
   ```cmd
   start-sync-tunnel.bat
   ```
   Или вручную: `"C:\Program Files\Tailscale\tailscale.exe" funnel --bg 18443`

ConfigAdmin и Tailscale должны оставаться запущенными, пока RDP-узлы online.

---

### RDP (Передатчик)

1. Скопировать `dist\wpf-single\ConfigAdmin.exe` (сборка: `build-wpf-single.bat`).
2. Режим **Передатчик** → Hub URL (без `:18443` — funnel проксирует 443).
3. Node ID и pairing-пароль — из карточки узла на Hub.

**Корпоративный DNS:** домены `*.ts.net` часто не резолвятся на RDP. В Передатчике включён fallback **DNS over HTTPS** (Cloudflare, Google) — см. `PublicDnsResolver.cs`.

---

### Файлы и артефакты

| Путь | Назначение |
|------|------------|
| `setup-tailscale-funnel.ps1` | Первичная настройка + сохранение URL |
| `start-sync-tunnel.bat` | Повторный запуск funnel |
| `%AppData%\ConfigAdmin\sync-tunnel.url` | Постоянный Hub URL для RDP |
| `%AppData%\ConfigAdmin\configadmin.db` | Узлы, pairing verifiers, last_seen |

---

### Альтернативы (не используются в MVP)

| Вариант | Комментарий |
|---------|-------------|
| Проброс порта / публичный IP | Требует роутер; отклонено |
| Cloudflare Tunnel | Удалён из репозитория |
| Tailscale IP `100.x.x.x` на RDP | Нужен Tailscale **на RDP**; не требуется при Funnel |
| VPS relay | Phase R3 |

---

### Диагностика

| Симптом | Действие |
|---------|----------|
| DNS 11004 на RDP | Пересобрать exe с DoH; проверить `nslookup workstation….ts.net` на RDP |
| 401 при register | Проверить pairing-пароль и `enabled` узла |
| offline на Hub | Funnel не запущен, Админка закрыта, или Передатчик отключён |
| Timeout | Hub спит, firewall на Hub-ПК, funnel сброшен (`tailscale funnel reset`) |

Текущий статус реализации: [`status.md`](status.md).
