# MongoDB TUI Viewer

[English](README.md) | **Русский**

Кроссплатформенный терминальный клиент для MongoDB, повторяющий UX-логику Studio 3T:
менеджер подключений, дерево серверов/баз/коллекций, вкладки запросов, просмотр и
редактирование документов, импорт/экспорт, автодополнение и ИИ-помощник на базе
OpenAI-совместимого API.

## Возможности

- **Менеджер подключений** — профили в SQLite; секреты шифруются AES-256-GCM.
- **Секреты** — KEK из OS keyring, при недоступности — мастер-пароль (Argon2id).
- **Вкладки запросов** — запросы выполняются через `mongosh --eval` с выводом Extended JSON, запуск по `F5`.
- **Панель результатов** — режимы таблицы, дерева и JSON, время выполнения и ошибки.
- **Документы** — добавление / изменение / удаление в формате JSON.
- **Индексы и статистика** — список, создание и удаление индексов; счётчики документов/индексов.
- **Экспорт / импорт** — `mongoexport`/`mongoimport` (json, canonical json, csv) и `mongodump`/`mongorestore`.
- **Автодополнение** — базы, коллекции и поля (с кэшем полей).
- **ИИ-помощник** — генерация запроса по схеме; перевод чужих запросов (например SQL) в MongoDB.
- **История запросов**, **тёмная тема**, **локализация** (английский/русский).

## Зависимости для работы

- **.NET 10 runtime** — только при framework-dependent запуске (self-contained сборка его включает).
- **Сервер MongoDB**, к которому выполняется подключение.
- **MongoDB Database Tools** — `mongosh`, `mongoexport`, `mongoimport`, `mongodump`, `mongorestore`.
  Должны быть в `PATH`, либо каталог задаётся в настройках (`MongoToolsOptions.Directory`).
- **Опционально OS keyring** — libsecret (Linux), Keychain (macOS), Credential Manager (Windows).
  Если недоступен, используется мастер-пароль.

## Запуск из бинаря

Готовые бинари пока не публикуются; соберите их (см. ниже) и запустите полученный файл.

- **Windows**: `MongoTui.TUI.exe`
- **Linux / macOS**: `./MongoTui.TUI`

Framework-dependent сборку также можно запустить через `dotnet MongoTui.TUI.dll`.

## Как пользоваться

1. Запустите приложение. Если секреты заблокированы, сначала откроется окно ввода мастер-пароля
   (либо KEK автоматически берётся из OS keyring, если он доступен); затем открывается диспетчер
   подключений.
2. **Connection → Manage connections…** — добавьте или отредактируйте подключение. Вставьте
   полную строку подключения (с учётными данными) и нажмите **Из URI**, чтобы заполнить поля
   автоматически: логин/пароль переносятся в отдельные поля, а база и механизм аутентификации
   разбираются из строки. Выберите подключение и нажмите **Подключить** — оно появится в дереве
   **активных подключений** слева, развёрнутое на список баз (пустые помечаются как `(пустая)`).
3. **Двойной клик** по узлу дерева (или **Enter**): по подключению — свернуть/развернуть,
   по базе — загрузить и показать коллекции, по коллекции — открыть вкладку запроса с готовым
   скриптом `find`.
4. Напишите JavaScript-запрос (например `db.users.find({})`) и нажмите **F5**.
5. Смотрите результат в панели таблицы/дерева/JSON.

Обзор меню:

| Меню | Действия |
|---|---|
| File | Exit (выход) (Ctrl+Q) |
| Connection | Manage connections… (Ctrl+O), Refresh (F9), Unlock secrets… (F8), Сменить мастер-пароль… (Ctrl+P) |
| Query | New tab (Ctrl+T), Run (F5), Close tab (Ctrl+W), AI assistant… (F6), Translate… (F7), History… (Ctrl+H) |
| Document | Insert… (Ctrl+I), Edit… (Ctrl+E), Delete… (Ctrl+D) |
| Collection | Indexes… (Ctrl+K), Export… (F2), Import… (F3) |
| Настройки | Вид результата (Таблица / Дерево / JSON), Язык (English / Русский) |

## Настройки и данные

Все данные лежат в домашнем каталоге пользователя:

- **Linux / macOS**: `~/.mongotui/`
- **Windows**: `%USERPROFILE%\.mongotui\`

| Путь | Назначение |
|---|---|
| `mongotui.db` | База SQLite: подключения, настройки, история запросов |
| `logs/app-*.log` | Логи Serilog (только в файл) |

| Настройка | Где | Значение по умолчанию |
|---|---|---|
| AI base URL | код (`AiOptions.BaseUrl`) | `https://api.openai.com/v1` |
| AI модель | код (`AiOptions.Model`) | `gpt-4o-mini` |
| AI API-ключ | хранилище настроек, ключ `ai.api_key` | — |
| Каталог MongoDB Tools | `MongoToolsOptions.Directory` | системный `PATH` |
| Salt мастер-пароля | хранилище настроек, ключ `security.kek_salt` | создаётся при первой разблокировке |
| Проверка мастер-пароля | хранилище настроек, ключ `security.kek_check` | создаётся при первой разблокировке |
| Язык | локаль ОС (en по умолчанию, ru доступен) | `en` |

ИИ работает с любым OpenAI-совместимым эндпоинтом (OpenAI, Azure OpenAI, Ollama, OpenRouter, локальные).

## Запуск и сборка из исходников

Требуется **.NET 10 SDK**.

```sh
dotnet run --project src/MongoTui.TUI
```

Сборка и тесты всего решения:

```sh
dotnet build MongoTui.sln
dotnet test MongoTui.sln
```

Интеграционные тесты требуют Docker (Testcontainers) и помечены
`[Trait("Category", "Integration")]`. Их можно исключить:

```sh
dotnet test MongoTui.sln --filter "Category!=Integration"
```

## Self-contained сборка

Публикация одного self-contained файла (на целевой машине .NET runtime не нужен):

```sh
# Linux x64
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true

# Windows x64
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true

# macOS (Apple Silicon)
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true
```

Другие RID: `osx-x64`, `linux-arm64`. Результат — в
`src/MongoTui.TUI/bin/Release/net10.0/<runtime>/publish/`.

Framework-dependent сборка (меньше размер, нужен .NET 10 runtime):

```sh
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release
```

## Архитектура

Onion-архитектура из четырёх слоёв:

```
TUI (Terminal.Gui)  →  Application  →  Domain
        ↘ Infrastructure ────────────────────────↗
```

| Проект | Назначение |
|---|---|
| `MongoTui.Domain` | Чистое ядро: модели, value objects, инварианты |
| `MongoTui.Application` | Интерфейсы (abstractions) и оркестрирующие сервисы |
| `MongoTui.Infrastructure` | Реализации: Mongo, SQLite, Crypto, AI, Process |
| `MongoTui.TUI` | TUI: View, ViewModel, редактор, темы |

Доступ к MongoDB идёт через **Composite Gateway**, который роутит запросы/CRUD в
официальный драйвер, а массовые операции (экспорт/импорт, dump/restore) — в CLI
MongoDB Database Tools.

## Стек

- .NET 10 (LTS), Terminal.Gui 2.5
- MongoDB.Driver + MongoDB Database Tools
- Microsoft.Data.Sqlite + LINQ to DB (linq2db) — SQLite через LINQ, raw SQL только для миграций
- AES-256-GCM + Argon2id
- Serilog (логирование только в файл)
- xUnit + FakeItEasy + AwesomeAssertions, Testcontainers

## Конвенции проекта

- [`AGENTS.md`](AGENTS.md)
