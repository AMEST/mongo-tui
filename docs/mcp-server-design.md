# MCP-сервер в MongoTui — дизайн

**Статус:** предложение (draft), не реализовано  
**Дата:** 2026-11-15  
**Ветка:** `docs/MCP` от `master` (`be59ee2`, версия 0.10.0)  
**Область:** Domain, Application, Infrastructure, TUI, Web, SQLite, локализация, HTTP-хост

## 1. Цель

Сделать MongoTui **MCP-сервером**, к которому может подключаться AI-агент, и дать
пользователю полный контроль над тем, что агенту разрешено делать.

Функция должна работать в обоих режимах приложения:

- **TUI** — `mongo-tui`;
- **Web** — `mongo-tui web`.

Требования:

1. Включение и выключение MCP из настроек TUI и Web.
2. Набор инструментов задаётся настройками, а не тем, что пришлёт агент:
   - read-only: `find`, `find_one`, `count`, простой `aggregate`;
   - write: полная замена документа и обновление отдельных полей;
   - delete: отдельный инструмент удаления;
   - script: отдельный инструмент выполнения произвольных скриптов.
3. Ограничения проверяются на сервере при каждом вызове. Агент не должен получить
   инструмент или подключение, которые не разрешены в настройках.
4. Видимость подключений настраивается. Агенту доступны только имена разрешённых
   подключений и, при отдельном разрешении, имена баз. URI, логины, пароли и SSH-параметры
   не раскрываются.
5. Ведётся история вызовов агента: инструмент, аргументы, подключение, результат, ошибка,
   длительность. Отдельно видны запросы, которые выполняются прямо сейчас, с возможностью
   отмены.
6. HTTP-запросы MCP защищены bearer-токеном. Токен можно перегенерировать и скопировать
   в любой момент; в SQLite он хранится зашифрованным.

### 1.1. Результат для пользователя

1. Пользователь включает MCP и выбирает разрешённые инструменты и подключения.
2. Копирует токен и передаёт его агенту вместе с адресом `http://127.0.0.1:<port>/mcp`.
3. Агент видит только разрешённые инструменты.
4. Любой вызов отражается в истории; активный вызов можно отменить.
5. Перегенерация токена немедленно отзывает старый токен.

## 2. Вне области

- MCP-клиент внутри MongoTui.
- Публичное сетевое размещение MCP: сервер слушает только loopback.
- Аутентификация нескольких пользователей и разграничение доступа между ними.
- OAuth, mTLS, внешние identity provider-ы.
- Автоматическое выполнение write/delete/script без явного разрешения.
- Массовые операции, `deleteMany` по произвольному фильтру в первой версии.
- Полноценный аудит с неизменяемым журналом и экспортом в SIEM.
- MCP-инструменты для управления пользователями, индексами, базами и коллекциями.
- `aggregate` с `$out`, `$merge`, `$function`, `$accumulator`.
- Интерактивные SSH-запросы (2FA) во время вызова MCP. В первой версии такие
  подключения возвращают ошибку, если туннель ещё не открыт.

## 3. Текущее состояние

| Что уже есть | Где | Чего не хватает для MCP |
|---|---|---|
| Два режима запуска | `src/MongoTui.TUI/Program.cs` | В TUI нет HTTP-хоста; Web-хост обслуживает только Blazor |
| Web-хост на loopback | `src/MongoTui.Web/Bootstrap/WebHost.cs` | Нет отдельного MCP-endpoint и его авторизации |
| Настройки в SQLite | `ISettingsStore`, `SqliteSettingsStore` | Нет ключей `mcp.*` |
| Шифрование секретов | `SecretKeyService`, `AesGcmCryptoService` | Нет зашифрованного MCP-токена |
| Подключения | `IConnectionRepository`, `ConnectionService` | Нет allowlist для агента |
| Чтение и запись | `IMongoGateway` | Нет `UpdateOne` для изменения отдельных полей |
| Произвольные скрипты | `QueryService`, `IMongoShellSession` | Нет отдельной точки входа и изоляции сессий MCP |
| История shell-запросов | `query_history`, `HistoryDialog`, `HistoryPanel` | Нет истории инструментов, аргументов и активных вызовов |
| Web-аутентификация | `WebSessionStore`, `WebSessionAuthenticationMiddleware` | Токен живёт в памяти, хранится только хэш, его нельзя показать |
| UI настроек | `AiSettingsDialog`, `SettingsPanel.razor` | Нет раздела MCP |

MCP-кода в проекте сейчас нет. Архитектура позволяет добавить его как новый адаптер,
не меняя Domain и не смешивая MCP с Blazor-аутентификацией.

## 4. Принципы

