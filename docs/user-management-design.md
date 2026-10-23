# Управление пользователями MongoDB — дизайн

**Статус:** черновик  
**Дата:** 2026-10-23  
**Область:** Domain, Application, Infrastructure, TUI

## 1. Цель

Добавить в MongoTui управление пользователями MongoDB без выхода в `mongosh`:

- контекстное меню **базы данных** — управление пользователями, созданными в этой базе;
- контекстное меню **подключения** — управление пользователями базы `admin`;
- модальный список пользователей с действиями **Create**, **Edit**, **Delete**, **Refresh**;
- при создании — ввод или генерация пароля, настройка доступа к базам (`r` / `rw` / admin),
  показ готового connection string;
- при изменении — только смена пароля и доступов к базам;
- удаление — с обязательным подтверждением.

Фича рассчитана на администратора, который уже подключён к MongoDB с правами на управление
пользователями. Пароли и connection string не сохраняются в SQLite и не пишутся в логи.

### Принятые решения (предлагаемые)

1. Контекст базы данных = **база аутентификации** (`authSource`) пользователя, а не множество баз,
   на которые у пользователя есть роли. Список строится через `usersInfo` в этой базе.
2. Контекст подключения всегда открывает пользователей базы `admin`.
3. Уровни доступа в UI:
   - `r` → роль `read` в выбранной базе;
   - `rw` → роль `readWrite` в выбранной базе;
   - **Administrator** → роль `root` в базе `admin` (доступ ко всему кластеру).
4. `Administrator` — отдельный режим формы. При его включении список баз отключается и очищается.
5. Неизвестные роли (пользовательские, `clusterAdmin`, `readAnyDatabase` и т.п.) не теряются:
   они показываются как дополнительные и сохраняются при обновлении.
6. Пароль генерируется криптографически стойким генератором, показывается один раз в результате
   создания вместе с connection string.
7. В режиме редактирования пустое поле пароля означает «оставить текущий пароль».
8. Удаление пользователя, под которым выполнено текущее подключение, запрещено.

## 2. Вне области

- Управление пользовательскими ролями (`createRole`, `updateRole`, `dropRole`).
- Роли сложнее `read`, `readWrite`, `root`; они только сохраняются как дополнительные.
- `customData`, `authenticationRestrictions`, `mechanisms`, X.509 и LDAP.
- Политики сложности паролей и срок действия паролей.
- История изменений пользователей и аудит.
- Массовые операции и импорт/экспорт пользователей.
- Автоматическое создание или обновление сохранённого подключения под новым пользователем.
- Просмотр всех пользователей кластера в одном списке (`usersInfo: { forAllDBs: true }`).
- Управление пользователями через `mongosh`/CLI-канал: используется только драйвер.

## 3. Пользовательские сценарии

### 3.1. Управление пользователями базы

1. Пользователь раскрывает подключение и выбирает базу в дереве.
2. В контекстном меню выбирает `Manage users...`.
3. Открывается модальное окно со списком пользователей этой базы.
4. Администратор может создать, изменить или удалить пользователя.
5. После успешной операции список перезагружается.

### 3.2. Управление пользователями подключения

1. Пользователь выбирает корневой узел подключения.
2. В контекстном меню выбирает `Manage users (admin)...`.
3. Открывается то же окно, но с базой `admin`.

### 3.3. Создание пользователя

1. Нажать `Create`.
2. Заполнить имя пользователя.
3. Ввести пароль вручную или нажать `Generate`.
4. Настроить доступ:
   - включить `Administrator`; или
   - добавить одну или несколько баз и выбрать для каждой `r`/`rw`.
5. Нажать `Create`.
6. После успешного выполнения команды показывается окно результата:
   - имя пользователя;
   - сгенерированный/введённый пароль и готовый connection string;
   - кнопка `Copy connection string`;
   - предупреждение, что после закрытия окна пароль нельзя получить повторно.
7. Закрыть окно и вернуться к списку.

### 3.4. Изменение пользователя

