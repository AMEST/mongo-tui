# Администрирование и отладка MongoDB — дизайн

**Статус:** предложение (draft), не реализовано  
**Дата:** 2026-10-31  
**Ветка:** `master` (`ace96a0`, версия 0.8.0)  
**Область:** Domain, Application, Infrastructure, TUI, Web, SQLite, локализация

## 1. Цель

Добавить в MongoTui единый графический слой администрирования и отладки MongoDB,
доступный и в терминале, и в браузере:

- включать разные уровни логирования медленных запросов без правки конфигурации сервера;
- собирать и показывать статистику выполнения запросов;
- показывать статистику базы данных и сервера в целом;
- быстро разбирать конкретный запрос через `explain`, с деревом плана, метриками
  выполнения и предупреждениями;
- добавить `explain` в автодополнение TUI и Web, чтобы инструмент было легко вызвать.

Функция рассчитана на разработчика или администратора, который уже подключён к MongoDB
с достаточными правами. Включение серверного профайлера всегда явное, с предупреждением
о нагрузке и о том, что состояние хранится на сервере, а не в приложении.

### 1.1. Результат для пользователя

1. В **Diagnostics** видно, какие запросы выполняются, сколько они занимают,
   где ошибки, таймауты и медленные операции.
2. Медленные запросы можно логировать на уровне приложения, на уровне серверного
   профайлера (`slowms`) или включить полный профайлер на время отладки.
3. На одном экране видны `dbStats`, `collStats`, состояние индексов, текущие операции,
   состояние сервера и репликации.
4. Кнопка **Explain** (и `F8` / `Ctrl+Shift+Enter`) показывает план выбранного
   `find`/`findOne`/`aggregate` без ручного набора команды в `mongosh`.
5. В автодополнении есть `explain`, поэтому метод находится поиском по префиксу.

## 2. Текущее состояние

| Что уже есть | Где | Чего не хватает |
|---|---|---|
| Логирование приложения в файл, настройка уровня | `ILoggingSettings`, `LoggingSettingsDialog`, `LoggingSettingsPanel`, `docs/logging.md` | Нет отдельного уровня «медленные запросы»; нет структурированной статистики |
| Просмотр логов в TUI и Web | `LogsPanel.razor`, файловый reader | Логи — это текст, а не агрегаты и не дерево плана |
| Длительность запроса | `QueryService` замеряет `Stopwatch`, `QueryResult.Elapsed` | Замер не сохраняется как метрика; у shell и driver разные пути |
| История shell-запросов | `query_history`, `HistoryPanel.razor` | Только последние 100 shell-скриптов, без статистики и атрибутов `explain` |
| Статистика БД/коллекции | `DatabaseService.GetStatsAsync`, `IndexService.GetStatsAsync`, `MainWindow.cs:685`, `CollectionPanel.razor` | Только `dbStats`/счётчики; нет `serverStatus`, `currentOp`, `$indexStats`, размеров индексов |
| Автодополнение | `QueryMethodCatalog`, `Snippets`, `editor.js` | Нет `explain` |
| MongoDB gateway | `DriverMongoGateway`, `CompositeMongoGateway` | Нет диагностических команд и `explain` |
| Настройки | `settings` таблица SQLite | Нет ключей `diagnostics.*` |
| Хранилище метрик | — | Нет таблицы наблюдений/агрегатов |

Ключевое ограничение: обычный `find` через драйвер не возвращает `docsExamined` и
`keysExamined`. Эти данные доступны только из `explain` или из `system.profile`.
Поэтому дизайн разделяет **клиентские метрики** (всегда доступны приложению) и
**серверные метрики** (доступны, когда включён профайлер или выполнен `explain`).

## 3. Принципы

- **Без изменения поведения по умолчанию.** Серверный профайлер выключен, пока
  пользователь явно его не включил. Сбор клиентских метрик по умолчанию включён,
  но хранится только в памяти.
- **Никакого текста запросов в логах и метриках.** Не сохраняются filter, pipeline,
  документы, `$comment`, значения полей и `command` из `system.profile`.
  Разрешены только namespace, тип операции, длительность, счётчики, код ошибки
  и `planSummary`.
- **Один источник правды для состояния профайлера — сервер.** При открытии экрана
  приложение читает фактическое состояние (`profile: -1`), а не доверяет SQLite.
- **Диагностика не должна ломать основной сценарий.** Ошибка прав, таймаут или
  недоступность `system.profile` показываются локально, но не блокируют работу с БД.
- **Один раз написанная модель используется TUI и Web.** Domain и Application не знают
  про Terminal.Gui и Razor; UI только отображает готовые снимки.
- **Тяжёлые команды выполняются по требованию.** `serverStatus`, `currentOp`,
  `$indexStats`, чтение `system.profile` не запускаются на каждое действие пользователя.

## 4. Принятые решения

1. **Метрики и профайлер — разные сущности.** `QueryObservation` описывает одно
   наблюдение выполнения. `ProfilerState` описывает серверную настройку.
2. **Четыре режима медленного логирования:** `Off`, `Client`, `ServerSlow`, `ServerAll`.
   `Client` — приложение замеряет всё и пишет Warning выше порога; `ServerSlow` —
   `setProfilingLevel(1, ...)`; `ServerAll` — `setProfilingLevel(2, ...)` для отладки.
3. **Порог по умолчанию — 100 мс.** Настраивается отдельно для клиентского лога и
   серверного `slowms`; значения синхронизируются, но могут отличаться.
4. **Профайлер включается для выбранной области:** текущая БД, явный список БД или
   все несистемные БД. Настройка применяется к каждой БД отдельно; частичные ошибки
   показываются построчно.
5. **`ServerAll` требует подтверждения** и по умолчанию автоматически выключается
   при выходе из приложения. Факт включения постоянно виден в UI.
6. **Метрики по умолчанию только в памяти.** Опционально включается сохранение в SQLite
   (7 дней / 50 000 наблюдений). Это не превращает приложение в APM-систему.
7. **Внутренние чтения не попадают в статистику.** `AutocompleteService`, загрузка
   дерева, `dbStats`, `$indexStats`, чтение профайлера передают
   `QueryExecutionOptions.Purpose = Internal`; такие наблюдения отбрасываются.
8. **`explain` работает только для распознанных `find`/`findOne`/`aggregate`.**
   Для произвольного JavaScript показывается сообщение и предложение выполнить запрос
   вручную. Объяснять write-операции в первой версии не нужно.
9. **Explain — временный результат.** Он не пишется в SQLite, историю и логи.
   Сырой JSON доступен по кнопке копирования, но не сохраняется автоматически.
10. **Новый интерфейс `IMongoDiagnosticsGateway`.** Он реализуется только driver-каналом,
    как `IMongoUserGateway`; CLI-канал `mongosh` для диагностики не используется.

## 5. Вне области

- Полноценный APM: распределённые трейсы, профилировщик CPU, алерты, интеграции с
  Prometheus/OpenTelemetry.
- Автоматический performance advisor с изменением индексов.
- Реплей запросов и сохранение их текста в статистике.
- Профайлер на уровне `mongos` и автоматическое включение на всех шардах.
- Долгосрочное хранение сырых `system.profile` документов.
- Аудит действий пользователей и immutable-журнал.
- Explain для `update`, `delete`, `updateOne`, `deleteMany` и сложных shell-сценариев.
- Управление `system.profile` (изменение размера capped-коллекции, очистка).
- Настройка MongoDB-параметров (`setParameter`, `configureFailPoint`).
- Отдельный HTTP API для диагностики: Web использует существующие Blazor-компоненты
  и защищённый контур приложения.

## 6. Пользовательские сценарии

### 6.1. Быстро включить логирование медленных запросов

1. Пользователь подключается к MongoDB и открывает **Diagnostics → Profiler**.
2. UI показывает фактическое состояние: `Off`, `Slowms 100`, `Sample rate 1.0`.
3. Пользователь выбирает режим:
   - **Client** — только приложение, без изменения сервера;
   - **Server slow** — `profile: 1`, `slowms: 100`;
   - **Server all** — `profile: 2` (с предупреждением).
4. Выбирает область: текущая БД / все БД / явный список.
5. Нажимает **Apply**. Приложение выполняет `setProfilingLevel` для каждой БД.
6. В статусе и на странице Diagnostics появляется индикатор активного профайлера.
7. Медленные запросы появляются на вкладке **Slow queries**; при включённом
   `Mirror to application log` дополнительно пишутся Warning в `app-.log`.

### 6.2. Посмотреть статистику запросов

1. Открыть **Diagnostics → Overview**.
2. Выбрать подключение, период (5 мин / 1 час / 24 часа / всё) и при необходимости
   БД/коллекцию.
3. Увидеть карточки: число выполнений, ошибки, p50/p95/p99, медленные, таймауты,
   среднее число возвращённых документов.
4. Ниже — таблица по namespace/операции и гистограмма задержек.
5. На вкладке **Slow queries** — последние медленные операции с `docsExamined`,
   `keysExamined`, `nReturned`, `planSummary` (если доступны).

### 6.3. Посмотреть состояние базы и сервера

1. Открыть **Diagnostics → Database**.
2. Выбрать БД.
3. Увидеть `dbStats`, размеры и количество документов по коллекциям, размеры индексов,
   неиспользуемые индексы (`$indexStats`), число медленных операций.
4. Открыть **Diagnostics → Server**:
   версия, uptime, соединения, opcounters, память, storage engine, репликация,
   текущие операции длиннее заданного порога.

### 6.4. Разобрать конкретный запрос через explain