- **MCP — это сервер, а не клиент.** MongoTui предоставляет инструменты внешнему агенту.
- **Loopback-only.** Как и Web, MCP слушает `127.0.0.1`; порт настраивается.
- **Запрещено по умолчанию.** MCP выключен, все write/delete/script-инструменты выключены.
- **Права проверяются на каждом вызове.** Скрытие инструмента из `tools/list` не является
  защитой: обработчик обязан повторно проверить настройку, allowlist подключения и лимиты.
- **Один инструмент — одна опасная возможность.** Не должно быть универсального
  `execute_anything`, кроме явно включаемого `run_script`.
- **Только имена подключений.** Агент никогда не получает connection string и секреты.
- **История — часть фичи, а не лог.** Аргументы вызовов хранятся в SQLite; в файловый лог
  попадают только имя инструмента, подключение, длительность и код ошибки.
- **TUI и Web используют один Application-слой.** Отличается только представление.
- **Никаких новых абстракций “на будущее”.** Транспорт MCP изолирован в отдельном проекте,
  остальное переиспользует существующие сервисы.

## 5. Принятые решения

1. Использовать официальный C# SDK `ModelContextProtocol` с ASP.NET Core-транспортом.
   Версия фиксируется в `Directory.Packages.props`.
2. Транспорт — Streamable HTTP, endpoint `/mcp`, отдельный loopback-listener.
   stdio-транспорт в первой версии не добавляется.
3. MCP-хост выносится в новый проект `src/MongoTui.Mcp` (адаптер). TUI и Web ссылаются
   на него; Application и Domain не знают про MCP SDK.
4. Web-режим и TUI-режим запускают один и тот же `McpHttpHost` на `mcp.port`
   (по умолчанию `8766`). Отдельный порт не пересекается с Blazor и не проходит через
   `WebSessionAuthenticationMiddleware`.
5. Настройка `mcp.enabled` применяется на лету: `McpHostController` запускает или
   останавливает listener. Изменения из другого процесса подхватываются polling-монитором
   (аналогично `LoggingSettingsMonitor`), в своём процессе — сразу после сохранения.
6. Allowlist подключений хранится по стабильным `ConnectionId`; агенту отдаются имена.
   Поиск подключения по имени выполняется на сервере.
7. Read-only реализуется через `IMongoGateway.FindAsync`, `CountAsync`, `AggregateAsync`.
   Это исключает запись на уровне драйвера; дополнительно валидируются стадии агрегации.
8. Для изменения отдельных полей добавляется `UpdateOneAsync` с разрешёнными
   операторами `$set` и `$unset`. Полная замена использует существующий `ReplaceOneAsync`.
9. Удаление — отдельный инструмент `delete_one_document`. `delete_many_documents`
   не входит в первую версию.
10. `run_script` использует отдельные mongosh-сессии MCP, а не сессии вкладок TUI/Web.
11. Токен: 32 случайных байта в hex. В SQLite хранятся зашифрованный токен и SHA-256
    для проверки. Перегенерация выполняется одной транзакцией.
12. История: таблица `mcp_requests`; запись создаётся со статусом `running` до выполнения
    и обновляется после. Активные вызовы дополнительно находятся в памяти процесса.

## 6. Пользовательские сценарии

### 6.1. Включение в TUI

1. Пользователь открывает `Settings → MCP...`.
2. Видит статус `Stopped`, порт и выключенные переключатели.
3. Включает `Enabled`, `Read`, выбирает разрешённые подключения.
4. Нажимает `Save`.
5. Диалог показывает `Running` и адрес `http://127.0.0.1:8766/mcp`.
6. MCP-хост запускается без вывода в консоль, чтобы не ломать Terminal.Gui.

### 6.2. Включение в Web

1. Пользователь открывает `Settings → MCP`.
2. Настраивает те же параметры, что и в TUI.
3. Нажимает `Save`.
4. `McpHostController` запускает listener; раздел показывает статус и адрес.
5. Токен можно показать, скопировать и перегенерировать.

### 6.3. Подключение агента

1. Пользователь копирует токен.
2. Агент отправляет MCP-запрос с `Authorization: Bearer <token>`.
3. Сервер проверяет токен, состояние `mcp.enabled` и разблокировку хранилища секретов.
4. Агент получает список инструментов, разрешённых настройками.
5. Вызов с неизвестным или запрещённым инструментом отклоняется.

### 6.4. Read-only запрос

1. Агент вызывает `list_connections` и получает только разрешённые имена.
2. При `expose_databases = true` вызывает `list_databases` и получает имена баз.
3. Вызывает `find` с `connection`, `database`, `collection`, `filter`.
4. Сервер проверяет allowlist, размер фильтра, лимит документов и таймаут.
5. Результат возвращается агенту, вызов попадает в историю.