1. Выбрать пользователя и нажать `Edit`.
2. Имя пользователя недоступно для изменения.
3. Поле пароля пустое; заполнение меняет пароль, пустое значение оставляет прежний.
4. Список баз и уровней доступа заполнен текущими управляемыми ролями.
5. Дополнительные (неуправляемые) роли показаны отдельным read-only блоком.
6. Сохранить изменения.
7. Если изменён пароль текущего пользователя подключения — показать предупреждение, что
   сохранённое подключение может перестать работать после переподключения.

### 3.5. Удаление пользователя

1. Выбрать пользователя и нажать `Delete`.
2. Показать подтверждение с именем и базой пользователя.
3. При подтверждении выполнить `dropUser`.
4. Перезагрузить список.

## 4. UI

### 4.1. Контекстные меню дерева

`MainWindow.BuildContextActions` (`src/MongoTui.TUI/Views/MainWindow.cs:600`) получает новые пункты:

| Узел | Пункт | База для окна |
|---|---|---|
| `TreeRowKind.Connection` | `Tree.ManageUsersAdmin` — «Manage users (admin)...» | `admin` |
| `TreeRowKind.Database` | `Tree.ManageUsers` — «Manage users...» | выбранная база |

Порядок: пункт управления пользователями размещается после статистики/refresh и до destructive
операций (`Drop database`, `Drop collection`).

### 4.2. `UserManagementDialog`

Новый файл `src/MongoTui.TUI/Views/UserManagementDialog.cs`.

- Заголовок: `Users.Title` с именем базы: `Users — admin`.
- `ListView` со строками вида:
  `app-reader  [orders: r, billing: rw]`;
  `cluster-admin  [Administrator]`;
  `service  [orders: rw] +2 roles` для неуправляемых ролей.
- Кнопки: `Create`, `Edit`, `Delete`, `Refresh`, `Close`.
- `Delete` и `Edit` неактивны без выбранного пользователя.
- Ошибка загрузки/команды показывается в строке статуса диалога, диалог не закрывается.
- Двойной клик по пользователю открывает редактирование.
- После `Create`/`Edit`/`Delete` выполняется `LoadAsync`.

Конструктор получает `UserManagementService`, `ILocalizer`, `IApplication`,
`ActiveConnection`, `DatabaseName scope` и имя текущего пользователя подключения.

### 4.3. `UserFormDialog`

Новый файл `src/MongoTui.TUI/Views/UserFormDialog.cs`.

Режимы: `Create` и `Edit`.

Поля:

| Поле | Create | Edit |
|---|---|---|
| Username | TextField | read-only |
| Password | secret TextField + `Generate` | secret TextField + `Generate`, пусто = не менять |
| Administrator | CheckBox | CheckBox |
| Access list | ListView `db: r/rw` + `Add access...` / `Remove` | то же |
| Additional roles | — | read-only список |
| Create/Save | активно при валидной форме | активно при валидной форме |

`Add access...` открывает `DatabaseAccessDialog`:

- база: `ComboBox` с уже известными базами подключения, допускает ввод произвольного имени;
- доступ: `r` или `rw`;
- `OK` / `Cancel`.

В create-режиме требуется хотя бы одна база или `Administrator`. В edit-режиме можно удалить все
управляемые роли, но неуправляемые роли сохраняются.

### 4.4. `UserCreatedDialog`

Новый файл `src/MongoTui.TUI/Views/UserCreatedDialog.cs`.

- Показывает имя, пароль и connection string в read-only `TextView`.
- Кнопка `Copy connection string` использует `TerminalClipboard`.
- Показывает предупреждение `Users.PasswordShownOnce`.
- Единственная кнопка закрытия — `Close`.
- Пароль не сохраняется в диалоге после закрытия и не попадает в лог.

### 4.5. Keyboard и доступность

