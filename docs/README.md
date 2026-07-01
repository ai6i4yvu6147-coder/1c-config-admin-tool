## Документация проекта

Структурированный контекст для разработчиков и агентов. Корневой [README.md](../README.md) — быстрый старт; детали — здесь.

### С чего начать (порядок чтения)

1. [`agent-onboarding.md`](agent-onboarding.md) — политики проекта, роль **Head (H)**, группа `1c-cursor`.
2. [`todo.md`](todo.md) — backlog и roadmap (**включая необработанные пакеты в `group/inbox/`**).
3. [`architecture.md`](architecture.md) — слои решения и поток выгрузки.
4. [`database.md`](database.md) — SQLite, хранилище данных, **NO_DB_MIGRATIONS**.
5. [`cli.md`](cli.md) — команды `configadmin.exe`.
6. [`onec-cli-reference.md`](onec-cli-reference.md) — параметры `1cv8.exe DESIGNER`, используемые при выгрузке.
7. [`remote-sync/README.md`](remote-sync/README.md) — **удалённая доставка с RDP** (R-Ping готово; upload — R1).

### Группа 1c-cursor (Head)

8. [`group/README.md`](group/README.md) — карта Sub, состояние протокола.
9. [`group/shared/README.md`](group/shared/README.md) — **канон** общего протокола и registry mapping.

### Admin Hub (реализация в этом репозитории)

10. [`admin-hub/README.md`](admin-hub/README.md) — integration, архив переговоров.
11. [`admin-hub/integration.md`](admin-hub/integration.md) — roadmap и ownership ConfigAdmin как Hub.
12. [`domain-model.md`](domain-model.md) — целевая модель сущностей (design note).

Протокол экосистемы (канон для всех модулей группы):

- [`group/shared/protocol-v1.md`](group/shared/protocol-v1.md)
- [`group/shared/protocol-v1.0.1-addendum.md`](group/shared/protocol-v1.0.1-addendum.md)
- [`group/shared/protocol-v1.0.2-addendum.md`](group/shared/protocol-v1.0.2-addendum.md)
- [`group/shared/registry-mapping.md`](group/shared/registry-mapping.md)

При конфликте: **v1.0.2 > v1.0.1 > v1**.

Каноны WI (локальная копия): [`canons/`](canons/). Запись нормализации: [`normalize-record.md`](normalize-record.md).