### 6.5. Изменение документа

1. Пользователь включает `Write`.
2. Агент вызывает `update_fields` с фильтром по `_id` и объектом `set`/`unset`.
3. Сервер отклоняет изменение `_id`, неизвестные операторы и слишком большие изменения.
4. Возвращаются `matched` и `modified`; операция сохраняется в истории.

### 6.6. Удаление и скрипты

1. Для удаления пользователь отдельно включает `Delete`; агент вызывает
   `delete_one_document`.
2. Для произвольного JavaScript пользователь отдельно включает `Script` и подтверждает
   предупреждение.
3. `run_script` выполняется в изолированной MCP-сессии; результат и скрипт сохраняются
   в истории.
4. Пользователь может отменить активный вызов из TUI/Web; отмена передаётся в
   `CancellationToken` драйвера или mongosh-сессии.

### 6.7. Перегенерация токена

1. Пользователь нажимает `Regenerate`.
2. Подтверждает, что старый токен перестанет работать.
3. Сервер создаёт новый токен, шифрует его, обновляет хэш и время создания.
4. UI показывает новый токен и кнопку `Copy`.
5. Следующий запрос со старым токеном получает `401`.

## 7. Архитектура

```text
MCP client (agent)
        |
        | HTTP /mcp, Authorization: Bearer <token>
        v
+-----------------------------+
| MongoTui.Mcp                |
| McpHttpHost                 |
| - loopback listener         |
| - token authentication      |
| - MCP session lifecycle     |
| - tools/list + tools/call   |
+--------------+--------------+
               |
               v
+-----------------------------+
| McpToolDispatcher           |
| - capability check          |
| - connection allowlist      |
| - argument validation       |
| - limits/timeouts           |
| - history + active registry |
+--------------+--------------+
               |
      +--------+---------+
      |                  |
      v                  v
+-----------+     +----------------------+
| QueryService |   | IMongoGateway        |
| mongosh      |   | find/count/aggregate |
| (run_script) |   | replace/update/delete|
+-----------+     +----------------------+
      |                  |
      +--------+---------+
               v
         MongoDB / SSH tunnel

TUI / Web UI
    |
    v
McpSettingsService ---> SQLite settings
IMcpHistoryRepository -> SQLite mcp_requests
McpRequestRegistry ---> in-memory active calls
```

### 7.1. Слои

- **Domain** — неизменяемые модели возможностей и истории, если они не зависят от UI.
- **Application** — `McpSettingsService`, `McpToolPolicy`, `McpHistoryEntry`,
  `IMcpHistoryRepository`, `McpRequestRegistry`, интерфейсы выполнения.
- **Infrastructure** — `SqliteMcpHistoryRepository`, новая таблица, реализация
  `UpdateOneAsync` в driver gateway.
- **MongoTui.Mcp** — MCP SDK, HTTP-хост, auth, регистрация инструментов, маппинг
  MCP-запросов в Application-сервисы. Зависит от Application, не от TUI/Web.
- **TUI** — диалог настроек и окно истории.
- **Web** — секция настроек и страница/панель истории.

`MongoTui.Mcp` не должен содержать бизнес-правила, которые нельзя проверить без SDK.
Политика прав и валидация аргументов живут в Application и покрываются unit-тестами.

### 7.2. Запуск хоста

- `WebHost` при старте создаёт `McpHostController` как hosted service.
- `AppHost` в TUI при старте создаёт тот же контроллер и передаёт ему `IApplication`
  только для отображения статуса; сам хост не пишет в консоль.
- `McpHostController` читает `mcp.enabled`, `mcp.port`, `mcp.token` и управляет
  `McpHttpHost`.
- При выключении listener освобождает порт. При повторном включении запускается заново.
- Если порт занят, MCP остаётся выключенным, а UI показывает ошибку. Приложение
  продолжает работать.
- При блокировке master password listener может оставаться запущенным, но каждый запрос
  получает `503` до разблокировки; это лучше, чем молчаливое отключение.

## 8. Настройки

Новый `McpSettingsService` по образцу `AiSettingsService`.

| Ключ | Тип | По умолчанию | Назначение |
|---|---|---|---|
| `mcp.enabled` | bool | `false` | Запускать ли MCP-хост |
| `mcp.port` | int | `8766` | Loopback-порт |
| `mcp.tools.read` | bool | `false` | Разрешить read-инструменты |
| `mcp.tools.write` | bool | `false` | Разрешить замену и изменение полей |
| `mcp.tools.delete` | bool | `false` | Разрешить удаление одного документа |
| `mcp.tools.script` | bool | `false` | Разрешить `run_script` |
| `mcp.expose_databases` | bool | `false` | Отдавать ли имена баз |
| `mcp.connections` | JSON-массив | `[]` | `ConnectionId`, видимые агенту |
| `mcp.token_cipher` | base64 | — | Зашифрованный токен |
| `mcp.token_hash` | hex | — | SHA-256 токена для проверки |
| `mcp.token_created_at` | ISO-8601 | — | Время генерации |
| `mcp.history_limit` | int | `5000` | Сколько записей хранить |