- `Enter` в списке пользователей открывает редактирование.
- `Delete` в списке пользователей инициирует удаление с подтверждением.
- `Esc` закрывает текущий диалог без изменений.
- Горячие клавиши не конфликтуют с существующими (`Ctrl+D` занят удалением документа).
- Все строки — через `Strings.resx` / `Strings.ru.resx`.

## 5. Модель Domain

Новая папка `src/MongoTui.Domain/Users/`.

```csharp
public sealed record MongoRole(string Name, string Database);

public sealed record MongoUser(
    string Username,
    string Database,
    IReadOnlyList<MongoRole> Roles);

public enum DatabaseAccessLevel
{
    Read,
    ReadWrite,
}

public sealed record DatabaseAccess(DatabaseName Database, DatabaseAccessLevel Level);

public sealed record UserAccessSelection(
    bool IsAdministrator,
    IReadOnlyList<DatabaseAccess> Databases);
```

`MongoUser.Database` — база, в которой создан пользователь (`authSource`), а не база роли.

`UserAccessSelection` — то, что редактирует пользователь в UI. Преобразование в роли Mongo и
сохранение неизвестных ролей выполняет Application-сервис.

## 6. Application

### 6.1. `IMongoUserGateway`

Новый интерфейс `src/MongoTui.Application/Abstractions/Mongo/IMongoUserGateway.cs`:

```csharp
public interface IMongoUserGateway
{
    Task<IReadOnlyList<MongoUser>> ListUsersAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken);

    Task CreateUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string password,
        IReadOnlyList<MongoRole> roles,
        CancellationToken cancellationToken);

    Task UpdateUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string? password,
        IReadOnlyList<MongoRole> roles,
        CancellationToken cancellationToken);

    Task DeleteUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        CancellationToken cancellationToken);
}
```

Отдельный интерфейс выбран вместо расширения `IMongoGateway`: управление пользователями — не
операция над коллекцией/документом, и она не нужна CLI-каналу. `CompositeMongoGateway` реализует
`IMongoUserGateway` делегированием в `DriverMongoGateway`.

### 6.2. `UserManagementService`

Новый сервис `src/MongoTui.Application/Services/UserManagementService.cs`.

Методы:

- `ListAsync(target, database, ct)`;
- `CreateAsync(target, database, username, password, access, ct)`;
- `UpdateAsync(target, database, user, password?, access, ct)`;
- `DeleteAsync(target, database, username, ct)`;
- `BuildConnectionString(profile, username, password, authDatabase)` — делегирует в
  `UserConnectionStringBuilder`;
- `GeneratePassword()` — делегирует в `PasswordGenerator`.

Сервис отвечает за:

1. Валидацию имени пользователя и пароля.
2. Преобразование `UserAccessSelection` в роли Mongo.
3. Объединение управляемых ролей с неуправляемыми ролями при `UpdateAsync`.
4. Запрет удаления текущего пользователя подключения.
5. Проверку, что при `IsAdministrator = false` есть хотя бы одна база.

Валидация не бросает исключения драйвера и не знает про BSON/локализацию.

### 6.3. Преобразование доступов в роли

| Выбор в UI | Роль Mongo |
|---|---|
| `r` для базы `orders` | `{ role: "read", db: "orders" }` |
| `rw` для базы `orders` | `{ role: "readWrite", db: "orders" }` |
| `Administrator` | `{ role: "root", db: "admin" }` |

Правила:

- `Administrator` и список баз взаимоисключающие.
- При `Administrator` все управляемые роли `read`/`readWrite` удаляются, `root` добавляется.
- Неуправляемые роли не входят в пересечение и переносятся без изменений.
- Управляемая роль — это `read`/`readWrite` в любой базе или `root` в `admin`.
- Одинаковые роли не дублируются.
- При удалении базы из списка её `read`/`readWrite` больше не передаётся в `updateUser`.

### 6.4. `PasswordGenerator`

Новый класс `src/MongoTui.Application/Services/PasswordGenerator.cs`.