1. В query-вкладке набрать `db.orders.find({ status: "new" }).sort({ createdAt: -1 })`.
2. Нажать **Explain** (TUI: `F8`; Web: кнопка рядом с Run или `F8`).
3. Приложение распознаёт `find`, выполняет `explain` с `executionStats`.
4. Открывается окно:
   - дерево стадий `IXSCAN → FETCH → SORT`;
   - метрики: `executionTimeMillis`, `nReturned`, `totalDocsExamined`,
     `totalKeysExamined`, `usedDisk`, `spills`;
   - предупреждения: `COLLSCAN`, лишние документы, in-memory sort, rejected plans.
5. Пользователь может переключить verbosity на `queryPlanner`/`allPlansExecution`
   или скопировать сырой JSON.

### 6.5. TUI и Web дают одинаковый результат

Оба интерфейса используют один `DiagnosticsService`, одни `QueryObservation` и один
парсер `explain`. Отличается только отображение: TUI — таблицы, деревья и диалоги;
Web — карточки, таблицы, модальные окна и автообновление.

## 7. Архитектура

```text
Пользовательский запрос
        |
        v
+-------------------+        +---------------------------+
| QueryService      |        | InstrumentedMongoGateway  |
| (shell/mongosh)   |        | (все driver-операции)     |
+---------+---------+        +-------------+-------------+
          |                                |
          +---------------+----------------+
                          v
                +-----------------------+
                | QueryMetricsRecorder  |
                | - классификация       |
                | - slow threshold      |
                | - кольцевой буфер     |
                +-----+-----------+-----+
                      |           |
          +-----------+           +-------------------+
          v                                           v
+-------------------+                       +----------------------+
| SlowQueryLogger   |                       | QueryMetricsStore    |
| ILogger Warning   |                       | память + SQLite      |
+-------------------+                       +----------+-----------+
                                                       |
                                                       v
                                            +----------------------+
                                            | Diagnostics UI       |
                                            | TUI / Web            |
                                            +----------+-----------+
                                                       ^
                                                       |
+-------------------+        +-------------------------+-----------+
| ExplainService    |        | DiagnosticsService                  |
| парсер + gateway  |<-------+ - profiler status/apply           |
+---------+---------+        | - dbStats/collStats/$indexStats     |
          |                  | - serverStatus/currentOp/repl       |
          v                  | - system.profile (safe projection)  |
+-------------------+        +-------------------------+-----------+
| IMongoDiagnostics |                                  |
| Gateway           |<---------------------------------+
+---------+---------+
          |
          v
      MongoDB
```

Поток данных:

1. `QueryService` (shell) и `InstrumentedMongoGateway` (driver) создают
   `QueryObservation` с длительностью, namespace, операцией, результатом и ошибкой.
2. `QueryMetricsRecorder` отбрасывает `Purpose=Internal`, обновляет кольцевой буфер
   и, если включено, ставит наблюдение в очередь записи SQLite.
3. Если режим `Client` и длительность выше порога, `SlowQueryLogger` пишет
   структурированный Warning без текста запроса.
4. `DiagnosticsService` читает серверные данные по запросу UI. `system.profile`
   проецируется только в безопасные поля и превращается в наблюдения с
   `Source=ServerProfiler`.
5. `ExplainService` разбирает скрипт, вызывает gateway и возвращает временный
   `ExplainResult`.

## 8. Модель Domain

Новая папка `src/MongoTui.Domain/Diagnostics/`.

```csharp
public enum QueryOperationKind
{
    Find, FindOne, Aggregate, Count,
    Insert, Update, Delete,
    Command, Script, Other,
}

public enum QuerySource { Driver, Shell, ServerProfiler }

public enum SlowQueryMode { Off, Client, ServerSlow, ServerAll }

public enum ExplainVerbosity { QueryPlanner, ExecutionStats, AllPlansExecution }

public sealed record QueryObservation
{
    public required Guid ProcessId { get; init; }
    public required ConnectionId ConnectionId { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required QueryOperationKind Operation { get; init; }
    public required QuerySource Source { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool Success { get; init; }
    public int? ErrorCode { get; init; }
    public long? ReturnedCount { get; init; }
    public long? DocumentsExamined { get; init; }
    public long? KeysExamined { get; init; }
    public string? PlanSummary { get; init; }
    public bool IsSlow { get; init; }
    public bool TimedOut { get; init; }
    public bool Cancelled { get; init; }
}
```

`QueryObservation` намеренно не содержит `Script`, `Filter`, `Pipeline` и `Command`.
`ProcessId` нужен, чтобы отличать наблюдения TUI и Web, работающих с одной SQLite.

```csharp
public sealed record QueryMetricsFilter
{
    public ConnectionId? ConnectionId { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public QueryOperationKind? Operation { get; init; }
    public QuerySource? Source { get; init; }
    public DateTimeOffset? Since { get; init; }
    public TimeSpan? MinimumDuration { get; init; }
    public bool SlowOnly { get; init; }
}

public sealed record LatencyPercentiles(
    double P50Ms, double P95Ms, double P99Ms, double MaxMs);

public sealed record LatencyBucket(TimeSpan UpperBound, long Count);

public sealed record QueryMetricsSummary
{
    public required long Total { get; init; }
    public required long Failed { get; init; }
    public required long Slow { get; init; }
    public required long TimedOut { get; init; }
    public required long Cancelled { get; init; }
    public required double AverageDurationMs { get; init; }
    public required LatencyPercentiles Latency { get; init; }
    public required long ReturnedDocuments { get; init; }
    public required long? DocumentsExamined { get; init; }
    public required long? KeysExamined { get; init; }
    public required IReadOnlyList<LatencyBucket> Histogram { get; init; }
}

public sealed record QueryMetricGroup
{
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required QueryOperationKind Operation { get; init; }
    public required QuerySource Source { get; init; }
    public required long Count { get; init; }
    public required long Failed { get; init; }
    public required long Slow { get; init; }
    public required double AverageDurationMs { get; init; }
    public required double P95Ms { get; init; }
}
```

Серверная диагностика:

```csharp
public enum ProfilingLevel { Off = 0, Slow = 1, All = 2 }

public sealed record ProfilerOptions
{
    public int SlowMs { get; init; } = 100;
    public double SampleRate { get; init; } = 1.0;
    public string? FilterJson { get; init; }
}

public sealed record ProfilerState(
    string Database,
    ProfilingLevel Level,
    ProfilerOptions Options,
    DateTimeOffset? ProfileCollectionCreatedAt,
    bool IsAvailable,
    string? ErrorCode);

public sealed record ProfilerEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required QueryOperationKind Operation { get; init; }
    public required TimeSpan Duration { get; init; }
    public long? DocumentsExamined { get; init; }
    public long? KeysExamined { get; init; }
    public long? ReturnedCount { get; init; }
    public string? PlanSummary { get; init; }
    public int? ErrorCode { get; init; }
}
```

Explain:

```csharp
public sealed record ExplainRequest
{
    public required CollectionRef Collection { get; init; }
    public required ExplainOperation Operation { get; init; }
    public BsonDocument? Filter { get; init; }
    public IReadOnlyList<BsonDocument>? Pipeline { get; init; }
    public BsonDocument? Sort { get; init; }
    public BsonDocument? Projection { get; init; }
    public int? Skip { get; init; }
    public int? Limit { get; init; }
    public ExplainVerbosity Verbosity { get; init; } = ExplainVerbosity.ExecutionStats;
    public TimeSpan? MaxTime { get; init; }
}

public enum ExplainOperation { Find, FindOne, Aggregate }

public sealed record ExplainNode
{
    public required string Stage { get; init; }
    public string? IndexName { get; init; }
    public long? ExecutionTimeMs { get; init; }
    public long? DocumentsExamined { get; init; }
    public long? KeysExamined { get; init; }
    public long? Returned { get; init; }
    public bool? UsedDisk { get; init; }
    public long? Spills { get; init; }
    public IReadOnlyList<ExplainNode> Children { get; init; } = [];
}

public enum ExplainWarningKind
{
    CollectionScan, HighDocumentsExaminedRatio, HighKeysExaminedRatio,
    InMemorySort, BlockingStage, RejectedPlans, HighLatency, UsedDisk,
}

public sealed record ExplainWarning(ExplainWarningKind Kind, string? Detail);

public sealed record ExplainResult
{
    public required ExplainVerbosity Verbosity { get; init; }
    public required string? Namespace { get; init; }
    public required ExplainNode Plan { get; init; }
    public required long? ExecutionTimeMs { get; init; }
    public required long? DocumentsExamined { get; init; }
    public required long? KeysExamined { get; init; }
    public required long? Returned { get; init; }
    public required IReadOnlyList<ExplainWarning> Warnings { get; init; }
    public required BsonDocument Raw { get; init; }
}
```

`ExplainResult.Raw` содержит полный ответ сервера, включая возможные значения фильтра.
Он живёт только в памяти диалога, не пишется в SQLite, историю и логи.

## 9. Application

### 9.1. `IQueryMetricsStore`

Новый интерфейс `src/MongoTui.Application/Abstractions/Diagnostics/IQueryMetricsStore.cs`:

```csharp
public interface IQueryMetricsStore
{
    void Record(QueryObservation observation);
    QueryMetricsSummary Summarize(QueryMetricsFilter filter);
    IReadOnlyList<QueryObservation> GetRecent(QueryMetricsFilter filter, int limit);
    IReadOnlyList<QueryMetricGroup> Group(QueryMetricsFilter filter, int limit);
    void Clear();
}
```