Правила:

- `mcp.enabled = false` делает неактивными все остальные настройки.
- `write`, `delete`, `script` не включаются автоматически вместе с `read`.
- Если `mcp.connections` пуст, агент не видит ни одного подключения.
- При удалении подключения его ID остаётся в allowlist, но `list_connections` его
  не показывает; вызов возвращает ошибку «подключение недоступно».
- Порт проверяется в диапазоне `1024..65535`, не может совпадать с портом Web
  (`8765` по умолчанию), если Web запущен.
- Токен создаётся при первом включении MCP, если его ещё нет.
- Все строки UI — через `.resx` (en/ru) в TUI и Web.

### 8.1. Модель настроек

```csharp
public sealed record McpSettings
{
    public bool Enabled { get; init; }
    public int Port { get; init; } = 8766;
    public bool ReadEnabled { get; init; }
    public bool WriteEnabled { get; init; }
    public bool DeleteEnabled { get; init; }
    public bool ScriptEnabled { get; init; }
    public bool ExposeDatabases { get; init; }
    public IReadOnlyList<ConnectionId> AllowedConnections { get; init; } = [];
    public bool HasToken { get; init; }
}
```

Сам токен не входит в `McpSettings`, который передаётся в UI-списки. Для показа и
копирования используется отдельный метод `GetTokenAsync`, требующий разблокированного
`SecretKeyService`.

## 9. Инструменты

### 9.1. Матрица

| Инструмент | Возможность | Вход | Бэкенд | Ограничения |
|---|---|---|---|---|
| `list_connections` | metadata | — | `IConnectionRepository` | Только allowlist, только имена |
| `list_databases` | read/metadata | `connection` | `IMongoGateway.ListDatabasesAsync` | Только при `expose_databases` |
| `find` | read | `connection`, `database`, `collection`, `filter` | `FindAsync` | `filter` — JSON-объект; лимит документов; таймаут |
| `find_one` | read | `connection`, `database`, `collection`, `filter` | `FindAsync` с `Limit = 1` | То же |
| `count` | read | `connection`, `database`, `collection`, `filter` | `CountAsync` | Только JSON-объект; таймаут |
| `aggregate` | read | `connection`, `database`, `collection`, `pipeline` | `AggregateAsync` | Whitelist стадий; лимит стадий/документов; таймаут |
| `replace_document` | write | `connection`, `database`, `collection`, `id`, `document` | `ReplaceOneAsync` | `_id` не меняется; документ — JSON-объект |
| `update_fields` | write | `connection`, `database`, `collection`, `id`, `set`, `unset` | новый `UpdateOneAsync` | Только `$set`/`$unset`; `_id` запрещён; лимит полей |
| `delete_one_document` | delete | `connection`, `database`, `collection`, `id` или `filter` | `DeleteOneAsync` | Удаляется не более одного документа; обязательный фильтр |
| `run_script` | script | `connection`, `database`, `script` | `QueryService`/mongosh | Отдельная сессия; лимит размера и времени; предупреждение |

`list_connections` доступен всегда, пока MCP включён: без имён подключений агент не сможет
работать. Он не раскрывает URI и секреты.

`list_databases` относится к read-возможности, но дополнительно управляется
`mcp.expose_databases`. Если имя базы скрыто, агент всё равно может передать его в
инструмент, если пользователь указал базу в запросе; сервер проверит подключение.

Имена инструментов в коде — константы; строки описаний для `tools/list` берутся из
resource-файлов или из стабильных английских констант, чтобы агент видел предсказуемый
контракт.

### 9.2. Read-only

- `find` не принимает `sort`, `projection`, `skip` и произвольные опции. Для сортировки
  и проекции используется `aggregate`.
- `aggregate` принимает только массив стадий. Разрешённые стадии:
  `$match`, `$project`, `$addFields`, `$set`, `$unset`, `$sort`, `$limit`, `$skip`,
  `$count`, `$group`, `$unwind`, `$lookup`, `$facet`, `$sample`.
- Запрещены `$out`, `$merge`, `$function`, `$accumulator`, `$where` и любые стадии,
  выполняющие JavaScript или пишущие данные.
- Вложенные pipeline в `$lookup` и `$facet` проверяются рекурсивно.
- `find`/`count` дополнительно отклоняют операторы, выполняющие JavaScript:
  `$where`, `$function`, `$accumulator`.