- Использует `System.Security.Cryptography.RandomNumberGenerator`.
- Длина по умолчанию — 24 символа.
- Набор без визуально неоднозначных символов (`O/0`, `I/l/1`) и без пробелов.
- Минимум по одному символу из букв верхнего/нижнего регистра, цифр и спецсимволов.
- Не является статическим синглтоном с состоянием; чистая утилита или инжектируемый сервис.
- Тесты проверяют длину, алфавит и наличие всех классов символов, но не конкретное значение.

### 6.5. `UserConnectionStringBuilder`

Новый класс `src/MongoTui.Application/Services/UserConnectionStringBuilder.cs`.

Строит строку из **сохранённого URI профиля**, а не из активного target:

- активный target может содержать `proxyHost`/`proxyPort` локального SSH-туннеля;
- сохранённый URI содержит исходные хосты, TLS и прочие параметры.

Алгоритм:

1. Распарсить `profile.Uri` через `ConnectionUriParser` и убрать существующие credentials.
2. Удалить из query `authSource`, `authMechanism`, `proxyHost`, `proxyPort`,
   `proxyUsername`, `proxyPassword`.
3. Добавить `authSource=<база пользователя>`.
4. Подставить `Uri.EscapeDataString(username)` и `Uri.EscapeDataString(password)` в userinfo.
5. Сохранить остальные параметры (`tls`, `replicaSet`, `retryWrites`, `readPreference` и т.п.).
6. Не включать пароль текущего администратора.

Пример результата для базы `admin`:

```text
mongodb://app-reader:P%40ssw0rd%21@mongo.example.com:27017/?authSource=admin&tls=true
```

Если у подключения включён SSH-туннель, connection string не содержит SSH-настройки. В окне
результата показывается предупреждение: строка рассчитана на прямой доступ к MongoDB; для работы
через SSH нужно настроить туннель отдельно.

## 7. Infrastructure

### 7.1. `DriverMongoGateway`

Файл `src/MongoTui.Infrastructure/Mongo/Driver/DriverMongoGateway.cs` реализует
`IMongoUserGateway`.

Все команды выполняются в базе `database`:

| Операция | Команда |
|---|---|
| Список | `{ usersInfo: 1 }` |
| Создание | `{ createUser: username, pwd: password, roles: [...] }` |
| Изменение | `{ updateUser: username, pwd?: password, roles: [...] }` |
| Удаление | `{ dropUser: username }` |

Через `IMongoDatabase.RunCommandAsync<BsonDocument>`.

Маппинг результата `usersInfo`:

- `user` → `MongoUser.Username`;
- `db` → `MongoUser.Database`;
- `roles[].role` / `roles[].db` → `MongoRole`.

Пароль не возвращается сервером и может использоваться только в окне результата создания.

### 7.2. Ошибки

`MongoCommandException` маппится в `DomainException` с безопасным текстом (без пароля и полной
командной строки):

| Код/случай | Сообщение |
|---|---|
| `13` Unauthorized | Недостаточно прав для управления пользователями |
| пользователь уже существует | Пользователь уже существует |
| пользователь не найден | Пользователь не найден |
| неверная роль | Не удалось назначить роль |
| остальное | `MongoDB command failed` |

TUI локализует доменные ошибки через существующий `DomainErrorLocalization`/`GetErrorMessage`.

### 7.3. `CompositeMongoGateway` и DI

- `CompositeMongoGateway` реализует `IMongoUserGateway` и делегирует в `IDriverMongoGateway`.
- `DriverMongoGateway` реализует `IMongoUserGateway` напрямую.
- В `InfrastructureServiceCollectionExtensions` регистрируется:
  `IMongoUserGateway` → `CompositeMongoGateway`.
- В `ApplicationServiceCollectionExtensions` регистрируется `UserManagementService`.

## 8. Команды MongoDB: детали

### 8.1. Создание

```javascript
db.runCommand({
  createUser: "app-reader",
  pwd: "generated-password",
  roles: [
    { role: "readWrite", db: "orders" },
    { role: "read", db: "analytics" },
  ],
});
```