Синхронный `Record` обязателен: он вызывается в горячем пути запроса и не должен
ждать I/O. Внутри — потокобезопасный кольцевой буфер на 10 000 наблюдений.
`Clear` очищает только память; сохранённая история очищается отдельной операцией
с подтверждением.

### 9.2. `IQueryMetricsRepository`

Новый интерфейс `src/MongoTui.Application/Abstractions/Persistence/IQueryMetricsRepository.cs`:

```csharp
public interface IQueryMetricsRepository
{
    Task AddBatchAsync(IReadOnlyList<QueryObservation> observations, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueryObservation>> QueryAsync(QueryMetricsFilter filter, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueryMetricGroup>> GroupAsync(QueryMetricsFilter filter, int limit, CancellationToken cancellationToken);
    Task ClearAsync(ConnectionId? connectionId, CancellationToken cancellationToken);
    Task PruneAsync(DateTimeOffset olderThan, int maximumRows, CancellationToken cancellationToken);
}
```

`QueryMetricsStore` (Application) объединяет кольцевой буфер и репозиторий:
сначала отдаёт свежие наблюдения из памяти, затем — сохранённые, без дублей по
`(ProcessId, StartedAt, ...)`.

### 9.3. `QueryMetricsRecorder`

Новый сервис `src/MongoTui.Application/Diagnostics/QueryMetricsRecorder.cs`.

Ответственность:

- нормализовать namespace и операцию;
- отбросить `Purpose=Internal`;
- пометить `IsSlow` по актуальному порогу;
- записать наблюдение в `IQueryMetricsStore`;
- при режиме `Client` и `IsSlow` вызвать `SlowQueryLogger`.

Сервис не знает про MongoDB и UI. Ошибки записи метрик не пробрасываются в запрос:
наблюдение — побочный эффект, а не условие успеха.

### 9.4. `InstrumentedMongoGateway`

Новый декоратор `src/MongoTui.Application/Diagnostics/InstrumentedMongoGateway.cs`
(или Infrastructure, если решим не засорять Application зависимостями на gateway):

- реализует `IMongoGateway`;
- замеряет каждую driver-операцию;
- для `FindAsync`/`AggregateAsync` берёт `Documents.Count`;
- для `CountAsync` — результат;
- для `InsertOneAsync`/`ReplaceOneAsync`/`DeleteOneAsync` — affected count;
- для `List*`, `Get*Stats`, `Create*`, `Drop*` создаёт операцию `Command`
  или не записывает её, если `Purpose=Internal`;
- передаёт `Purpose` из `QueryExecutionOptions`;
- не перехватывает `IMongoDiagnosticsGateway` и `IMongoUserGateway`.

`AutocompleteService` передаёт `Purpose = Internal`, чтобы выборка 20 документов
для подсказок не искажала статистику. `DatabaseService`/`IndexService` можно
пометить как `Command` и показывать отдельной группой, а не смешивать с пользовательскими
запросами.

### 9.5. `DiagnosticsSettingsService`

Новый сервис с типизированными настройками:

```csharp
public sealed record DiagnosticsSettings
{
    public bool MetricsEnabled { get; init; } = true;
    public bool PersistMetrics { get; init; }
    public SlowQueryMode SlowMode { get; init; } = SlowQueryMode.Off;
    public int SlowThresholdMs { get; init; } = 100;
    public double SampleRate { get; init; } = 1.0;
    public int MemoryRetentionMinutes { get; init; } = 60;
    public int PersistentRetentionDays { get; init; } = 7;
    public int MaximumPersistentRows { get; init; } = 50_000;
    public bool MirrorServerSlowToApplicationLog { get; init; }
}
```

Ключи в существующей таблице `settings`:

| Ключ | Значение по умолчанию |
|---|---|
| `diagnostics.metrics.enabled` | `true` |
| `diagnostics.metrics.persist` | `false` |
| `diagnostics.slow.mode` | `Off` |
| `diagnostics.slow.thresholdMs` | `100` |
| `diagnostics.slow.sampleRate` | `1.0` |
| `diagnostics.metrics.memoryRetentionMinutes` | `60` |
| `diagnostics.metrics.persistentRetentionDays` | `7` |
| `diagnostics.metrics.maximumPersistentRows` | `50000` |
| `diagnostics.slow.mirrorServerLog` | `false` |

Валидация: порог `0..600000`, sample rate `0.0..1.0`, retention `1..365` дней,
максимум строк `1000..1000000`.

Фактическое состояние профайлера не хранится как «включено». В SQLite можно хранить
только последние выбранные параметры для предзаполнения формы:
`diagnostics.profiler.last.{connectionId}` — JSON с режимом, порогом и областью.
При открытии экрана приложение всё равно читает сервер.

### 9.6. `DiagnosticsService`

Новый сервис, объединяющий данные для UI:

```csharp
public interface IDiagnosticsService
{
    Task<IReadOnlyList<ProfilerState>> GetProfilerStatesAsync(
        MongoConnectionTarget target, IReadOnlyList<DatabaseName> databases, CancellationToken ct);

    Task<IReadOnlyList<ProfilerApplyResult>> ApplyProfilerAsync(
        MongoConnectionTarget target, ProfilerApplyRequest request, CancellationToken ct);

    Task<DatabaseOverview> GetDatabaseOverviewAsync(
        MongoConnectionTarget target, DatabaseName database, CancellationToken ct);

    Task<ServerOverview> GetServerOverviewAsync(
        MongoConnectionTarget target, CancellationToken ct);

    Task<IReadOnlyList<CurrentOperation>> GetCurrentOperationsAsync(
        MongoConnectionTarget target, TimeSpan minimumDuration, CancellationToken ct);

    Task<IReadOnlyList<ProfilerEntry>> ReadProfilerAsync(
        MongoConnectionTarget target, DatabaseName database, ProfilerReadFilter filter, CancellationToken ct);
}
```

`DatabaseOverview` содержит существующий `DatabaseStats` плюс:

- `IReadOnlyList<CollectionOverview>`: `CollectionStats` с `DataSizeBytes`,
  `StorageSizeBytes`, `IndexCount`, `IndexSizeBytes`, `AverageObjectSizeBytes`;
- `IReadOnlyList<IndexUsage>`: имя индекса, `ops`, `since`, `isUnused`;
- `IReadOnlyList<ProfilerState>` для БД;
- счётчики медленных операций из `IQueryMetricsStore`.

`ServerOverview`:

```csharp
public sealed record ServerOverview
{
    public string? Version { get; init; }
    public TimeSpan? Uptime { get; init; }
    public int? CurrentConnections { get; init; }
    public int? AvailableConnections { get; init; }
    public long? ResidentMemoryBytes { get; init; }
    public long? VirtualMemoryBytes { get; init; }
    public string? StorageEngine { get; init; }
    public IReadOnlyDictionary<string, long> OpCounters { get; init; }
    public ReplicaSetOverview? Replication { get; init; }
    public IReadOnlyList<CurrentOperation> CurrentOperations { get; init; }
    public IReadOnlyList<DiagnosticsWarning> Warnings { get; init; }
}
```

### 9.7. `ExplainService`

Новый сервис `src/MongoTui.Application/Services/ExplainService.cs`:

```csharp
public interface IExplainService
{
    ExplainRequest? TryCreateRequest(QueryExecutionContext context, ExplainVerbosity verbosity);
    Task<ExplainResult> ExplainAsync(
        MongoConnectionTarget target, ExplainRequest request, CancellationToken ct);
}
```

`TryCreateRequest` использует новый `ExplainQuerySource` (Application/Queries).
Он повторяет разбор `DocumentQuerySource`, но дополнительно понимает:

- `find(filter).sort(...).skip(n).limit(n)`;
- `findOne(filter)`;
- `aggregate([ ... ])`;
- `db.getSiblingDB("db").collection...`.

Если выражение не распознано, возвращается `null`, а UI показывает
`Explain.Unsupported`: «Explain поддерживает простые find, findOne и aggregate».

`ExplainService` добавляет предупреждения:

| Правило | Условие |
|---|---|
| `CollectionScan` | в плане есть `COLLSCAN` |
| `HighDocumentsExaminedRatio` | `docsExamined > max(100, nReturned * 10)` |
| `HighKeysExaminedRatio` | `keysExamined > max(100, nReturned * 10)` |
| `InMemorySort` | стадия `SORT` без индекса или `hasSortStage` |
| `BlockingStage` | `$group`, `$sort`, `$lookup`, `$facet` в плане |
| `RejectedPlans` | `rejectedPlans` непуст |
| `UsedDisk` | `usedDisk = true` или `spills > 0` |
| `HighLatency` | `executionTimeMillis` выше текущего порога медленного запроса |

### 9.8. `ServerProfilerMonitor`

Новый фоновый сервис нужен только для зеркалирования серверных медленных записей
в лог приложения. Если `MirrorServerSlowToApplicationLog = false`, монитор ничего
не делает.

- `ProfilerSessionRegistry` хранит активные `(connectionId, target, database)`.
- При включении `ServerSlow`/`ServerAll` UI регистрирует область.
- Монитор раз в 2 секунды читает новые записи `system.profile` по каждому
  зарегистрированному namespace, проецирует безопасные поля и передаёт их в
  `QueryMetricsRecorder` с `Source=ServerProfiler`.
- При отключении подключения или выключении режима область снимается с регистрации.
- Регистрация живёт в памяти процесса и не восстанавливается после перезапуска:
  серверное состояние остаётся, но приложение не включает ничего само.

Если зеркалирование не нужно, вкладка **Slow queries** просто читает `system.profile`
при открытии и по кнопке Refresh.

