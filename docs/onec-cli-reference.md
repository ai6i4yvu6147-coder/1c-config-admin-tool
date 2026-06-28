# Справка: командная строка 1cv8 (используемое в ConfigAdmin)

Краткая выжимка параметров платформы 1С, которые формирует [`OneCCommandBuilder`](../src/ConfigAdmin.Integration.OneC/OneCCommandBuilder.cs). Полная документация — у вендора (Приложение 7 «Параметры командной строки»).

Официальная справка по выгрузке: [/DumpConfigToFiles](https://yellow-erp.com/help/1cv8/zif3_dumpconfigtofiles/?lang=ru).

---

## Общий вид

```text
1cv8.exe DESIGNER <подключение> <авторизация> <пакетные команды>
```

Пакетный режим конфигуратора: без диалогов (`/DisableStartupDialogs`, `/DisableStartupMessages`), одна основная команда выгрузки/загрузки на строку (кроме явно совместимых общих параметров).

---

## Подключение к информационной базе

| Параметр | Когда | Пример |
|----------|--------|--------|
| `/S "<сервер>\<база>"` | Серверная ИБ | `/S "srv01\erp"` |
| `/F "<путь>"` | Файловая ИБ | `/F "D:\Bases\MyBase"` |
| `/N"<пользователь>"` | Авторизация | `/N"Admin"` |
| `/P"<пароль>"` | Пароль | `/P"secret"` |

В ConfigAdmin строка `/S` или `/F` берётся из `InfobaseProfile.ConnectionString` (`ConnectionType`).

---

## Выгрузка конфигурации в файлы

### `/DumpConfigToFiles "<каталог>"`

Выгрузка конфигурации или расширения в каталог XML.

| Параметр | Использование в ConfigAdmin |
|----------|----------------------------|
| `-Format Hierarchical` | По умолчанию (`ExportFormat.Hierarchical`) |
| `-Format Plain` | `ExportFormat.Plain` |
| `-Extension "<имя>"` | Одно выбранное расширение |
| `-AllExtensions` | Все расширения (основная конфа **не** выгружается); каждое — в подкаталог с именем расширения |

**Ограничения платформы:**

- `-Extension` и `-AllExtensions` **нельзя** указывать одновременно.
- При работе с расширениями exit code: `0` — успех, `1` — ошибка.
- Каталоги в пути к `<каталог>` должны существовать.

Параметры `-update`, `-force`, `-getChanges`, `-listFile`, `-configDumpInfoOnly` ConfigAdmin **не использует** (полная выгрузка в чистый/заменяемый каталог).

### Пример (основная конфигурация)

```text
1cv8.exe DESIGNER /S "srv\base" /N"Admin" /P"***" /DisableStartupDialogs /DisableStartupMessages /DumpConfigToFiles "D:\Exports\Client\Base\Основная конфигурация" -Format Hierarchical /Out "...\out.log" /DumpResult "...\dumpresult.txt"
```

### Пример (одно расширение)

```text
... /DumpConfigToFiles "D:\...\Расширение1" -Extension "ФТ_Доработки" -Format Hierarchical ...
```

---

## Диагностика пакетного запуска

| Параметр | Назначение |
|----------|------------|
| `/Out "<файл>"` | Служебные сообщения конфигуратора (в ConfigAdmin — `out.log` шага) |
| `/Out "<файл>" -NoTruncate` | Не очищать файл (ConfigAdmin не использует) |
| `/DumpResult "<файл>"` | Результат работы: число (`0` = успех) |
| `/DisableStartupDialogs` | Без стартовых диалогов |
| `/DisableStartupMessages` | Без стартовых предупреждений (IE и т.д.) |

`OneCOutLogReader` разбирает `/Out` для UI и журнала.

---

## Связанные команды (не используются ConfigAdmin сейчас)

| Команда | Кратко |
|---------|--------|
| `/DumpCfg <файл.cf>` | Выгрузка в бинарный cf/cfe |
| `/LoadConfigFromFiles` | Загрузка из каталога XML |
| `/DumpIB` / `/RestoreIB` | Дамп/восстановление всей ИБ |

---

## Код и тесты

- Сборка команд: `src/ConfigAdmin.Integration.OneC/OneCCommandBuilder.cs`
- Запуск процесса: `OneCCliAdapter`
- Тесты: `tests/ConfigAdmin.Tests/OneCCommandBuilderTests.cs`