- Максимальный размер JSON-аргумента — 64 КБ; максимальная глубина — 20;
  максимальное число стадий — 20.
- Максимум документов на read-вызов — `100` (константа первой версии; позже можно
  вынести в настройки).
- Таймаут берётся из существующей настройки `query.timeoutSeconds`; значение `0`
  означает отсутствие прикладного таймаута, но серверный `maxTimeMS` всё равно
  применяется для драйверных операций.

### 9.3. Write

`replace_document`:

- принимает `id` (значение `_id`) и `document`;
- документ должен быть JSON-объектом;
- `_id` в новом документе либо отсутствует, либо равен исходному;
- возвращает `matched`, `modified`.

`update_fields`:

- принимает `id`, объект `set` и массив `unset`;
- разрешены только операторы `$set` и `$unset`;
- запрещено менять `_id` и использовать точечные пути, начинающиеся с `_id`;
- количество полей ограничено;
- возвращает `matched`, `modified`.

Оба инструмента используют драйвер, а не mongosh, поэтому не могут выполнить
произвольный JavaScript. Автоматических retry нет: повторный вызов после таймаута
может быть неидемпотентным.

### 9.4. Delete

`delete_one_document`:

- принимает либо `id`, либо `filter`;
- если переданы оба, `filter` игнорируется или отклоняется (выбирается строгий вариант:
  отклонять);
- фильтр обязателен и не может быть пустым объектом;
- удаляется не более одного документа;
- возвращает `deleted`.

`delete_many_documents` в первую версию не входит. Если он понадобится, это будет
отдельный инструмент и отдельный переключатель, а не параметр существующего.

### 9.5. Script

`run_script`:

- отдельный переключатель, независимый от write/delete;
- выполняется через `QueryService` с выделенным `TabId`/сессией MCP;
- сессия закрывается по завершении или отмене;
- входной скрипт ограничен по размеру (например, 64 КБ);
- результат возвращается в том же формате, что и shell-запрос;
- скрипт и результат сохраняются в историю;
- в описании инструмента явно указано, что он даёт полный доступ к базе и может
  обойти все остальные ограничения.

`run_script` — это осознанный режим «break glass». Пока он выключен, агент не может
выполнить произвольный JavaScript через MCP. Пока он включён, гарантий от destructive
изменений нет.

## 10. Проверка прав и безопасности

### 10.1. Проверка вызова

Порядок для каждого `tools/call`:

1. Проверить `Authorization: Bearer`.
2. Проверить `mcp.enabled` и разблокировку `SecretKeyService`.
3. Найти инструмент в реестре и проверить его capability.
4. Проверить `mcp.connections` после разрешения имени подключения.
5. Проверить и нормализовать аргументы.
6. Применить лимиты и таймаут.
7. Создать запись истории со статусом `running`.
8. Выполнить операцию.
9. Обновить историю и вернуть результат.

Если инструмент выключен, сервер возвращает ошибку «инструмент недоступен», не
раскрывая список скрытых инструментов. `tools/list` строится из тех же правил.

### 10.2. Аутентификация

- Токен передаётся только в `Authorization: Bearer`.
- Сравнение — constant-time по SHA-256.
- Пустой, короткий или отсутствующий токен — `401`.
- Неверный токен — `401` без подробностей.
- Заблокированное хранилище — `503`.
- Выключенный MCP — `404`/`503` без раскрытия внутренних настроек.
- Токен не пишется в логи, историю, URL и сообщения об ошибках.
- Перегенерация атомарно заменяет `token_cipher` и `token_hash`; старый токен сразу
  перестаёт проходить проверку.

### 10.3. Сетевые ограничения

- Listener привязан к `IPAddress.Loopback`.
- Проверка `Host` допускает только `127.0.0.1:<port>` и `localhost:<port>`.
- `Origin`, если передан, должен быть пустым или соответствовать локальному адресу.
- Ограничение размера тела запроса, глубины JSON и количества одновременных вызовов.
- Rate limit на токен и на процесс.
- Никаких CORS-заголовков, кроме необходимого минимума для MCP-клиента.

### 10.4. Секреты и подключения

- Агенту доступны только имена разрешённых подключений.
- `list_databases` возвращает только имена баз.
- `ConnectionService.ResolveTargetAsync` и `ConnectionTunnelService` используются
  внутри процесса; connection string не попадает в ответ.
- Для SSH-подключений с интерактивной 2FA MCP в первой версии возвращает ошибку,
  если туннель не был открыт заранее. Это предотвращает неожиданные диалоги во время
  вызова агента.
- Пароли, URI и содержимое документов не пишутся в `app-.log`.

## 11. История и активные вызовы