### 9.9. `QueryExecutionOptions.Purpose`

Добавить в `src/MongoTui.Domain/Queries/QueryExecutionOptions.cs`:

```csharp
public enum QueryPurpose { User, Internal }

public sealed record QueryExecutionOptions
{
    // существующие поля
    public QueryPurpose Purpose { get; init; } = QueryPurpose.User;
}
```

`AutocompleteService.GetFieldsAsync` передаёт `Purpose = Internal`.
Диагностические чтения (`dbStats`, `$indexStats`, `system.profile`, `serverStatus`)
идут через `IMongoDiagnosticsGateway` и не попадают в `IMongoGateway`,
поэтому не требуют отдельной пометки.

## 10. Infrastructure

### 10.1. `IMongoDiagnosticsGateway`

Новый интерфейс `src/MongoTui.Application/Abstractions/Mongo/IMongoDiagnosticsGateway.cs`:

```csharp
public interface IMongoDiagnosticsGateway
{
    Task<ProfilerState> GetProfilerStateAsync(
        MongoConnectionTarget target, DatabaseName database, CancellationToken ct);

    Task<ProfilerState> SetProfilerLevelAsync(
        MongoConnectionTarget target, DatabaseName database,
        ProfilingLevel level, ProfilerOptions options, CancellationToken ct);

    Task<IReadOnlyList<ProfilerEntry>> ReadProfilerAsync(
        MongoConnectionTarget target, DatabaseName database,
        DateTimeOffset? since, TimeSpan? minimumDuration, int limit, CancellationToken ct);

    Task<ServerOverview> GetServerStatusAsync(
        MongoConnectionTarget target, CancellationToken ct);

    Task<IReadOnlyList<CurrentOperation>> GetCurrentOperationsAsync(
        MongoConnectionTarget target, TimeSpan minimumDuration, CancellationToken ct);

    Task<IReadOnlyList<IndexUsage>> GetIndexUsageAsync(
        MongoConnectionTarget target, CollectionRef collection, CancellationToken ct);

    Task<CollectionStats> GetDetailedCollectionStatsAsync(
        MongoConnectionTarget target, CollectionRef collection, CancellationToken ct);

    Task<ExplainResult> ExplainAsync(
        MongoConnectionTarget target, ExplainRequest request, CancellationToken ct);
}
```

`DriverMongoGateway` реализует интерфейс. `CompositeMongoGateway` делегирует
`IMongoDiagnosticsGateway` в `IDriverMongoGateway`; CLI-канал не участвует.
`InfrastructureServiceCollectionExtensions` регистрирует
`IMongoDiagnosticsGateway → CompositeMongoGateway`.

### 10.2. Команды MongoDB

| Данные | Команда |
|---|---|
| Состояние профайлера | `{ profile: -1 }` в выбранной БД |
| Включение профайлера | `{ profile: 1 \| 2, slowms: 100, sampleRate: 1.0, filter: {...} }` |
| Чтение записей | `find` по `system.profile`, фильтр `ts`, `millis`, сортировка `ts` |
| Статистика БД | `{ dbStats: 1 }` (уже используется) |
| Статистика коллекции | `{ collStats: "name", scale: 1 }` |
| Использование индексов | `aggregate: "name", pipeline: [{ $indexStats: {} }], cursor: {}` |
| Состояние сервера | `{ serverStatus: 1 }` |
| Текущие операции | `{ currentOp: 1, active: true, secs_running: { $gte: n } }` |
| Репликация | `{ replSetGetStatus: 1 }`, ошибка «not running with --replSet» не считается фатальной |
| Explain find | `{ explain: { find: "name", filter: {...}, sort: {...}, skip, limit }, verbosity, maxTimeMS }` |
| Explain aggregate | `{ explain: { aggregate: "name", pipeline: [...], cursor: {} }, verbosity, maxTimeMS }` |

Все имена команд и полей добавляются в `MongoWireNames`, а не пишутся строками
по месту. Для `system.profile` используется **проекция только безопасных полей**:

```csharp
new BsonDocument
{
    { "ts", 1 }, { "op", 1 }, { "ns", 1 }, { "millis", 1 },
    { "docsExamined", 1 }, { "keysExamined", 1 }, { "nreturned", 1 },
    { "planSummary", 1 }, { "errCode", 1 }, { "responseLength", 1 },
}
```

Поля `command`, `query`, `originatingCommand`, `client`, `user` и любые значения
фильтра не читаются и не сохраняются.

### 10.3. Разбор `system.profile`

`ns` вида `db.collection` разбивается на `Database`/`Collection`.
`op` маппится в `QueryOperationKind`:

| `op` | Значение |
|---|---|
| `query` | `Find` |
| `getmore` | `Find` |
| `command` | `Command` или `Aggregate` по `planSummary`/`command` (без сохранения текста) |
| `insert` | `Insert` |
| `update` | `Update` |
| `remove` | `Delete` |

Если тип операции определить нельзя, используется `Other`.
`planSummary` сохраняется как короткая строка (`IXSCAN { status: 1 }`), потому что
она описывает план, а не данные.

### 10.4. SQLite

Добавить миграцию в `SqliteDatabase.MigrationSql`:

```sql
CREATE TABLE IF NOT EXISTS query_observations (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    process_id    TEXT NOT NULL,
    connection_id TEXT NOT NULL,
    database      TEXT,
    collection    TEXT,
    operation     TEXT NOT NULL,
    source        TEXT NOT NULL,
    started_at    TEXT NOT NULL,
    duration_ms   REAL NOT NULL,
    success       INTEGER NOT NULL,
    error_code    INTEGER,
    returned_count INTEGER,
    docs_examined INTEGER,
    keys_examined INTEGER,
    plan_summary  TEXT,
    slow          INTEGER NOT NULL,
    timed_out     INTEGER NOT NULL,
    cancelled     INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_query_observations_started
    ON query_observations(started_at);
CREATE INDEX IF NOT EXISTS ix_query_observations_namespace
    ON query_observations(connection_id, database, collection, started_at);
CREATE INDEX IF NOT EXISTS ix_query_observations_slow
    ON query_observations(slow, started_at);
```

Новые классы:

- `QueryObservationRow` — маппинг таблицы;
- `MongoTuiDataConnection.QueryObservations`;
- `SqliteQueryMetricsRepository` — batch insert, выборка, группировка, очистка, prune.

Сохранение выключено по умолчанию. При включении:

- наблюдения копятся в ограниченной очереди (4096);
- фоновый `QueryMetricsFlushService` раз в 2 секунды пишет батч в одной транзакции;
- при переполнении очереди старые наблюдения отбрасываются, счётчик потерь
  показывается в Diagnostics;
- при старте и раз в час выполняется `PruneAsync` по возрасту и количеству строк;
- при завершении приложения очередь дописывается best-effort.

### 10.5. Фоновая запись и TUI

В TUI контейнер создаётся вручную и уже запускает `LoggingSettingsMonitor`.
`QueryMetricsFlushService` регистрируется как `IHostedService`; в `Program.cs`
TUI-ветка запускает и останавливает его так же, как монитор логирования.
В Web `WebHost` запускает hosted services автоматически.

`ServerProfilerMonitor` тоже регистрируется как hosted service, но активируется
только при наличии зарегистрированных областей.

### 10.6. Настройки и TUI/Web

`DiagnosticsSettingsService` читает настройки из `ISettingsStore`.
Для горячего пути медленного лога `QueryMetricsRecorder` держит кэш настроек
на 3 секунды. `LoggingSettingsMonitor` расширяется до обновления и логирования,
и диагностических настроек, либо рядом добавляется `DiagnosticsSettingsMonitor`.
Изменения из другого процесса подхватываются в течение 3 секунд, как сейчас
с уровнем логов.

## 11. Метрики и вычисления

### 11.1. Процентили

Для кольцевого буфера:
- сортировка длительностей выбранного окна;
- `p50/p95/p99` по nearest-rank;
- максимум;
- гистограмма по фиксированным границам:
  `1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, >10000` мс.

Для сохранённой истории процентили считаются по тем же наблюдениям SQL-запросом.
Ограничение в 50 000 строк не даёт памяти и БД расти бесконечно.

### 11.2. Производные показатели

- `ErrorRate = Failed / Total`;
- `SlowRate = Slow / Total`;
- `Throughput = Total / окно` (ops/s);
- `DocumentsExaminedPerReturned` — только если есть серверные данные;
- `Cache/Index efficiency` — предупреждение, если `docsExamined / returned > 10`.

### 11.3. Дедупликация источников

Клиентские и серверные наблюдения не смешиваются в одной строке:

- `Source=Driver` — длительность приложения, returned count;
- `Source=Shell` — длительность `mongosh`, returned count;
- `Source=ServerProfiler` — серверные `millis`, `docsExamined`, `keysExamined`.

В таблице показывается колонка **Source**. Пользователь сам решает, смотреть
клиентскую задержку или серверную. Explain — отдельный, не сохраняемый результат.

## 12. Логирование и приватность

### 12.1. Медленный лог приложения

Категория: `MongoTui.Application.Diagnostics.SlowQueryLogger`.

Шаблон сообщения:

```text
Slow query: {Database}.{Collection} {Operation} took {DurationMs:0.0} ms
({Source}, returned {ReturnedCount}, docs examined {DocumentsExamined}, keys examined {KeysExamined})
```

Структурированные поля добавляются в `LogPropertyNames`:

```text
Database, Collection, Operation, Source, DurationMs, ReturnedCount,
DocumentsExamined, KeysExamined, PlanSummary, ErrorCode
```