Пользователь создаётся в базе, из контекста которой открыт диалог (`authSource`).
Роль `root` в `admin` разрешена только пользователям с соответствующими правами.

### 8.2. Изменение

```javascript
db.runCommand({
  updateUser: "app-reader",
  pwd: "new-password",
  roles: [{ role: "readWrite", db: "orders" }],
});
```

`roles` передаётся полным набором: управляемые роли из формы плюс сохранённые неуправляемые.
Если пароль не менялся, `pwd` не передаётся.

### 8.3. Удаление

```javascript
db.runCommand({ dropUser: "app-reader" });
```

Перед выполнением — `ConfirmDialog`. Удаление текущего пользователя подключения блокируется.

## 9. Безопасность и приватность

- Пароли не сохраняются в SQLite, настройках и истории.
- Пароль не логируется; ошибки не содержат `pwd`.
- Connection string и пароль показываются только в `UserCreatedDialog`.
- После закрытия окна восстановить пароль нельзя; это явно написано в UI.
- Копирование в clipboard — по явному действию пользователя.
- В лог пишутся только факты: тип операции, имя базы, результат; без имени пользователя?
  Имя пользователя допустимо, пароль — нет.
- `usersInfo` не возвращает хэши паролей; приложение их не запрашивает.
- Для создания/изменения пользователя нужны права `userAdmin`/`userAdminAnyDatabase` и право
  выдавать запрошенные роли. Ошибка прав показывается без падения TUI.
- При SSH-подключении сгенерированный URI не содержит параметров туннеля и может быть непригоден
  без отдельного туннеля; показывается предупреждение.

## 10. Локализация

Добавить в `Strings.resx` и `Strings.ru.resx`:

```text
Tree.ManageUsers
Tree.ManageUsersAdmin

Users.Title
Users.Empty
Users.Refresh
Users.Create
Users.Edit
Users.Delete
Users.DeleteConfirmTitle
Users.DeleteConfirmMessage
Users.DeleteCurrentForbidden
Users.LoadFailed
Users.SaveFailed

Users.Form.CreateTitle
Users.Form.EditTitle
Users.Field.Username
Users.Field.Password
Users.Field.Generate
Users.Field.Administrator
Users.Field.Access
Users.Field.AdditionalRoles
Users.Access.Add
Users.Access.Remove
Users.Access.Database
Users.Access.Read
Users.Access.ReadWrite
Users.Access.Required
Users.Access.DatabaseInvalid

Users.Created.Title
Users.Created.PasswordShownOnce
Users.Created.CopyConnectionString
Users.Created.Copied
Users.Created.SshWarning

Users.Error.Unauthorized
Users.Error.AlreadyExists
Users.Error.NotFound
Users.Error.RoleGrantFailed
Users.Error.CommandFailed
```

## 11. Изменения по файлам

### Domain

| Файл | Изменение |
|---|---|
| `Users/MongoRole.cs` | новый record |
| `Users/MongoUser.cs` | новый record |
| `Users/DatabaseAccessLevel.cs` | новый enum |
| `Users/DatabaseAccess.cs` | новый record |
| `Users/UserAccessSelection.cs` | новый record |

### Application

| Файл | Изменение |
|---|---|
| `Abstractions/Mongo/IMongoUserGateway.cs` | новый интерфейс |
| `Services/UserManagementService.cs` | новый сервис |
| `Services/PasswordGenerator.cs` | новый генератор |
| `Services/UserConnectionStringBuilder.cs` | новый построитель URI |
| `DependencyInjection/ApplicationServiceCollectionExtensions.cs` | регистрация `UserManagementService` |

### Infrastructure

