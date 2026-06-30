# Backlog — ConfigAdmin

Живой список задач. Текущее состояние продукта — [README.md](../README.md), архитектура — [architecture.md](architecture.md).

---

## Admin Hub protocol (config-admin + host shell)

Протокол: [admin-hub/integration.md](admin-hub/integration.md). Реализация — по фазам, без big bang.

| Фаза | Задачи | Статус |
|------|--------|--------|
| **Phase 1 (config-mcp)** | Hub tables в SQLite, MCP screen, `apply-registry` sync, post-export sync | **готово** |
| **Phase 1 (config-admin CLI)** | `module.manifest.json`, `inventory --json`, `status --json`, JSON для `list-bases`/`list-runs` | не начато |
| **Phase 2** | ConfigAdmin `export-registry`/`apply-registry`, export locks, JSON export/test-connection | не начато |
| **Phase 3 (config-mcp CLI)** | `rebuild-index`, `rebuild-all`, `reconcile-markers`, `apply-registry --trigger-rebuild`, `indexReadiness` в status | **готово** (2026-06-29) |
| **Phase 3 (Hub H6)** | orchestration **`rebuild-index` после export / Remote Sync** | **готово** (2026-06-30, E2E ✅) |

### Instance-level MCP — проверено E2E (2026-06-30)

**Сценарий (ручная проверка):** настройка базы и расширений в Hub → экран MCP: привязка к существующему проекту + «Создать новую database» (до первой выгрузки, planned `sourcePath`) → локальная выгрузка расширения → post-export sync → `apply-registry` + автоматический `rebuild-index` → расширение видно в MCP.

| Шаг | Компонент | Статус |
|-----|-----------|--------|
| Привязка до выгрузки | `ConfigMcpProjectsJsonMerger` (planned path в `projects.json`) | ✅ |
| Синхронизация после выгрузки | `ConfigMcpSyncService.ApplyFragmentAsync` → `apply-registry` | ✅ |
| Индекс `.db` | H6: `rebuild-index` по `followUpOperations` / fallback по `sourcePath` на диске | ✅ |
| Кодировка `projects.json` | `UnsafeRelaxedJsonEscaping` + UTF-8 без BOM в merger | ✅ |

Код: `ConfigMcpSyncService`, `ConfigMcpProjectsJsonMerger`, `ConfigMcpFragmentBuilder`, `ConfigMcpViewModel`, `ExportViewModel.TrySyncToMcpAsync`.

Чеклист для регрессии: [`admin-hub/integration.md`](admin-hub/integration.md) § Reference workflow.

### Registry mapping (config-mcp) — agreed 2026-06-28

Канон: [`admin-hub/registry-mapping.md`](admin-hub/registry-mapping.md). Реализация — **registry R2** (код пока R1).

| ID | Задача | Статус |
|----|--------|--------|
| H1 | `config_mcp_project_id` на Client, auto-assign | не начато |
| H2 | Fragment: Client + N databases | не начато |
| H3 | Export: id и path на base + extensions | не начато |
| H4 | Deprecate ручной линк infobase→project | не начато |
| H5 | Документация `registry-mapping.md` | **готово** |
| H6 | Orchestration `rebuild-index` (apply → followUpOperations → subprocess) | **готово** (2026-06-30, E2E ✅) |
| H7 | UI: «MCP-контейнер» вместо «MCP Project» | не начато |

---

## Remote Sync (доставка XML с RDP)

Спецификация: [`remote-sync/README.md`](remote-sync/README.md). **Phase R-Ping — готово** (2026-06-28).

| Фаза | Задачи | Статус |
|------|--------|--------|
| **R-Ping** | Schema узлов, receiver, agent UI, Tailscale Funnel, E2E register/heartbeat | **готово** |
| **R1 MVP** | Chunk upload, zip/resume, sync UI, E2E выгрузка | **готово** (manual E2E ✅ 2026-06-28) |
| **R1.x** | Live-прогресс export 1С (file count/size в work dir) | **готово** |
| **R2** | Headless agent, **скорость upload**, cleanup-настройка, **упрощение GUI**, multi-PC Hub | не начато |
| **R3** | Relay VPS, S3, расписание | не начато |

Статус и инструкция по сети: [`remote-sync/status.md`](remote-sync/status.md), [`remote-sync/network-setup.md`](remote-sync/network-setup.md).