`SlowQueryLogger` пишет `LogWarning`. Текст запроса не передаётся даже на уровне
`Verbose`. `PlanSummary` содержит имена индексов и стадий, а не значения фильтра.

### 12.2. Зеркалирование серверного профайлера

Только при `diagnostics.slow.mirrorServerLog=true`. Записи `system.profile`
преобразуются в безопасные поля до попадания в логгер. Поле `command` никогда
не читается из БД. Дубли клиентского и серверного лога не устраняются полностью:
это разные источники, это явно указано в UI.

### 12.3. Explain и данные

- Explain выполняется только по явному действию.
- `ExplainResult.Raw` не сериализуется в SQLite и не попадает в `ILogger`.
- Дерево плана и предупреждения показываются в UI.
- Копирование JSON в clipboard — явное действие пользователя.
- В логи попадают только факты: namespace, операция, длительность, код ошибки,
  тип предупреждения.

### 12.4. Права и ошибки

| Операция | Минимальные права |
|---|---|
| `setProfilingLevel`, `$indexStats`, `collStats` | `dbAdmin`/`dbOwner` на БД |
| `serverStatus`, `currentOp`, `replSetGetStatus` | `clusterMonitor`, `clusterAdmin` или `root` |
| `explain` | те же права, что на обычное чтение коллекции |

Ошибка прав (`code: 13`) маппится в локализованное сообщение
`Diagnostics.Error.Unauthorized`; остальные ошибки — `Diagnostics.Error.Command`.
Ни одна ошибка диагностики не должна закрывать сессию или блокировать query tabs.

### 12.5. Ограничения MongoDB

- Профайлер настраивается **на каждой базе отдельно**. На `mongos` команда
  `setProfilingLevel` недоступна; приложение показывает пояснение и предлагает
  выполнить настройку на `mongod`/шардах.
- `system.profile` — capped-коллекция. Старые записи исчезают автоматически.
  Смена уровня может пересоздать коллекцию на некоторых версиях; UI предупреждает,
  что история профайлера недолговечна.
- `level 2` заметно увеличивает нагрузку и объём записи. Включать только на время.
- `$indexStats` сбрасывается при перезапуске сервера и не показывает «никогда
  не использованный» индекс абсолютно достоверно.
- На Atlas и в managed-окружениях часть команд может быть запрещена политикой.

## 13. TUI

### 13.1. Меню и горячие клавиши

`MainWindow.BuildMenuItems` (`src/MongoTui.TUI/Views/MainWindow.cs:359`) получает:

| Меню | Пункт | Действие |
|---|---|---|
| Query | `Explain current query` | `F8`, открыть `ExplainDialog` |
| Query | `Query insights...` | открыть `QueryInsightsDialog` |
| Settings | `Diagnostics...` | открыть `DiagnosticsDialog` |
| Settings | `Logging...` | оставить существующий |
| Connection context | `Diagnostics...` | сводка по подключению |
| Database context | `Query insights...` | статистика по БД |
| Database context | `Profiler...` | настройка профайлера для БД |
| Collection context | `Explain sample query...` | открыть explain для `find({}).limit(50)` |

`F8` не занят существующими действиями (`F5` — run, `F6` — AI, `F7` — translate,
`F9` — refresh, `F10` — menu). В Web `F8` добавляется как альтернатива кнопке.

### 13.2. `DiagnosticsDialog`

Новый файл `src/MongoTui.TUI/Views/DiagnosticsDialog.cs`.

- Заголовок `Diagnostics.Title` + имя подключения.
- `TabView`:
  1. **Overview** — карточки-строки (всего, ошибки, p50/p95/p99, медленные),
     ASCII-гистограмма, таблица групп.
  2. **Slow queries** — таблица последних медленных наблюдений.
  3. **Server** — `serverStatus`, текущие операции, репликация.
  4. **Profiler** — фактическое состояние и кнопка настройки.
- Кнопки `Refresh`, `Settings...`, `Close`.
- Ошибки загрузки — в статусной строке диалога, без закрытия.
- Автообновление по `Application.AddTimeout` каждые 2 секунды, пока диалог открыт.
  При закрытии таймер снимается.

Класс не должен превышать 300 строк: каждая вкладка выносится в отдельный
`DiagnosticsOverviewTab`, `SlowQueriesTab`, `ServerHealthTab`, `ProfilerTab`.

### 13.3. `QueryInsightsDialog`

Новый файл `src/MongoTui.TUI/Views/QueryInsightsDialog.cs`.

- Фильтры: подключение, БД, коллекция, операция, источник, период, `slow only`.
- Таблица групп: namespace, операция, источник, count, error %, avg, p95, slow.
- Двойной клик по группе открывает отфильтрованный список наблюдений.
- Кнопки `Refresh`, `Clear in-memory`, `Close`.
- `Clear in-memory` не трогает SQLite; очистка сохранённой истории — отдельная
  команда с подтверждением.

### 13.4. `SlowQueriesDialog`

Новый файл `src/MongoTui.TUI/Views/SlowQueriesDialog.cs`.

- Источник: память приложения и/или `system.profile` выбранной БД.
- Колонки: время, БД, коллекция, операция, мс, docs, keys, returned, plan, source.
- Переключатель `Follow` (обновление каждые 2 секунды).
- Фильтр минимальной длительности и количества строк.
- `Enter` открывает детали наблюдения.
- Очистка `system.profile` не выполняется: это серверная capped-коллекция.

### 13.5. `ProfilerDialog`

Новый файл `src/MongoTui.TUI/Views/ProfilerDialog.cs`.

Поля:

| Поле | Значение |
|---|---|
| Mode | `Off`, `Client`, `Server slow`, `Server all` |
| Scope | текущая БД / все несистемные БД / список БД |
| Slow threshold | мс, по умолчанию 100 |
| Sample rate | 0.0–1.0, по умолчанию 1.0 |
| Advanced filter | JSON, только для серверных режимов |
| Mirror to application log | чекбокс |
| Auto-disable on exit | включён для `ServerAll` |
| Auto-disable after | 15 / 30 / 60 минут / без ограничения |

После `Apply` показывается таблица фактических состояний по каждой БД:
`database`, `requested`, `actual`, `result`. Частичный успех не откатывает
успешные БД; пользователь видит, где применить не удалось.

При выборе `Server all` — обязательное `ConfirmDialog` с предупреждением о нагрузке
и о том, что настройка сохраняется на сервере.

### 13.6. `ExplainDialog`

Новый файл `src/MongoTui.TUI/Views/ExplainDialog.cs`.

- Левая панель — `TreeView` плана: стадии, индексы, метрики.
- Правая панель — сводка: verbosity, namespace, executionTime, docs/keys/returned,
  usedDisk, spills.
- Нижняя панель — предупреждения с цветовой маркировкой.
- Кнопки: `Query planner`, `Execution stats`, `All plans`, `Copy JSON`, `Re-run`, `Close`.
- `Esc` закрывает без сохранения.
- Если запрос не распознан, показывается локализованная подсказка и кнопка `Close`.

Результат не пишется в историю и не становится частью `QueryResult`.

### 13.7. Индикатор активного профайлера

В статусной строке `MainWindow` при активном серверном профайлере:

```text
DIAG: profiler level 1, slowms 100 — shop, orders (2 DB)
```

Индикатор обновляется при применении настроек и при переключении подключения.
Он нужен, чтобы пользователь не забыл выключить `ServerAll`.

## 14. Web

### 14.1. Навигация

В `Workbench.razor` добавить `MainPage.Diagnostics` и пункт навигации
`Nav.Diagnostics` с иконкой `Activity` (новая `IconNames.Activity`).
Порядок: Workspace, Connections, History, Diagnostics, Logs, Settings.

Страница доступна только внутри `SecurityGate`, как и остальные. Новых публичных
HTTP endpoint не появляется.

### 14.2. `DiagnosticsPage.razor`

Новый файл `src/MongoTui.Web/Components/Panels/DiagnosticsPage.razor`.

Содержимое:

- селектор подключения (только активные) и БД;
- переключатель периода 5м / 1ч / 24ч / всё;
- карточки `Total`, `Errors`, `p50`, `p95`, `p99`, `Slow`, `Timeouts`;
- гистограмма задержек (CSS bars, без внешних библиотек);
- таблица групп с сортировкой по count/avg/p95/slow;
- вкладки/секции `Slow queries`, `Database`, `Server`, `Profiler`.

Пустое состояние: «Нет активного подключения» и кнопка перехода в Connections.
При отсутствии наблюдений: «Пока нет данных» и подсказка включить сбор.

### 14.3. Компоненты

| Компонент | Назначение |
|---|---|
| `QueryInsightsPanel.razor` | карточки, гистограмма, фильтры, таблица групп |
| `SlowQueriesPanel.razor` | последние медленные наблюдения, follow, фильтр |
| `DatabaseDiagnosticsPanel.razor` | `dbStats`, коллекции, размеры индексов, `$indexStats` |
| `ServerHealthPanel.razor` | `serverStatus`, `currentOp`, репликация |
| `ProfilerPanel.razor` | фактическое состояние и применение режима |
| `ExplainDialog.razor` | дерево плана, метрики, предупреждения, raw JSON |
| `LatencyHistogram.razor` | горизонтальные бары по buckets |
| `MetricCard.razor` | карточка числа с подписью и подсказкой |
| `QueryObservationTable.razor` | общая таблица наблюдений |

Стили — в `wwwroot/app.css`, в духе существующих settings/logs panels.
Иконки — через `IconNames` и `Icon.razor`.

### 14.4. Explain в Web