### 11.1. Таблица `mcp_requests`

Добавляется в `MigrationSql` в `SqliteDatabase` и в `MongoTuiDataConnection`.

```sql
CREATE TABLE IF NOT EXISTS mcp_requests (
    id                TEXT PRIMARY KEY,
    session_id        TEXT,
    tool              TEXT NOT NULL,
    capability        TEXT NOT NULL,
    connection_id     TEXT,
    connection_name   TEXT,
    database          TEXT,
    collection        TEXT,
    arguments_json    TEXT NOT NULL,
    status            TEXT NOT NULL,
    started_at        TEXT NOT NULL,
    finished_at       TEXT,
    duration_ms       INTEGER,
    error_code        TEXT,
    error_message     TEXT,
    result_documents  INTEGER,
    result_bytes      INTEGER
);

CREATE INDEX IF NOT EXISTS ix_mcp_requests_started_at
    ON mcp_requests (started_at DESC);

CREATE INDEX IF NOT EXISTS ix_mcp_requests_status
    ON mcp_requests (status);
```

`arguments_json` хранит аргументы вызова в том виде, в котором их прислал агент.
Это явное требование: история должна показывать, что именно вызывалось. Если в
аргументах есть чувствительные данные, они всё равно остаются в локальной SQLite;
UI должен предупредить об этом. В файловые логи аргументы не попадают.

### 11.2. Статусы

- `running` — вызов выполняется;
- `succeeded` — успешно завершён;
- `failed` — ошибка выполнения;
- `cancelled` — отменён пользователем или клиентом;
- `interrupted` — процесс завершился, не обновив запись.

При старте приложения все записи `running` старше заданного порога помечаются
`interrupted`. Это защищает от вечно «активных» записей после падения процесса.

### 11.3. Активные вызовы

`McpRequestRegistry` — singleton в Application/Infrastructure:

- `ConcurrentDictionary<Guid, ActiveMcpRequest>`;
- хранит `CancellationTokenSource`, инструмент, подключение, время старта;
- `Cancel(id)` отменяет `CancellationToken`;
- при завершении запись удаляется;
- UI читает снимок реестра и показывает активные вызовы;
- `McpToolDispatcher` создаёт linked token: request aborted + timeout + registry.

Если в один момент работают два процесса (TUI и Web) с разными портами, у каждого
свой реестр; история общая через SQLite. Кнопка отмены в UI отменяет только вызовы
своего процесса. Для межпроцессной отмены в будущем можно добавить колонку
`cancel_requested` и polling, но в первой версии это не требуется.

### 11.4. Очистка истории

- По умолчанию хранится 5000 последних записей (`mcp.history_limit`).
- Очистка выполняется после успешной записи, чтобы не блокировать вызов.
- Пользователь может очистить историю вручную.
- История не удаляется при выключении MCP.

## 12. UI

### 12.1. TUI

Новый `McpSettingsDialog` по образцу `AiSettingsDialog`:

- заголовок и статус `Running`/`Stopped`/`Error`;
- `CheckBox` для `Enabled`, `Read`, `Write`, `Delete`, `Script`, `Expose databases`;
- `TextField` для порта;
- `ListView` с множественным выбором подключений (только имена);
- поле токена только для чтения с кнопками `Copy` и `Regenerate`;
- предупреждение при включении `Script`;
- кнопки `Save`, `Close`;
- ошибки сохранения и запуска — в строке статуса, диалог не закрывается.

Новый `McpHistoryDialog`:

- сверху — активные вызовы с кнопкой `Cancel`;
- ниже — последние записи: время, инструмент, подключение, статус, длительность;
- выбор записи показывает аргументы и ошибку в `TextView`;
- `Refresh`, `Clear history`, `Close`;
- `Esc` закрывает диалог.

Меню `Settings` получает пункт `MCP...`; меню/панель истории — пункт `MCP history...`.
Все строки — через `Strings.resx`/`Strings.ru.resx`.

### 12.2. Web

В `SettingsPanel.razor` добавляется секция `MCP`:

- переключатели и порт;
- список подключений с чекбоксами;
- токен в поле `password` с кнопками `Show`, `Copy`, `Regenerate`;
- статус хоста и адрес endpoint;
- предупреждение для `Script`;
- сохранение через `McpSettingsService`, обратная связь через `FormFeedback`.

Новая страница/панель `McpHistoryPanel.razor`:

- карточки/таблица активных вызовов с `Cancel`;
- таблица последних вызовов с фильтром по инструменту/статусу;
- модальное окно с аргументами и результатом;
- `Clear history`;
- автообновление активных вызовов раз в секунду, пока панель открыта.

Навигация: новый пункт в `Routes.razor`/меню, локализация в `LocalizationKeys`.