| Файл | Изменение |
|---|---|
| `Mongo/Driver/DriverMongoGateway.cs` | реализация `IMongoUserGateway` |
| `Mongo/Cli/CompositeMongoGateway.cs` | делегирование `IMongoUserGateway` |
| `DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | регистрация `IMongoUserGateway` |

### TUI

| Файл | Изменение |
|---|---|
| `Views/MainWindow.cs` | новые пункты контекстного меню, открытие диалога |
| `Views/UserManagementDialog.cs` | новый |
| `Views/UserFormDialog.cs` | новый |
| `Views/DatabaseAccessDialog.cs` | новый |
| `Views/UserCreatedDialog.cs` | новый |
| `Resources/Strings.resx` | новые строки |
| `Resources/Strings.ru.resx` | новые строки |

### Корневые файлы

- `CHANGELOG.md` — запись после реализации.
- `README.md`, `README_RU.md` — раздел о возможностях управления пользователями.
- `docs/user-management-design.md` — этот документ.

## 12. Тестирование

### Unit

- `UserManagementServiceTests`:
  - преобразование `r`/`rw`/`Administrator` в роли;
  - взаимоисключение admin и списка баз;
  - сохранение неизвестных ролей при обновлении;
  - пустой пароль в edit не меняет пароль;
  - запрет удаления текущего пользователя;
  - ошибка при пустом списке доступов без admin.
- `PasswordGeneratorTests`: длина, алфавит, все классы символов.
- `UserConnectionStringBuilderTests`:
  - `mongodb://` и `mongodb+srv://`;
  - percent-encoding логина и пароля;
  - замена `authSource`;
  - удаление старых credentials и `authMechanism`;
  - сохранение `tls`, `replicaSet` и других параметров;
  - URI без credentials.
- `Domain`-тесты моделей, если добавляется валидация.

### Integration

В `MongoTui.IntegrationTests` на существующем контейнере:

- создать пользователя с `read` и проверить подключение к разрешённой базе;
- создать пользователя с `readWrite` и проверить запись;
- проверить запрет записи для `read`;
- создать пользователя с `root` и проверить доступ к `admin`;
- обновить пароль и проверить вход со старым/новым паролем;
- обновить роли и проверить права;
- удалить пользователя и проверить, что вход невозможен;
- проверить ошибку при недостаточных правах;
- проверить, что сгенерированный connection string действительно подключается.

### TUI

- `UserManagementDialogTests`: загрузка списка, пустой список, ошибка загрузки,
  активность кнопок, подтверждение удаления.
- `UserFormDialogTests`: валидация имени/пароля/доступов, генерация пароля,
  переключение `Administrator`, редактирование без смены пароля.
- `UserCreatedDialogTests`: отображение и копирование connection string.
- `MainWindow` context-menu tests: пункты доступны для connection/database,
  передаётся правильная база (`admin` для корня).

## 13. Критерии готовности

1. В контекстном меню подключения есть управление пользователями `admin`.
2. В контекстном меню каждой базы есть управление пользователями этой базы.
3. Список пользователей загружается и обновляется без перезапуска приложения.
4. Можно создать пользователя с паролем вручную или сгенерированным.
5. Можно назначить `r`/`rw` на произвольный список баз.
6. Можно выдать admin-доступ (`root`) без выбора отдельных баз.
7. После создания показывается рабочий connection string с паролем.
8. При редактировании меняются только пароль и доступы; имя не редактируется.
9. Неуправляемые роли не теряются при редактировании.
10. Удаление требует подтверждения; текущего пользователя удалить нельзя.
11. Пароли и connection string не попадают в логи и SQLite.
12. Все новые строки локализованы (en/ru).
13. `dotnet build` без warnings, `dotnet test` зелёный.

## 14. План PR и оценка

| PR | Содержание | Оценка |
|---|---|---|
| 1 | Domain-модели, `IMongoUserGateway`, `UserManagementService`, `PasswordGenerator`, `UserConnectionStringBuilder` + unit-тесты | 2–3 дня |
| 2 | Реализация в `DriverMongoGateway`/`CompositeMongoGateway`, маппинг ошибок, интеграционные тесты | 2–3 дня |
| 3 | `UserManagementDialog`, `UserFormDialog`, `DatabaseAccessDialog`, `UserCreatedDialog` | 3–4 дня |
| 4 | Контекстные меню, локализация, TUI-тесты, README/CHANGELOG | 1–2 дня |
| **Итого** | | **8–12 дней** |