- В `QueryArea.razor` рядом с Run/Cancel добавить кнопку `Explain` (иконка `Activity`).
- В `CodeEditor` добавить keybinding `F8` → `ExplainAsync`.
- В browse-режиме кнопка объясняет текущий фильтр.
- `ExplainDialog` открывается модально, не заменяет результаты.
- Если запрос не распознан — `FormFeedback` с `Explain.Unsupported`.
- Результат не сохраняется в `WebTab` и не попадает в `Workspace.QueryRequest`.

### 14.5. Автообновление и отмена

- Diagnostics page: обновление раз в 5 секунд, только пока страница открыта.
- Slow queries: follow раз в 2 секунды, как `LogsPanel`.
- Все запросы получают `CancellationTokenSource` и отменяются при уходе со страницы
  или закрытии модального окна.
- Поздний ответ не должен перезаписывать более новый выбор подключения/БД.
- Кнопки сохраняют подпись и размер; индикатор загрузки — через существующий
  `BusyButton`/`BusyIcon`.
- Ошибка одной секции не скрывает остальные: каждая секция показывает свою ошибку.

### 14.6. Web-предупреждение о профайлере

Если на выбранном подключении фактический уровень профайлера выше `Off`,
в `Workbench.razor` показывается постоянная полоса:

```text
Profiler is ON for shop: level 1, slowms 100. [Open diagnostics] [Disable]
```

Кнопка `Disable` выполняет `ApplyProfilerAsync(Off)` с подтверждением.
Полоса не блокирует работу с запросами.

## 15. Explain: разбор, отображение, автодополнение

### 15.1. Поддерживаемый синтаксис

```javascript
db.orders.find({ status: "new" }).sort({ createdAt: -1 }).limit(50)
db.orders.findOne({ _id: ObjectId("...") })
db.orders.aggregate([{ $match: { status: "new" } }, { $group: { _id: "$customerId", n: { $sum: 1 } } }])
db.getSiblingDB("shop").orders.find({})
```

Не поддерживается:

```javascript
const x = 1; db.orders.find({ x });
db.orders.updateMany({}, { $set: { seen: true } });
db.orders.find({}).forEach(printjson);
```

Для них — `Explain.Unsupported`. Это осознанное ограничение: приложение не
исполняет произвольный JavaScript ради explain.

### 15.2. Выполнение

- `find`: команда `explain` с вложенным `find`, `filter`, `sort`, `skip`, `limit`,
  `maxTimeMS`.
- `findOne`: `find` с `limit: 1`; результат помечается как `FindOne`.
- `aggregate`: `explain` с вложенным `aggregate` и `cursor: {}`.
- Verbosity по умолчанию `executionStats`; переключатель в диалоге.
- `maxTimeMS` берётся из `SettingsService.GetQueryTimeoutSecondsAsync`; при `0`
  используется безопасный предел UI (30 секунд), чтобы explain не висел бесконечно.
- Ошибка таймаута маппится в `QueryTimeoutException` и показывается как
  `Query.TimedOut`; ошибка прав — `Diagnostics.Error.Unauthorized`.

### 15.3. Дерево плана

Из `queryPlanner.winningPlan` (и `executionStats.executionStages`) строится
`ExplainNode`:

- `stage`, `indexName`, `direction`;
- `executionTimeMillisEstimate` или `executionTimeMillis`;
- `docsExamined`, `keysExamined`, `nReturned`;
- `usedDisk`, `spills`;
- дочерние стадии в порядке вложения.

Если сервер вернул только `winningPlan` (verbosity `queryPlanner`), метрики
исполнения показываются как `—`, а в диалоге остаётся переключатель на
`executionStats`.

### 15.4. Автодополнение

TUI (`src/MongoTui.TUI/Editor/QueryMethodCatalog.cs`):

```csharp
// Collection scope
new("explain", "\"executionStats\"", 2, QueryMethodScope.Collection)

// Cursor scope
new("explain", "\"executionStats\"", 2)
```

`Snippets.cs`:

```csharp
new Snippet("explain execution stats",
    "db.collection.find({}).explain(\"executionStats\")"),
new Snippet("explain aggregate",
    "db.collection.explain(\"executionStats\").aggregate([ { $match: {} } ])"),
```

Web (`src/MongoTui.Web/ClientAssets/editor.js`):

- расширить список методов `explain`;
- для `explain` применять вставку `explain("executionStats")`, а не `explain()`;
- после сборки обновить `wwwroot/editor.js` штатным `npm run build`.

Дополнительно в TUI `QueryMethodCatalog` можно добавить `getProfilingStatus`
в scope Database, но это не обязательно для первой версии.

### 15.5. Точки входа

- TUI: `F8`, пункт меню Query, контекстное меню коллекции.
- Web: кнопка `Explain` в `QueryArea`, `F8` в редакторе, кнопка в
  `CollectionPanel` для текущего фильтра.
- Explain всегда выполняется через driver; `mongosh` не используется, поэтому
  результат стабилен и не зависит от форматирования shell.

## 16. Локализация

Добавить ключи в `LocalizationKeys` (Application) и строки в
`src/MongoTui.TUI/Resources/Strings.resx`, `Strings.ru.resx`,
`src/MongoTui.Web/Resources/Strings.resx`, `Strings.ru.resx`.

```text
Nav.Diagnostics
Menu.QueryExplain
Menu.QueryInsights
Menu.SettingsDiagnostics
Tree.QueryInsights
Tree.Profiler
Tree.ExplainSample

Diagnostics.Title
Diagnostics.Overview
Diagnostics.SlowQueries
Diagnostics.Server
Diagnostics.Profiler
Diagnostics.Database
Diagnostics.Refresh
Diagnostics.Empty
Diagnostics.NoConnection
Diagnostics.LoadFailed
Diagnostics.PartialFailure
Diagnostics.Enabled
Diagnostics.Disabled
Diagnostics.ProfilerOn
Diagnostics.ProfilerOff
Diagnostics.ProfilerLevel
Diagnostics.ProfilerSlowMs
Diagnostics.ProfilerSampleRate
Diagnostics.ProfilerScope
Diagnostics.ProfilerScopeCurrent
Diagnostics.ProfilerScopeAll
Diagnostics.ProfilerScopeList
Diagnostics.ProfilerFilter
Diagnostics.ProfilerMirrorLog
Diagnostics.ProfilerAutoDisable
Diagnostics.ProfilerAutoDisableAfter
Diagnostics.ProfilerWarningAll
Diagnostics.ProfilerWarningPersistent
Diagnostics.ProfilerApply
Diagnostics.ProfilerDisable
Diagnostics.ProfilerActual
Diagnostics.ProfilerRequested
Diagnostics.ProfilerUnavailableMongos
Diagnostics.MetricsPersist
Diagnostics.MetricsRetention
Diagnostics.MetricsMemoryOnly
Diagnostics.MetricsLost
Diagnostics.CurrentOperations
Diagnostics.NoCurrentOperations
Diagnostics.Connections
Diagnostics.OpCounters
Diagnostics.Memory
Diagnostics.StorageEngine
Diagnostics.Replication
Diagnostics.ReplicationNotConfigured
Diagnostics.Uptime
Diagnostics.Version
Diagnostics.UnusedIndexes
Diagnostics.IndexUsage
Diagnostics.IndexNeverUsed
Diagnostics.IndexUsageSinceReset

Diagnostics.Summary.Total
Diagnostics.Summary.Errors
Diagnostics.Summary.Slow
Diagnostics.Summary.Timeouts
Diagnostics.Summary.Cancelled
Diagnostics.Summary.Average
Diagnostics.Summary.P50
Diagnostics.Summary.P95
Diagnostics.Summary.P99
Diagnostics.Summary.Max
Diagnostics.Summary.Returned
Diagnostics.Summary.DocsExamined
Diagnostics.Summary.KeysExamined

Diagnostics.Column.Time
Diagnostics.Column.Database
Diagnostics.Column.Collection
Diagnostics.Column.Operation
Diagnostics.Column.Source
Diagnostics.Column.Duration
Diagnostics.Column.Returned
Diagnostics.Column.DocsExamined
Diagnostics.Column.KeysExamined
Diagnostics.Column.Plan
Diagnostics.Column.Error
Diagnostics.Column.Count
Diagnostics.Column.Average
Diagnostics.Column.P95
Diagnostics.Column.Slow

Explain.Title
Explain.Verbosity
Explain.QueryPlanner
Explain.ExecutionStats
Explain.AllPlans
Explain.Run
Explain.ReRun
Explain.CopyJson
Explain.Copied
Explain.Unsupported
Explain.NoIndex
Explain.CollectionScan
Explain.HighDocumentsExamined
Explain.HighKeysExamined
Explain.InMemorySort
Explain.BlockingStage
Explain.RejectedPlans
Explain.HighLatency
Explain.UsedDisk
Explain.Metrics
Explain.RawJson
Explain.EmptyPlan
```

Для `ExplainWarningKind` маппинг в строки выполняется в UI-слое, а не в Domain.

## 17. Изменения по файлам

### Domain

| Файл | Изменение |
|---|---|
| `Diagnostics/QueryOperationKind.cs` | новый enum |
| `Diagnostics/QuerySource.cs` | новый enum |
| `Diagnostics/SlowQueryMode.cs` | новый enum |
| `Diagnostics/QueryObservation.cs` | новый record |
| `Diagnostics/QueryMetricsFilter.cs`, `QueryMetricsSummary.cs`, `QueryMetricGroup.cs` | новые record |
| `Diagnostics/ProfilerState.cs`, `ProfilerOptions.cs`, `ProfilerEntry.cs` | новые record |
| `Diagnostics/ExplainRequest.cs`, `ExplainNode.cs`, `ExplainResult.cs`, `ExplainWarning.cs` | новые record |
| `Diagnostics/DatabaseOverview.cs`, `ServerOverview.cs`, `CurrentOperation.cs`, `IndexUsage.cs` | новые record |
| `Queries/QueryExecutionOptions.cs` | `Purpose` |