## 13. Модели и интерфейсы

### 13.1. Application

```csharp
public enum McpCapability
{
    Read,
    Write,
    Delete,
    Script,
}

public enum McpRequestStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Interrupted,
}

public sealed record McpHistoryEntry
{
    public required Guid Id { get; init; }
    public string? SessionId { get; init; }
    public required string Tool { get; init; }
    public required McpCapability Capability { get; init; }
    public ConnectionId? ConnectionId { get; init; }
    public string? ConnectionName { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required string ArgumentsJson { get; init; }
    public required McpRequestStatus Status { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public long? DurationMs { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public long? ResultDocuments { get; init; }
    public long? ResultBytes { get; init; }
}
```

```csharp
public interface IMcpHistoryRepository
{
    Task AddAsync(McpHistoryEntry entry, CancellationToken cancellationToken);
    Task CompleteAsync(Guid id, McpRequestStatus status, long durationMs,
        string? errorCode, string? errorMessage, long? resultDocuments, long? resultBytes,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<McpHistoryEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<McpHistoryEntry>> GetActiveAsync(CancellationToken cancellationToken);
    Task MarkRunningAsInterruptedAsync(CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
```

`McpSettingsService`:

- `GetAsync`, `SaveAsync`, `GetTokenAsync`, `RegenerateTokenAsync`;
- `IsToolEnabled(McpCapability)`, `IsConnectionAllowed(ConnectionId)`;
- `GetAllowedConnectionsAsync` — возвращает только профили из allowlist;
- работает через `ISettingsStore` и `SecretKeyService`.

`McpToolPolicy` — чистые функции:

- `CanInvoke(McpSettings, McpCapability)`;
- `ValidateFilter(string json)`;
- `ValidatePipeline(string json)`;
- `ValidateUpdate(object set, object unset)`;
- `ValidateScript(string script)`;
- лимиты и коды ошибок.

### 13.2. Infrastructure

- `SqliteMcpHistoryRepository`;
- `McpRequestRow` с атрибутами LINQ to DB;
- `DriverMongoGateway.UpdateOneAsync`;
- `McpRequestRegistry` (регистрация singleton в DI).

### 13.3. MongoTui.Mcp

- `McpHttpHost` — Kestrel + `ModelContextProtocol.AspNetCore`;
- `McpHostController` — запуск/остановка по настройке;
- `McpTokenAuthenticator` — проверка bearer;
- `McpToolRegistry` — список инструментов и их capability;
- `McpToolDispatcher` — проверка прав, валидация, история, вызов Application;
- `McpConnectionResolver` — имя → `ConnectionId` → `MongoConnectionTarget` + туннель.

`McpHttpHost` не должен использовать `Console`; логирование — через `ILogger` в Serilog.

## 14. Транспорт и совместимость

- Endpoint: `POST /mcp` (Streamable HTTP), при необходимости `GET /mcp` для SSE.
- Адрес по умолчанию: `http://127.0.0.1:8766/mcp`.
- Заголовок: `Authorization: Bearer <64 hex chars>`.
- Ответы — JSON; ошибки — стандартные MCP error codes.
- Таймауты: прикладной из `query.timeoutSeconds`; для HTTP-запроса — не меньше
  прикладного таймаута.
- MCP-сессия хранится в памяти процесса; при перезапуске клиент инициализируется заново.
- Версия протокола и SDK фиксируются в `Directory.Packages.props`; обновление —
  отдельный PR с регрессионными тестами.

## 15. Локализация

Новые ключи в TUI и Web `.resx`:

- `Mcp.SettingsTitle`, `Mcp.Enabled`, `Mcp.Port`, `Mcp.Read`, `Mcp.Write`,
  `Mcp.Delete`, `Mcp.Script`, `Mcp.ExposeDatabases`, `Mcp.AllowedConnections`,
  `Mcp.Token`, `Mcp.CopyToken`, `Mcp.RegenerateToken`, `Mcp.ConfirmRegenerate`,
  `Mcp.StatusRunning`, `Mcp.StatusStopped`, `Mcp.StatusError`, `Mcp.ScriptWarning`;
- `Mcp.HistoryTitle`, `Mcp.Active`, `Mcp.Cancel`, `Mcp.ClearHistory`,
  `Mcp.NoHistory`, `Mcp.LoadError`, `Mcp.Tool`, `Mcp.Connection`, `Mcp.Database`,
  `Mcp.Collection`, `Mcp.Duration`, `Mcp.Status`, `Mcp.Arguments`, `Mcp.Error`.

Application не возвращает готовые тексты ошибок; он возвращает коды, UI подбирает строку.

## 16. Тестирование

### 16.1. Unit

