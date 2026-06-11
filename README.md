# ConfigAdmin — утилита выгрузки конфигураций 1С

Локальная Windows-утилита для хранения профилей баз и выгрузки основной конфигурации и расширений в XML через пакетный режим конфигуратора (`1cv8.exe DESIGNER /DumpConfigToFiles`).

## Требования

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Установленная платформа 1С (`1cv8.exe`)

## Сборка

```powershell
git clone https://github.com/ai6i4yvu6147-coder/1c-config-admin-tool.git
cd 1c-config-admin-tool
dotnet build ConfigAdmin.sln
```

Готовые exe (self-contained, Windows x64):

```powershell
build-all.bat          # папки dist\console и dist\wpf
build-all-single.bat   # один exe в dist\console-single и dist\wpf-single
```

## Запуск

### Консоль (отладка ядра)

```powershell
dotnet run --project src\ConfigAdmin.Console -- init-vault --password "YourMasterPassword"
dotnet run --project src\ConfigAdmin.Console -- add-client --name ClientA --export-root D:\Exports
dotnet run --project src\ConfigAdmin.Console -- add-base `
  --client ClientA `
  --name BaseERP `
  --platform "C:\Program Files\1cv8\8.3.24.0\bin\1cv8.exe" `
  --server "srv01\erp" `
  --user Admin `
  --base-password secret
dotnet run --project src\ConfigAdmin.Console -- test-connection --base BaseERP
dotnet run --project src\ConfigAdmin.Console -- export --base BaseERP
dotnet run --project src\ConfigAdmin.Console -- list-runs --base BaseERP
```

### WPF UI

```powershell
dotnet run --project src\ConfigAdmin.Wpf
```

При первом запуске задайте мастер-пароль. Клиентов удобно добавить через консоль (`add-client`), базы — в UI или через `add-base`.

## Структура выгрузки

```text
{ExportRoot}/{ClientName}/{BaseName}/
  Основная конфигурация/     ← основная конфигурация
  {ИмяРасширения}/           ← каждое расширение отдельно
```

Служебные данные приложения хранятся отдельно от выгрузки:

```text
%AppData%\ConfigAdmin\
  configadmin.db             ← профили, журнал запусков (SQLite)
  logs\                      ← общий лог приложения (Serilog)
  runs\{Client}\{Base}\{runId}\
    export-meta.json         ← meta JSON запуска (шаги, команды, артефакты)
    {step}.out.log           ← /Out платформы 1С
    {step}.dumpresult        ← /DumpResult платформы 1С
```

Пакетный режим 1С: `/DisableStartupDialogs /DisableStartupMessages` — конфигуратор не открывает окно. Для каждого шага выгрузки также передаются `/Out` и `/DumpResult`.

## Архитектура

| Проект | Назначение |
|--------|------------|
| `ConfigAdmin.Domain` | Модели и интерфейсы |
| `ConfigAdmin.Integration.OneC` | CLI-адаптер 1С |
| `ConfigAdmin.Infrastructure` | SQLite, шифрование, файловая система |
| `ConfigAdmin.Application` | Сценарии выгрузки |
| `ConfigAdmin.Console` | CLI для автоматизации |
| `ConfigAdmin.Wpf` | Desktop UI |

Данные приложения: `%AppData%\ConfigAdmin\` (БД, `logs\`, `runs\`)

## Тесты

```powershell
dotnet test tests\ConfigAdmin.Tests
```

## Документация 1С

- [Параметры командной строки](https://its.1c.ru/db/v8317doc/bookmark/adm/TI000000493)
- [/DumpConfigToFiles](https://yellow-erp.com/help/1cv8/zif3_dumpconfigtofiles/?lang=ru)

## План доработок

См. [ROADMAP.md](ROADMAP.md).

## Безопасность

- Мастер-пароль не хранится на диске
- Пароли баз шифруются AES-GCM с ключом от Argon2id
- В журнале команд пароль маскируется (`/P"***"`)