### Application

| Файл | Изменение |
|---|---|
| `Abstractions/Diagnostics/IQueryMetricsStore.cs` | новый интерфейс |
| `Abstractions/Persistence/IQueryMetricsRepository.cs` | новый интерфейс |
| `Abstractions/Mongo/IMongoDiagnosticsGateway.cs` | новый интерфейс |
| `Diagnostics/QueryMetricsRecorder.cs` | новый сервис |
| `Diagnostics/QueryMetricsStore.cs` | кольцевой буфер и агрегаты |
| `Diagnostics/SlowQueryLogger.cs` | новый логгер |
| `Diagnostics/InstrumentedMongoGateway.cs` | декоратор gateway |
| `Services/DiagnosticsSettingsService.cs` | настройки `diagnostics.*` |
| `Services/DiagnosticsService.cs` | серверная и БД-диагностика |
| `Services/ExplainService.cs` | explain |
| `Queries/ExplainQuerySource.cs` | разбор find/findOne/aggregate |
| `Services/QueryService.cs` | передача наблюдений в recorder |
| `Services/AutocompleteService.cs` | `Purpose = Internal` |
| `DependencyInjection/ApplicationServiceCollectionExtensions.cs` | регистрация сервисов |
| `Localization/LocalizationKeys.Diagnostics.cs` | новые ключи |
| `Diagnostics/LogPropertyNames.cs` | новые поля лога |

### Infrastructure

