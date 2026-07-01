# Changelog

Формат: краткие записи по дате. Продуктовые релизы — по мере публикации; инфраструктурные — отдельно.

## [Unreleased]

### Added

- Нормализация **Head (H)** по канону WI 2.2.0: группа `1c-cursor`, `docs/group/shared/`, group sync scripts.
- MCP instance-level linking, UI activity journal (см. `docs/todo.md`).
- **B3:** чекбоксы плана выгрузки на `ExportView` (session override, без записи в SQLite).
- **B4:** ручная очистка артефактов на Передатчике (`AgentDataCleanupService`, две кнопки в `SyncAgentView`).
- **WPF:** экран «Настройки Hub» (`HubSettingsView`); `ProfileService` update-by-id при переименовании клиента; тест `ProfileServiceTests`.

### Fixed

- **B2:** краш Hub при повторном открытии карточки базы (singleton VM + detached WPF bindings; `PrepareEditAsync`, reuse `BaseEditView`, `DataContext = null` при навигации).
- **B1 (частично):** защита от дубликата MCP-проекта по имени клиента; логирование привязки.
- **WPF:** vault lock не оставляет мастер-пароль в VM; переименование клиента по id без дубликата; «Проверить соединение» без неявного сохранения export-настроек; zombie-процессы при закрытии (`Environment.Exit` только по timeout shutdown).

### Changed

- Канон общего протокола группы — `docs/group/shared/`; Hub-спеки — `docs/admin-hub/`.
- **WPF UI review:** shell-навигация «← Назад (Esc)»; `HubSettingsView` (listen URL, смена режима); `PasswordBoxBindingBehavior`; `BusyViewModelBase`; batch export summaries в `MainViewModel`; общие стили в `App.xaml`; ConfigMcp — один refresh при открытии.

## [0.1.0] — 2026-06

- Remote Sync R1 MVP, Admin Hub Phase 1 (config-mcp screen, apply-registry).
- Согласованный registry mapping Hub ↔ config-mcp (2026-06-28).