---

## Продуктовый backlog

### 0. Remote Sync — UX и скорость (после R1 E2E)

**Сейчас:** E2E работает, но GUI перегружен и неочевиден; upload ~800 MB занимает 30+ мин через Funnel.

- Упростить экраны Hub (Remote-база, tunnel, статус job) и Передатчик (pairing + прогресс на одном экране).
- R2.6: оптимизация скорости — chunk size, direct Tailscale, параллельные chunks.
- R2.10: **расчёт и отображение примерной скорости загрузки на Hub** — MB/s (скользящее среднее по отправленным chunks), ETA по `uploaded/total`; UI Передатчика и карточка job на Hub.
- R2.7: cleanup на RDP — **две кнопки** на Передатчике (B4): job-каталоги; полная очистка `%AppData%\ConfigAdmin\` на машине агента.
- **Полный MCP-цикл (H6 / R2.9):** **готово** (2026-06-30) — Hub orchestration `rebuild-index` после export / sync. См. [`admin-hub/integration.md`](admin-hub/integration.md) § Reference workflow.

Детали: [`remote-sync/implementation-plan.md`](remote-sync/implementation-plan.md) R2.6–R2.10, [`remote-sync/status.md`](remote-sync/status.md).

### 1. Выгрузка всех баз

**Сейчас:** `export-all` только в консоли.

- Кнопка «Выгрузить все базы» в WPF.
- Прогресс по базе, итоговый отчёт.

### 2. Удаление и сброс данных

**Сейчас:** delete в `ProfileService`, в GUI недоступно.

- Удаление базы/клиента из GUI.
- Сброс профиля (очистка `%AppData%\ConfigAdmin\`).
- Предупреждение: XML на диске не удаляются.

### 3. План выгрузки на экране «Выгрузка» (B3)

**Сейчас:** план read-only; `ExportEnabled` редактируется только в карточке базы (`BaseEditView`).

**Решение (2026-06-30):** чекбоксы на `ExportView` — **только на один запуск**; `configuration_instances.export_enabled` в SQLite **не менять**. Override в `ExportViewModel` при построении плана / перед export.

- [ ] Чекбоксы в гриде плана на экране Выгрузка
- [ ] Фильтрация export plan / remote jobs по session-override
- [ ] Подсказка в UI: «изменения не сохраняются в настройках базы»

### 4. Дерево клиентов/баз

**Сейчас:** плоский список `Клиент / База`.

- Редактирование клиента.
- UI-дерево: клиент → базы.
- Export root на уровне клиента.

### 5. Журнал выгрузок — **выполнено**

Serilog, `runs/`, meta JSON, экран логов в WPF, `/Out` и `/DumpResult`.

### 6. Формат выгрузки: XML или архив

- Настройка на уровне базы: каталог vs архив.
- Spike CLI 1С для `/DumpCfg` и расширений.

### 7. Единый журнал UI-событий — **частично (2026-06-29)**

**Сейчас:** вкладка «События» в журнале (`UiActivityLog`), ошибки MCP пишутся в память + `%AppData%\ConfigAdmin\logs\`; копируемый текст на экране MCP.

**Целевое:** единый `IUiNotificationService` для всех экранов, автопереход в журнал при ERROR, дублирование в Serilog, опционально toast; удаление «битых» проектов MCP из UI (`apply-registry` remove / Admin GUI config-mcp).

- [x] `UiActivityLog` + вкладка «События»
- [x] MCP → журнал + копируемый статус
- [ ] Подключить Export, Remote Sync, BaseEdit и остальные экраны
- [ ] UI удаления project/database в portable (см. B1)

### 8. ConfigurationTemplate / Instance — **в работе**

Канон: [`domain-model.md`](domain-model.md). Глобальные шаблоны + instances на инфobазе (шаблонные и локальные расширения); единый export plan; Remote 1 job = 1 instance.

- [x] Design note и схема SQLite
- [x] Instance-level MCP link + post-export sync (E2E 2026-06-30)
- [ ] WPF: каталог шаблонов, редактор instances на базе
- [ ] Export / Remote Sync / MCP fragment (H3 partial)

**Follow-up:** автообнаружение расширений из 1С → предложение instance.

---

## Известные баги и UX-дыры (открыто, 2026-06-30)

| ID | Симптом | Контекст / решение | Приоритет |
|----|---------|-------------------|-----------|
| **B1** | Мусорные проекты «Р» / «P» в `projects.json` | Создаёт Hub (подтверждено); сценарий пока не воспроизведён — **отладка** (`LinkAndSyncInstanceAsync`, выбор project/database в UI, лог apply/merge). Валидация ≥ 3 символа не закрыла все пути | P1 |
| **B2** | Hub **вылетает** без сообщений | **Стабильно:** одна и та же ИБ → настройки → назад → снова настройки. `BaseEditViewModel` singleton + `async void BeginEdit`. **Отладка** под дебаггером (стек не в UI / Serilog) | P0 |
| **B3** | План выгрузки read-only на экране «Выгрузка» | Чекбоксы **только на один запуск** (не писать в SQLite). См. §3 выше | P1 |
| **B4** | Артефакты sync на RDP после job | Две кнопки на Передатчике (см. ниже); `TryCleanupWorkDir` — только текущий job | P1 |

### B2 — crash карточки базы

**Воспроизведение:** главный экран → настройки инфобазы → назад → снова те же настройки той же ИБ → процесс падает, диалогов нет.

**План отладки:**
- breakpoint в `BaseEditViewModel.BeginEdit` / `BeginNew` / `LoadInstancesAsync`;
- проверить гонку `_beginSequence` vs повторная навигация (`NavigationService` создаёт новый `BaseEditView` на тот же singleton VM);
- логировать необработанные исключения в `App.xaml.cs` (`DispatcherUnhandledException`).

### B4 — очистка на Передатчике (R2.7)

**Две кнопки** (подтверждение + итог в лог Передатчика):

| Кнопка | Scope | Не трогать |
|--------|-------|------------|
| **Очистить job-каталоги** | `%AppData%\ConfigAdmin\agent\work\` (все `{jobId}\`), опционально `agent\resume\` | активный job (export/upload в процессе) |
| **Полная очистка следов ПО** | весь `%AppData%\ConfigAdmin\` на этой машине: `agent\` (work, resume, `settings.json`), `logs\`, локальный `configadmin.db` если есть | Hub `ExportRoot` на ПК Hub; на RDP обычно нет выгрузок Hub |

Вторая кнопка — сброс Передатчика «как не ставили»: отключение от Hub, пустые поля UI после очистки.

Опционально позже: автоматический cleanup после успеха (настройка R2.7).

Код: `SyncAgentJobProcessor.TryCleanupWorkDir`, `AgentSettingsStore`, `AgentResumeStore`, `AgentWorkDirectoryResolver`.

### B1 — мусорные MCP-проекты (дополнительно к §7)

- [ ] Отладка: логировать `ConfigMcpLinkMode`, `defaultProjectName`, selection project/database при «Привязать»
- [ ] Запретить «Создать новый проект», если в MCP уже есть проект с тем же `clientId` / именем клиента
- [ ] UI удаления project/database в portable или reconcile через CLI
- [ ] Ручная очистка существующих «Р» / «P» в `projects.json` или Admin GUI config-mcp

---

## Вне scope (пока)

- Расписание выгрузок, checksum снимков.
- CI с установленной 1С.
- Миграции существующих `configadmin.db` (политика NO_DB_MIGRATIONS).

---

## Приоритеты

| P | Задача |
|---|--------|
| P0 | **B2:** crash при повторном открытии карточки базы |
| P0 | ConfigAdmin protocol CLI (Phase 1) |
| P0 | config-mcp integration (Phase 1) — **done** |
| P1 | **B1** мусорные MCP-проекты «Р» / **B3** план на экране Выгрузка / **B4** очистка work dirs на Передатчике |
| P1 | **ConfigurationTemplate / Instance** (export plan, Remote extensions) |
| P1 | **H6** — **done** (2026-06-30) |
| P1 | Remote Sync R2: UX/GUI + скорость upload |
| P1 | Дерево клиентов/баз + редактирование клиента |
| P1 | Удаление и сброс |
| P2 | Выгрузить все базы (GUI) |
| P2 | ConfigAdmin registry sync (Phase 2) |
| P3 | XML vs архив, автообнаружение расширений |
| P3 | help/data MCP links (Phase 3) |

*Обновлено: 2026-06-30*