| Файл | Изменение |
|---|---|
| `Mongo/Driver/DriverMongoGateway.cs` | `IMongoDiagnosticsGateway`, explain, profiler, serverStatus, currentOp, indexStats |
| `Mongo/Driver/MongoWireNames.cs` | имена команд и полей |
| `Mongo/Driver/MongoProfilerReader.cs` | безопасная проекция `system.profile` |
| `Mongo/Driver/MongoExplainMapper.cs` | ответ explain → `ExplainResult` |
| `Mongo/Cli/CompositeMongoGateway.cs` | делегирование диагностики |
| `Diagnostics/QueryMetricsFlushService.cs` | фоновая запись SQLite |
| `Diagnostics/ServerProfilerMonitor.cs` | зеркалирование серверного лога |
| `Diagnostics/ProfilerSessionRegistry.cs` | активные области профайлера |
| `Persistence/Sqlite/QueryObservationRow.cs` | строка таблицы |
| `Persistence/Sqlite/SqliteQueryMetricsRepository.cs` | репозиторий |
| `Persistence/Sqlite/SqliteDatabase.cs` | миграция |
| `Persistence/Sqlite/MongoTuiDataConnection.cs` | `ITable<QueryObservationRow>` |
| `DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | регистрация |

### TUI

| Файл | Изменение |
|---|---|
| `Views/MainWindow.cs` | пункты меню, `F8`, контекстные действия |
| `Views/MainWindow.Diagnostics.cs` | открытие диалогов |
| `Views/DiagnosticsDialog.cs` | новый |
| `Views/DiagnosticsOverviewTab.cs`, `SlowQueriesTab.cs`, `ServerHealthTab.cs`, `ProfilerTab.cs` | новые вкладки |
| `Views/QueryInsightsDialog.cs` | новый |
| `Views/SlowQueriesDialog.cs` | новый |
| `Views/ProfilerDialog.cs` | новый |
| `Views/ExplainDialog.cs` | новый |
| `Editor/QueryMethodCatalog.cs` | `explain` |
| `Editor/Snippets.cs` | сниппеты explain |
| `Resources/Strings.resx`, `Strings.ru.resx` | строки |
| `Views/ShortcutsDialog.cs` | `F8` |

### Web

| Файл | Изменение |
|---|---|
| `Components/Workbench.razor` | `MainPage.Diagnostics`, баннер профайлера |
| `Components/Panels/DiagnosticsPage.razor` | новый |
| `Components/Panels/QueryInsightsPanel.razor` | новый |
| `Components/Panels/SlowQueriesPanel.razor` | новый |
| `Components/Panels/DatabaseDiagnosticsPanel.razor` | новый |
| `Components/Panels/ServerHealthPanel.razor` | новый |
| `Components/Panels/ProfilerPanel.razor` | новый |
| `Components/Panels/ExplainDialog.razor` | новый |
| `Components/Panels/QueryArea.razor` | кнопка Explain |
| `Components/Panels/CollectionPanel.razor` | ссылка на диагностику/explain |
| `Components/Shared/LatencyHistogram.razor`, `MetricCard.razor` | новые |
| `Services/ExplainCoordinator.cs` | вызов explain из UI |
| `Services/IconNames.cs` | `Activity` |
| `ClientAssets/editor.js` | `explain` и keybinding |
| `wwwroot/editor.js` | пересобрать |
| `wwwroot/app.css` | стили диагностики |
| `Resources/Strings.resx`, `Strings.ru.resx` | строки |

### Корневые файлы

- `docs/logging.md` — раздел о медленном логе и зеркалировании.
- `docs/web-interface.md` — новая страница Diagnostics и Explain.
- `README.md`, `README_RU.md` — возможности администрирования и отладки.
- `CHANGELOG.md` — запись после реализации.
- `docs/hardcoded-values.md` — новые имена команд/полей и пределы.
- `docs/admin-debugging-design.md` — этот документ.

## 18. Тестирование

### 18.1. Domain / Application

- `QueryMetricsStoreTests`:
  - сумма, ошибки, slow, таймауты;
  - p50/p95/p99 на известном наборе;
  - гистограмма и границы;
  - фильтр по БД/коллекции/операции/источнику/периоду;
  - `Internal` наблюдения не попадают в выборку;
  - кольцевой буфер не растёт выше лимита;
  - `Clear` очищает только память.
- `SlowQueryLoggerTests`:
  - Warning только выше порога;
  - режим `Off` не пишет;
  - текст запроса/фильтра отсутствует в сообщении и свойствах;
  - `PlanSummary` не содержит значений документа.
- `ExplainQuerySourceTests`:
  - find/findOne/aggregate, sort/skip/limit, getSiblingDB;
  - отказ для write, переменных, forEach;
  - некорректный JSON даёт `null`, а не исключение.
- `ExplainWarningTests`:
  - COLLSCAN;
  - docsExamined/returned > 10;
  - SORT/usedDisk/rejectedPlans.
- `DiagnosticsSettingsServiceTests`:
  - значения по умолчанию;
  - границы порога и sample rate;
  - повреждённые значения в SQLite не ломают приложение.
- `InstrumentedMongoGatewayTests`:
  - каждая операция даёт одно наблюдение;
  - `Purpose=Internal` отбрасывается;
  - ошибка gateway всё равно даёт наблюдение с `Success=false`;
  - метрики не изменяют возвращаемый `QueryResult`.
- `QueryMetricsRecorderTests`: параллельная запись не теряет счётчики и не падает.

### 18.2. Infrastructure

- `MongoProfilerReaderTests`: проекция не читает `command`/`query`;
  неизвестный `op` → `Other`; `ns` без коллекции не падает.
- `DriverMongoDiagnosticsTests` (Testcontainers):
  - `GetProfilerStateAsync` возвращает `Off`;
  - `SetProfilerLevelAsync(1, 50)` и обратное выключение;
  - медленный `find` появляется в `system.profile`;
  - `level 2` и `sampleRate`;
  - `ReadProfilerAsync` не возвращает текст команды;
  - `GetServerStatusAsync`, `GetCurrentOperationsAsync`, `GetIndexUsageAsync`;
  - explain find без индекса даёт `COLLSCAN`;
  - explain find с индексом даёт `IXSCAN`;
  - explain aggregate;
  - ошибка прав маппится в локализованную доменную ошибку.
- `SqliteQueryMetricsRepositoryTests`:
  - batch insert;
  - фильтры и группировка;
  - prune по времени и количеству;
  - отсутствие колонок с текстом запроса.
- `QueryMetricsFlushServiceTests`: батч, отмена, переполнение очереди.

### 18.3. TUI

- `DiagnosticsDialogTests`: вкладки, загрузка, ошибка, пустое состояние.
- `ProfilerDialogTests`: режимы, подтверждение `ServerAll`, частичный успех,
  отображение фактического состояния, блокировка Apply без прав.
- `ExplainDialogTests`: дерево, метрики, предупреждения, неподдерживаемый запрос.
- `MainWindowDiagnosticsTests`: пункты меню, `F8`, контекстные действия,
  индикатор профайлера.
- `QueryMethodAutocompleteTests`: `explain` появляется в collection/cursor scope.
- `ShortcutsDialogTests`: `F8` описан.

### 18.4. Web

- `DiagnosticsPageTests`: пустое состояние, выбор подключения, карточки, таблица,
  ошибка одной секции не скрывает остальные.
- `ProfilerPanelTests`: применение, подтверждение, частичный успех, отключение.
- `ExplainDialogTests`: загрузка, дерево, предупреждения, копирование, отмена.
- `QueryAreaTests`: кнопка Explain, `F8`, неподдерживаемый запрос.
- `LatencyHistogramTests`: масштаб баров, пустые данные.
- `SlowQueriesPanelTests`: follow, отмена, поздний ответ.
- `EmbeddedAssetsTests`: `explain` присутствует в собранном `editor.js`.
- `WorkbenchTests`: навигация Diagnostics, баннер активного профайлера.
- `ServiceLoggingTests`: в логах нет текста запроса и `command` из профайлера.

### 18.5. Ручные проверки

- MongoDB 6/7/8, standalone и replica set.
- Пользователь с `read` (explain работает), `clusterMonitor` (serverStatus),
  `dbAdmin` (profiler), без прав (ошибки локализованы).
- `mongos`: профайлер недоступен, explain доступен.
- Atlas или managed-инстанс: команда запрещена — UI не падает.
- TUI и Web одновременно: настройки подхватываются, сохранённая история видна
  обоим процессам, live-метрики помечены процессом.
- Смена уровня профайлера при открытом окне Diagnostics.
- Закрытие Web-вкладки во время explain/чтения `system.profile` не оставляет
  работающих запросов.
- В логах и SQLite нет filter/pipeline/документов после explain и профайлера.

## 19. Критерии готовности

1. В TUI и Web есть раздел Diagnostics с обзором, медленными запросами,
   состоянием БД/сервера и настройкой профайлера.
2. Можно включить `Off`, `Client`, `Server slow`, `Server all`, порог и sample rate.
3. Включение и выключение применяется без перезапуска; фактическое состояние
   читается с сервера.
4. `ServerAll` требует подтверждения, показывает постоянный индикатор и умеет
   автоматически выключаться при выходе.
5. Метрики выполнения собираются для driver и shell, фильтруются по времени,
   БД, коллекции, операции, источнику и длительности.
6. Показываются p50/p95/p99, ошибки, таймауты, медленные, гистограмма и группы.
7. При включённом сохранении история переживает перезапуск и очищается по
   возрасту/количеству.
8. `dbStats`, `collStats`, `$indexStats`, `serverStatus`, `currentOp` и репликация
   отображаются без выхода в `mongosh`.
9. `explain` для find/findOne/aggregate показывает дерево плана, метрики и
   предупреждения; для остального — понятную ошибку.
10. `explain` есть в автодополнении TUI и Web.
11. Текст запросов, фильтры, pipeline и `command` из `system.profile` не попадают
    в логи и SQLite.
12. Все строки локализованы (en/ru), `dotnet build` без warnings, `dotnet test` зелёный.

## 20. План PR и оценка

| PR | Содержание | Оценка |
|---|---|---|
| 1 | Domain-модели, `IQueryMetricsStore`, `IQueryMetricsRepository`, `QueryMetricsRecorder`, `SlowQueryLogger`, `QueryExecutionOptions.Purpose` + unit-тесты | 2–3 дня |
| 2 | `IMongoDiagnosticsGateway`, `DriverMongoGateway`, `MongoProfilerReader`, `MongoExplainMapper`, `CompositeMongoGateway` + интеграционные тесты | 3–4 дня |
| 3 | SQLite-миграция, `SqliteQueryMetricsRepository`, `QueryMetricsFlushService`, `DiagnosticsSettingsService`, DI | 2–3 дня |
| 4 | TUI: `DiagnosticsDialog`, вкладки, `ProfilerDialog`, `ExplainDialog`, меню, `F8`, автодополнение, локализация + TUI-тесты | 3–4 дня |
| 5 | Web: Diagnostics page, панели, Explain, баннер профайлера, CSS, `editor.js`, локализация + Web-тесты | 3–4 дня |
| 6 | Документация, README, CHANGELOG, ручные проверки, полировка | 1–2 дня |
| **Итого** | | **14–20 дней** |

PR 4 и PR 5 при превышении 400 строк дробить на «обзор и профайлер» и
«медленные запросы и explain».

## 21. Риски и открытые вопросы

| Риск | Смягчение |
|---|---|
| `ServerAll` забыли выключить | Постоянный индикатор, подтверждение, auto-disable on exit, явная кнопка Disable |
| Профайлер увеличивает нагрузку | `ServerAll` только вручную; `slowms`/`sampleRate`; предупреждение в UI |
| `system.profile` содержит текст запроса | Читать только whitelisted-поля; тест на отсутствие `command`/`query` |
| Частичный сбой при включении на нескольких БД | Показывать результат по каждой БД; успешные не откатывать |
| `mongos`/Atlas не поддерживают профайлер | Локализованная ошибка, не блокировать остальную диагностику |
| Нет прав на `serverStatus`/`currentOp` | Показывать предупреждение в секции, остальное продолжает работать |
| Клиентские метрики без docsExamined | Явно указывать источник; серверные метрики — только при профайлере/explain |
| Дубли клиентских и серверных наблюдений | Разделять `Source`, не смешивать в одной строке |
| Рост SQLite | Persist выключен по умолчанию; prune 7 дней / 50 000 строк; очередь с потерями |
| Задержка между TUI и Web | Помечать live-метрики процессом; сохранённая история общая, обновление раз в 2 секунды |
| Explain не поддерживает сложный JS | Понятная ошибка и ограниченный список сниппетов |
| Ложные предупреждения explain | Пороговые правила, возможность скрыть конкретное предупреждение в диалоге |
| Расхождение времени клиента и сервера | В `system.profile` использовать серверный `ts`; в UI помечать источник времени |
| `system.profile` пересоздаётся при смене уровня | Предупреждение, что история профайлера недолговечна |
| Приватность raw JSON explain | Не сохранять, не логировать; копирование только по явному действию |

Открытые вопросы для продукта:

1. **Режим по умолчанию.** Оставить `Off` (рекомендуется) или включать `Client`
   с порогом 100 мс при первом запуске?
2. **Сохранение метрик.** Выключено по умолчанию (рекомендуется) или включено
   на 24 часа для «коробочной» статистики?
3. **Срок хранения.** 7 дней / 50 000 строк (рекомендуется) или больше?
4. **ServerAll.** Автоматически выключать через 15 минут (рекомендуется) или
   полагаться только на auto-disable on exit?
5. **Область профайлера.** Только текущая БД (безопаснее) или сразу все
   несистемные БД (удобнее)?
6. **Explain write-операций.** Добавлять ли `update`/`delete` explain во второй
   итерации?
7. **Хранение текста запроса.** Оставляем полный запрет (рекомендуется) или
   добавляем opt-in «хранить нормализованный fingerprint» без значений?
8. **Отдельный пункт навигации.** Diagnostics как отдельная страница (рекомендуется)
   или вкладка внутри Settings?
9. **Порог «медленного».** Единый глобальный 100 мс или отдельный для каждой
   коллекции/БД?
10. **Карточки сервера.** Показывать ли `opLatencies` и `wiredTiger` подробно
    в первой версии или ограничиться базовыми счётчиками?

## 22. Ссылки на текущий код

- Замер и логирование запросов: `src/MongoTui.Application/Services/QueryService.cs`.
- Модель результата: `src/MongoTui.Domain/Queries/QueryResult.cs`,
  `QueryExecutionOptions.cs`.
- Driver gateway: `src/MongoTui.Infrastructure/Mongo/Driver/DriverMongoGateway.cs`
  (`FindAsync`, `AggregateAsync`, `GetDatabaseStatsAsync`).
- Маршрутизация каналов: `src/MongoTui.Infrastructure/Mongo/Cli/CompositeMongoGateway.cs`.
- `system.profile`/shell: `src/MongoTui.Infrastructure/Mongo/Cli/MongoShellSession.cs`.
- Статистика БД/коллекции: `src/MongoTui.Application/Services/DatabaseService.cs`,
  `IndexService.cs`, `src/MongoTui.Domain/Databases/DatabaseStats.cs`,
  `src/MongoTui.Domain/Collections/CollectionStats.cs`.
- TUI-меню и статистика: `src/MongoTui.TUI/Views/MainWindow.cs:359`,
  `:625`, `:685`, `:707`.
- TUI-настройки логов: `src/MongoTui.TUI/Views/LoggingSettingsDialog.cs`,
  `MainWindow.LoggingSettings.cs`.
- Web-навигация: `src/MongoTui.Web/Components/Workbench.razor:12`, `:42`.
- Web-настройки: `src/MongoTui.Web/Components/Panels/SettingsPanel.razor`,
  `LoggingSettingsPanel.razor`, `LogsPanel.razor`.
- Web-запросы: `src/MongoTui.Web/Services/QueryWorkspace.cs`,
  `src/MongoTui.Web/Components/Panels/QueryArea.razor`.
- Автодополнение TUI: `src/MongoTui.TUI/Editor/QueryMethodCatalog.cs`,
  `Snippets.cs`, `QueryCompletionContext.cs`.
- Автодополнение Web: `src/MongoTui.Web/ClientAssets/editor.js`,
  `src/MongoTui.Web/wwwroot/editor.js`.
- SQLite: `src/MongoTui.Infrastructure/Persistence/Sqlite/SqliteDatabase.cs`,
  `SqliteQueryHistoryRepository.cs`, `MongoTuiDataConnection.cs`.
- Настройки: `src/MongoTui.Application/Services/SettingsService.cs`,
  `src/MongoTui.Infrastructure/Persistence/Sqlite/SqliteSettingsStore.cs`.
- Логи: `docs/logging.md`,
  `src/MongoTui.Infrastructure/Diagnostics/SerilogLoggingSettings.cs`,
  `FileApplicationLogReader.cs`.
- Web-хост и DI: `src/MongoTui.Web/Bootstrap/WebHost.cs`,
  `src/MongoTui.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`,
  `src/MongoTui.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`.
- Существующий дизайн-документ как образец структуры:
  `docs/user-management-design.md`.