- `McpSettingsService`: значения по умолчанию, сохранение, генерация токена,
  перегенерация, отсутствие токена при заблокированном хранилище.
- `McpToolPolicy`: матрица возможностей; запрет скрытых инструментов; проверка
  allowlist; валидация фильтров, стадий агрегации, `$set`/`$unset`, `_id`, размеров.
- `McpTokenAuthenticator`: верный/неверный/просроченный токен, constant-time путь.
- `McpRequestRegistry`: регистрация, отмена, удаление, завершение.
- `McpHistoryEntry`: переходы статусов, `interrupted` при старте.

### 16.2. Infrastructure

- Миграция SQLite создаёт `mcp_requests` и индексы идемпотентно.
- `SqliteMcpHistoryRepository`: добавление, завершение, выборка, очистка,
  `MarkRunningAsInterruptedAsync`.
- `DriverMongoGateway.UpdateOneAsync`: `$set`/`$unset`, `_id`, matched/modified.
- Шифрование/дешифрование токена через `SecretKeyService`.

### 16.3. Integration

Testcontainers MongoDB:

- read: `find`, `find_one`, `count`, разрешённый `aggregate`;
- запрет `$out`/`$merge`/`$function`/`$where`;
- write: `replace_document`, `update_fields`; проверка `_id` и лишних операторов;
- delete: `delete_one_document`; пустой фильтр отклоняется;
- `run_script`: успешный скрипт, ошибка, отмена, отдельная сессия;
- allowlist: запрещённое подключение не видно и не вызывается;
- история: запись `running` до выполнения, `succeeded`/`failed` после;
- отмена: `Cancel` прерывает запрос, статус `cancelled`;
- таймаут: запрос завершается с ошибкой таймаута.

### 16.4. TUI/Web

- TUI: диалог настроек загружает/сохраняет значения, показывает статус, копирует токен,
  требует подтверждение при перегенерации и включении `Script`.
- TUI: история показывает активные вызовы и отменяет их через `McpRequestRegistry`.
- Web: секция настроек и история используют те же сервисы; проверяется, что токен
  не попадает в HTML в открытом виде без действия пользователя.
- Локализация en/ru для всех новых ключей.

## 17. Этапы реализации

PR-ы небольшие (по правилам репозитория — до 400 строк изменений):

1. **PR 1: модель и хранение.** `McpSettingsService`, `McpSettings`, токен,
   `mcp_requests`, `IMcpHistoryRepository`, `SqliteMcpHistoryRepository`,
   unit/infrastructure-тесты. Без HTTP и UI.
2. **PR 2: политика и read-инструменты.** `McpToolPolicy`, `McpToolDispatcher`,
   `MongoTui.Mcp`, read-инструменты, bearer-auth, allowlist, хост в TUI/Web.
3. **PR 3: настройки и история в UI.** TUI-диалоги, Web-секция и панель истории,
   активные вызовы и отмена, локализация.
4. **PR 4: write/delete.** `UpdateOneAsync`, `replace_document`, `update_fields`,
   `delete_one_document`, история и тесты.
5. **PR 5: script и hardening.** `run_script`, изоляция сессий, лимиты, rate limit,
   финальные security-тесты и документация.

## 18. Открытые вопросы

1. Нужен ли `delete_many_documents` как отдельный инструмент в первой версии?
   В дизайне — нет; добавлять только после отдельного обсуждения.
2. Должен ли `list_databases` требовать `Read`, если `mcp.expose_databases = true`,
   или это отдельная metadata-возможность? В дизайне — требует `Read`.
3. Нужно ли шифровать `arguments_json` в истории? Требование говорит хранить запросы
   напрямую; текущее решение — хранить как есть в локальной SQLite.
4. Нужна ли межпроцессная отмена, если TUI и Web запущены одновременно?
   В первой версии — нет; каждый процесс отменяет свои вызовы.
5. Нужен ли stdio-транспорт для агентов, которые не умеют HTTP? В первой версии — нет.
6. Как поступать с SSH/2FA: показывать диалог в TUI/Web или возвращать ошибку?
   В первой версии — ошибка, если туннель не открыт.

## 19. Критерии готовности

- MCP включается и выключается в TUI и Web без перезапуска приложения.
- Агент видит только разрешённые инструменты и подключения.
- Вызов запрещённого инструмента/подключения отклоняется на сервере.
- Read-инструменты не могут писать в MongoDB.
- Write/delete/script работают только при явном включении.
- Токен хранится зашифрованным, отображается, копируется и перегенерируется.
- История содержит все вызовы, аргументы и статусы; активные вызовы отменяются.
- Тесты покрывают матрицу прав, валидацию, историю, отмену и миграцию SQLite.
- `dotnet test` зелёный; сборка без warnings.