PR 3 при превышении 400 строк дробить на «список + удаление» и «форма + результат создания».

## 15. Риски и открытые вопросы

| Риск | Смягчение |
|---|---|
| Недостаточные права у текущего подключения | Показать локализованную ошибку; не считать это багом TUI |
| Случайная потеря неуправляемых ролей | Сохранять и показывать их; тест на merge |
| Пользователь удаляет сам себя | Запретить удаление текущего пользователя |
| Пароль уходит в clipboard/историю | Явное действие, предупреждение, не логировать |
| SSH-подключение | Строить URI из профиля, а не из target; предупреждать, что туннель не входит в строку |
| `root` слишком широк | Явная пометка «Administrator» и предупреждение в UI |
| Пользовательские роли на нескольких базах | Показывать как дополнительные и сохранять |
| `mongodb+srv` и параметры URI | Тесты на сохранение query-параметров |
| Sharded cluster / Atlas | Команды стандартные; проверить в интеграционном тесте при возможности |

Открытые вопросы для продукта:

1. **Admin-роль:** использовать `root` или набор `userAdminAnyDatabase` +
   `dbAdminAnyDatabase` + `readWriteAnyDatabase`? Рекомендация — `root` как явный «полный админ».
2. **Контекст базы:** показывать только пользователей, созданных в этой базе, или всех, у кого
   есть роли на эту базу? Рекомендация — только `authSource` этой базы; второй вариант — отдельная
   будущая функция.
3. **Удаление себя:** блокировать полностью или разрешать после двойного подтверждения?
   Рекомендация — блокировать.
4. **Произвольные базы:** разрешать вводить имя базы, которой ещё нет, или выбирать только из
   существующих? Рекомендация — разрешать ввод, как это делает MongoDB.
5. **Пароль при edit:** пустое поле = не менять (предложено) или обязательно задавать новый?
6. **Длина/алфавит генерируемого пароля:** 24 символа с спецсимволами? Нужна ли настройка?
7. **Connection string для SSH:** предупреждения достаточно или нужно генерировать строку с
   параметрами SOCKS-прокси?
8. **Показывать ли пользователей с не-SCRAM механизмами** (X.509/LDAP) в общем списке?
   Рекомендация — показывать, но не предлагать смену пароля.

## 16. Ссылки на текущий код

- Контекстное меню дерева: `src/MongoTui.TUI/Views/MainWindow.cs:575`,
  `BuildContextActions` — `:600`.
- Открытие модальных окон: `MainWindow.OpenIndexesAsync`, `OpenExport`, `OpenImport`.
- Дерево и активное подключение: `src/MongoTui.TUI/ViewModels/TreeViewModel.cs`,
  `src/MongoTui.TUI/ViewModels/ActiveConnection.cs`.
- Драйверный gateway: `src/MongoTui.Infrastructure/Mongo/Driver/DriverMongoGateway.cs`.
- Маршрутизация gateway: `src/MongoTui.Infrastructure/Mongo/Cli/CompositeMongoGateway.cs`.
- Разбор URI: `src/MongoTui.Application/Services/ConnectionUriParser.cs`,
  `src/MongoTui.Application/Models/ParsedConnectionUri.cs`.
- Построение connection string с credentials:
  `src/MongoTui.Application/Services/ConnectionService.cs:120`.
- Модальный список с кнопками: `src/MongoTui.TUI/Views/ConnectionManagerDialog.cs`,
  `src/MongoTui.TUI/Views/IndexManagerDialog.cs`.
- Подтверждение: `src/MongoTui.TUI/Views/ConfirmDialog.cs`.
- Локализация: `src/MongoTui.TUI/Resources/Strings.resx`,
  `src/MongoTui.TUI/Resources/Strings.ru.resx`.
- DI: `src/MongoTui.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`,
  `src/MongoTui.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`.
