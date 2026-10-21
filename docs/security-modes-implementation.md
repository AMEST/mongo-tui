# Режимы защиты секретов — план реализации

**Статус:** готов к реализации  
**Дата:** 2026-10-21  
**Область:** Application, Infrastructure, TUI

## 1. Цель

Сделать master password необязательным, добавив три явных режима защиты секретов:

| Режим | KEK | Пароль при старте | Платформы |
|---|---|---|---|
| `Password` | Argon2id от master password + salt | Да | Windows, macOS, Linux |
| `Keyring` | 32 случайных байта в OS keyring | Нет | Windows, macOS |
| `None` | 32 случайных байта в файле `~/.mongotui/kek.key` с правами `0600` | Нет | Windows, macOS, Linux |

Режим по умолчанию и рекомендуемый — `Password`.

### Принятые решения

1. Библиотека keyring: **Latchkey 0.1.0** (MIT, `net8.0`/`net10.0`, чистый managed P/Invoke).
2. Latchkey скрывается за существующим `IKeyring`; при необходимости его можно заменить
   собственной реализацией без изменения Application.
3. На Linux keyring-режим недоступен. Доступны `Password` и `None`.
4. В режиме `None` KEK не хранится в SQLite вместе с шифротекстом. Он лежит в отдельном файле
   `~/.mongotui/kek.key` с правами `0600` на Unix.
5. Пустой пароль и `Argon2id("")` не используются.
6. При потере ключевого материала (keyring-запись, файл KEK, salt/check) приложение **не создаёт
   новый KEK молча**, если уже есть сохранённые секреты. Показывается ошибка с вариантами
   восстановления или destructive reset.
7. Пункт «Change master password» и `Ctrl+P` доступны только в режиме `Password`.
8. Переключение режимов перешифровывает пароли подключений и AI API key в одной транзакции.

## 2. Вне области

- Реализация Linux keyring через Secret Service/libsecret.
- Автоматическая миграция между машинами и перенос keyring-ключей.
- Требования к сложности master password (остаётся как сейчас).
- Deferred unlock: приложение по-прежнему разблокирует секреты при старте, но в `Keyring`/`None`
  делает это автоматически.
- Шифрование всей SQLite-базы.
- Смена KEK в пределах одного режима (`Keyring` → `Keyring`, `None` → `None`) как отдельная
  функция. Переключение режима и смена пароля покрываются; ротация ключа внутри режима — нет.

## 3. Пользовательские сценарии

### Первый запуск

1. Пользователь запускает MongoTui с пустой базой.
2. Показывается диалог выбора режима.
3. На Windows/macOS доступны все три варианта; на Linux — `Password` и `None`.
4. После выбора:
   - `Password` — задаётся пароль (с подтверждением);
   - `Keyring` — создаётся запись в keyring;
   - `None` — создаётся файл KEK с `0600` и показывается предупреждение.
5. Режим сохраняется. Приложение продолжает обычный старт.

### Обычный запуск

| Режим | Поведение |
|---|---|
| `Password` | Показывается диалог ввода пароля, как сейчас |
| `Keyring` | KEK читается из keyring без ввода пароля |
| `None` | KEK читается из файла без ввода пароля |

### Переключение режима

1. Пользователь открывает `Settings → Security...`.
2. Видит текущий режим и доступные варианты.
3. Выбирает новый режим.
4. Для `Password` задаёт новый пароль.
5. При переходе из `Password` в более слабый режим показывается подтверждение.
6. Все секреты перешифровываются, режим сохраняется.
7. Меню и состояние приложения обновляются.

## 4. Архитектура

### 4.1. Слои

- **Application** — модель режимов, `SecurityModeService`, интерфейсы `IKekSource`/`IKekFileStore`,
  `KekRotation`.
- **Infrastructure** — реализации `IKekSource`:
  - `PasswordKekSource` (Argon2id);
  - `KeyringKekSource` (Latchkey через `IKeyring`);
  - `FileKekSource` (файл `0600`);
  - `FileKekFileStore` (файловые операции).
- **TUI** — диалоги выбора/переключения режима, изменения меню, локализация.
- **Domain** не меняется.

### 4.2. Модель

`src/MongoTui.Application/Models/SecurityMode.cs`:

```csharp
namespace MongoTui.Application.Models;

public enum SecurityMode
{
    Password,
    Keyring,
    None,
}
```

Состояние обнаружения режима:

```csharp
public enum SecurityState
{
    FirstRun,
    Ready,
    Invalid,
}

public enum SecurityProblem
{
    KeyringUnavailable,
    KeyringKeyMissing,
    KekFileMissing,
    PasswordMaterialMissing,
    UnknownKeyMaterial,
}
```

`Problem` — код, а не текст: TUI сам подбирает строку из `.resx`. Это сохраняет правило
«Application не знает о локализации».

### 4.3. Источник KEK

`src/MongoTui.Application/Abstractions/Security/IKekSource.cs`:

```csharp
public interface IKekSource
{
    SecurityMode Mode { get; }

    bool IsAvailable { get; }

    /// <summary>
    /// Returns an existing KEK for modes that can unlock without user input.
    /// Returns null when the key material is missing; must never create a new key here.
    /// </summary>
    Task<byte[]?> TryGetKekAsync(CancellationToken cancellationToken);

    /// <summary>Unlocks a password-protected KEK.</summary>
    Task<byte[]> UnlockAsync(string password, CancellationToken cancellationToken);

    /// <summary>
    /// Creates new key material and returns the new KEK. For Password, creates a new salt
    /// and check value; for Keyring, overwrites the keyring entry; for None, overwrites the file.
    /// </summary>
    Task<byte[]> CreateAsync(string? password, CancellationToken cancellationToken);

    /// <summary>Deletes the current key material after a successful mode switch.</summary>
    Task DeleteAsync(CancellationToken cancellationToken);
}
```

Семантика по режимам:

| Метод | `PasswordKekSource` | `KeyringKekSource` | `FileKekSource` |
|---|---|---|---|
| `TryGetKekAsync` | `null` (нужен пароль) | запись или `null` | файл или `null` |
| `UnlockAsync` | проверяет пароль | `NotSupportedException` | `NotSupportedException` |
| `CreateAsync` | новый salt/check + Argon2id | новый random KEK + запись | новый random KEK + файл |
| `DeleteAsync` | удаляет salt/check | удаляет запись | удаляет файл |
| `IsAvailable` | `true` | `IKeyring.IsAvailable` | `true` |

Текущие `IKeyProvider`, `FallbackKeyProvider`, `KeyringKeyProvider`, `MasterPasswordKeyProvider`
заменяются этой моделью. `IKeyring` остаётся.

### 4.4. Хранилище файла KEK

`src/MongoTui.Application/Abstractions/Security/IKekFileStore.cs`:

```csharp
public interface IKekFileStore
{
    string Path { get; }
    Task<byte[]?> TryReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(byte[] kek, CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
}
```

Требования к `FileKekFileStore`:

- путь: `~/.mongotui/kek.key` (добавить `AppPaths.KekFile`);
- ровно 32 байта;
- атомарная запись: temp-файл в том же каталоге + `File.Move(..., overwrite: true)`;
- на Unix — `File.SetUnixFileMode(path, UserRead | UserWrite)` (`0600`);
- на Windows — стандартные ACL профиля пользователя; при необходимости ужесточить позже;
- при чтении файла неверной длины — `DomainException` с английским сообщением, UI показывает
  локализованную ошибку;
- каталог `~/.mongotui` создаётся при необходимости.

### 4.5. `SecurityModeService`

`src/MongoTui.Application/Services/SecurityModeService.cs`:

```csharp
public sealed class SecurityModeService
{
    public SecurityMode? CurrentMode { get; }
    public bool IsUnlocked => _secrets.IsUnlocked;

    public IReadOnlyList<SecurityMode> AvailableModes { get; }

    public Task<SecurityModeDetection> DetectAsync(CancellationToken cancellationToken);
    public Task InitializeAsync(SecurityMode mode, string? password, CancellationToken cancellationToken);
    public Task<AutoUnlockResult> TryAutoUnlockAsync(CancellationToken cancellationToken);
    public Task UnlockWithPasswordAsync(string password, CancellationToken cancellationToken);
    public Task SwitchModeAsync(SecurityMode target, string? newPassword, CancellationToken cancellationToken);
    public Task ChangePasswordAsync(string newPassword, CancellationToken cancellationToken);
}
```

Результаты:

```csharp
public enum AutoUnlockResult
{
    Unlocked,
    PasswordRequired,
    KeyMaterialUnavailable,
}
```

Зависимости:

- `ISettingsStore` — маркер режима;
- `IConnectionRepository` — проверка наличия сохранённых секретов для legacy-детекта;
- `IEnumerable<IKekSource>` — доступные источники;
- `SecretKeyService` — хранение KEK и шифрование;
- `ConnectionService` — `MigrateCredentialsAsync`, `ReEncryptSecretsAsync`;
- `AiSettingsService` — `ReEncryptAsync`;
- `IStorageTransaction` — атомарная перешифровка.

`MasterPasswordService` удаляется; `ChangePasswordAsync` делегирует в
`SwitchModeAsync(SecurityMode.Password, newPassword)`.

### 4.6. `SecretKeyService`

Убрать из `SecretKeyService` знание о провайдерах и режимах:

- оставить `IsUnlocked`, `Lock`, `RunExclusiveAsync`, `Protect`, `Unprotect`, `SetKek`;
- заменить `ChangeMasterPasswordAsync` на:

```csharp
public KekRotation BeginRotation(byte[] newKek)
{
    var oldKek = RequiredKek();
    return new KekRotation(_cryptoService, this, oldKek, newKek);
}
```

- `MasterPasswordRotation` переименовать в `KekRotation` (или оставить имя и изменить XML-doc;
  предпочтительно переименовать, так как теперь это не только про пароль).

### 4.7. `ConnectionService`

- Удалить `UnlockAsync`, `TryAutoUnlockAsync`, `IsUnlocked`, `Lock` — эти обязанности переходят
  в `SecurityModeService`.
- `MigrateCredentialsAsync` сделать доступным для `SecurityModeService` (`public` или `internal`).
- `GetSecretAsync`, `ResolveTargetAsync`, `SaveAsync` продолжают использовать `SecretKeyService`.

Так избегаем циклической зависимости: `SecurityModeService` → `ConnectionService`, но не наоборот.

## 5. Ключи настроек и совместимость

### 5.1. Новые ключи

| Ключ | Значение |
|---|---|
| `security.kek_mode` | `password`, `keyring`, `none` |
| `security.kek_salt` | существующий ключ, только для `Password` |
| `security.kek_check` | существующий ключ, только для `Password` |
| `ai.api_key` | существующий ключ, наличие учитывается при детекте |

Файл `None`: `~/.mongotui/kek.key`.  
Keyring: service name `com.amest.mongotui`, key `kek`.

Схема SQLite не меняется: таблица `settings` уже есть.

### 5.2. Определение режима

| Условие | Результат |
|---|---|
| `security.kek_mode` есть и валиден | `Ready` с этим режимом |
| Маркера нет, есть `security.kek_salt` или `security.kek_check` | `Ready`, режим `Password` (legacy), после разблокировки записать маркер |
| Маркера нет, salt/check нет, сохранённых секретов нет | `FirstRun` |
| Маркера нет, salt/check нет, секреты есть | `Invalid`, `UnknownKeyMaterial` |
| `Password`, но salt/check отсутствуют | `Invalid`, `PasswordMaterialMissing` |
| `Keyring`, но keyring недоступен | `Invalid`, `KeyringUnavailable` |
| `Keyring`, keyring доступен, записи нет | если секретов нет — `Ready` (ключ создаётся заново при старте); иначе `Invalid`, `KeyringKeyMissing` |
| `None`, файла нет | если секретов нет — `Ready` (ключ создаётся заново при старте); иначе `Invalid`, `KekFileMissing` |

«Секреты есть» означает: хотя бы одно подключение с `Secret != null` или непустой `ai.api_key`.

Legacy-база без маркера, но с salt/check, должна открываться по паролю как раньше. После
успешного `UnlockWithPasswordAsync` записывается `security.kek_mode = password`.

`Invalid` не лечится автоматически. UI предлагает:

- `KeyringUnavailable`/`KeyringKeyMissing` — восстановить keychain/запись и повторить, либо
  сбросить сохранённые секреты (с подтверждением);
- `KekFileMissing` — восстановить файл из бэкапа, либо сбросить;
- `PasswordMaterialMissing`/`UnknownKeyMaterial` — только сброс.

## 6. Первый запуск

В `MainWindow.OpenStartupAsync` (`src/MongoTui.TUI/Views/MainWindow.cs:258`) до текущего
`TryAutoUnlockAsync`:

1. `DetectAsync`.
2. `FirstRun` → показать `SecurityModeDialog`.
3. `Invalid` → показать `SecurityErrorDialog`, не открывать менеджер подключений.
4. `Ready` → `TryAutoUnlockAsync`.

`SecurityModeDialog`:

- `RadioGroup` с вариантами; `Keyring` показывается только если `AvailableModes` содержит его;
- при выборе `Password` — два поля: пароль и подтверждение;
- при выборе `None` — предупреждение: «секреты защищены только правами файла; любой процесс под
  вашим пользователем сможет их прочитать»;
- `Password` выбран по умолчанию;
- OK/Cancel; отмена завершает приложение, а не выбирает режим молча.

После выбора:

```csharp
await _security.InitializeAsync(choice.Mode, choice.Password, CancellationToken.None);
```

`InitializeAsync`:

1. Проверяет, что режим доступен.
2. Вызывает `IKekSource.CreateAsync`.
3. Записывает `security.kek_mode` (и salt/check для `Password`).
4. Устанавливает KEK в `SecretKeyService`.
5. При ошибке записи маркера — удаляет созданный ключевой материал best-effort.

## 7. Штатный старт

```csharp
var detection = await _security.DetectAsync(token);

if (detection.State == SecurityState.FirstRun)
{
    // SecurityModeDialog
}
else if (detection.State == SecurityState.Invalid)
{
    // SecurityErrorDialog; выход
}
else
{
    var result = await _security.TryAutoUnlockAsync(token);
    if (result == AutoUnlockResult.PasswordRequired)
    {
        UnlockSecrets(); // существующий MasterPasswordDialog
    }
    else if (result == AutoUnlockResult.KeyMaterialUnavailable)
    {
        // SecurityErrorDialog; выход
    }
}
```

Отмена диалога пароля в режиме `Password` оставляет приложение в заблокированном состоянии,
как сейчас: менеджер подключений открывается, но операции с секретами требуют F8. Для
`Keyring`/`None` этот сценарий невозможен — они разблокируются автоматически.

`TryAutoUnlockAsync`:

- `Password` → `PasswordRequired`;
- `Keyring`/`None` → `TryGetKekAsync`;
  - ключ есть → `Unlocked` + `SetKek`;
  - ключа нет и сохранённых секретов нет → создать материал заново и разблокироваться;
  - ключа нет, но секреты есть → `KeyMaterialUnavailable` (никогда не создавать новый ключ);
- после успешной разблокировки — `ConnectionService.MigrateCredentialsAsync`.

Создание нового материала при отсутствии ключа допустимо только при полном отсутствии сохранённых
секретов (нет `Secret` ни у одного подключения и нет `ai.api_key`). Это не может привести к потере
данных, так как расшифровывать нечего.

## 8. Переключение режимов

### 8.1. Алгоритм

```csharp
public Task SwitchModeAsync(
    SecurityMode target,
    string? newPassword,
    CancellationToken cancellationToken) =>
    _secrets.RunExclusiveAsync(async token =>
    {
        var source = SourceFor(target);
        if (!source.IsAvailable) throw new DomainException("Security mode is not available.");

        if (target == SecurityMode.Password && string.IsNullOrEmpty(newPassword))
            throw new DomainException("Master password must not be empty.");

        var previousMode = CurrentMode ?? throw new InvalidOperationException("Security mode is not initialized.");

        KekRotation? rotation = null;

        if (target == SecurityMode.Password)
        {
            // salt/check и перешифровка коммитятся одной транзакцией.
            await _transaction.ExecuteAsync(async tx =>
            {
                var newKek = await source.CreateAsync(newPassword, tx);
                rotation = _secrets.BeginRotation(newKek);

                await _connections.ReEncryptSecretsAsync(rotation, tx);
                await _ai.ReEncryptAsync(rotation, tx);
                await _settings.SetAsync(ModeSettingKey, ToSettingValue(target), tx);
            }, token);
        }
        else
        {
            // Keyring/файл создаются вне SQLite-транзакции.
            var newKek = await source.CreateAsync(null, token);
            rotation = _secrets.BeginRotation(newKek);

            try
            {
                await _transaction.ExecuteAsync(async tx =>
                {
                    await _connections.ReEncryptSecretsAsync(rotation, tx);
                    await _ai.ReEncryptAsync(rotation, tx);
                    await _settings.SetAsync(ModeSettingKey, ToSettingValue(target), tx);
                }, token);
            }
            catch
            {
                await source.DeleteAsync(token); // best-effort cleanup
                throw;
            }
        }

        rotation!.Commit();
        if (previousMode != target)
        {
            await SourceFor(previousMode).DeleteAsync(token);
        }
        CurrentMode = target;
    }, cancellationToken);
```

Для `Keyring` → `Keyring` и `None` → `None` операция должна быть no-op без перешифровки.
Для `Password` → `Password` создаётся новый salt/check, а старые перезаписываются; удалять
материал после commit нельзя.

### 8.2. Порядок и транзакционность

| Шаг | `Password` | `Keyring` | `None` |
|---|---|---|---|
| Создание нового материала | внутри транзакции (salt/check) | до транзакции | до транзакции |
| Перешифровка секретов | в транзакции | в транзакции | в транзакции |
| Запись `security.kek_mode` | в транзакции | в транзакции | в транзакции |
| Удаление старого материала | после commit | после commit | после commit |

Правила:

- новый keyring-ключ/файл создаётся **до** открытия транзакции; при падении транзакции он
  удаляется best-effort, старый остаётся рабочим;
- `PasswordKekSource.CreateAsync` вызывается внутри транзакции, чтобы salt/check и данные
  коммитились атомарно;
- старый ключевой материал удаляется только после успешного commit;
- при `Password` → `Password` (смена пароля) генерируется новый salt, а не переиспользуется
  старый;
- при переходе в `Keyring`/`None` ключ всегда создаётся заново и перезаписывает существующий,
  чтобы не переиспользовать устаревшую запись/файл.

### 8.3. Матрица переходов

| From \ To | Password | Keyring | None |
|---|---|---|---|
| Password | смена пароля | подтверждение + перешифровка | подтверждение + перешифровка |
| Keyring | новый пароль + перешифровка | no-op | перешифровка |
| None | новый пароль + перешифровка | перешифровка | no-op |

`Keyring` недоступен на Linux, поэтому переходы в него/из него на Linux отклоняются на уровне
`AvailableModes` и UI.

## 9. UI

### 9.1. `SecurityModeDialog`

Новый файл `src/MongoTui.TUI/Views/SecurityModeDialog.cs`.

- Заголовок: `Security.Mode.Title`.
- Список режимов с пояснениями.
- Поля пароля/подтверждения только для `Password`.
- Предупреждение для `None`.
- `SelectedMode`, `Password`, `Cancelled`.
- `Enter` на кнопке по умолчанию запускает подтверждение.

### 9.2. `SecuritySettingsDialog`

Новый файл `src/MongoTui.TUI/Views/SecuritySettingsDialog.cs`.

- Показывает текущий режим.
- Доступные варианты переключения.
- Для `Password` — новые пароль и подтверждение.
- Для перехода из `Password` в `Keyring`/`None` — отдельное подтверждение.
- Вызывает `SecurityModeService.SwitchModeAsync`.
- После успеха `Changed = true`.
- Ошибки `DomainException` показываются в диалоге.

### 9.3. Меню и shortcuts

В `MainWindow.BuildMenuItems` (`src/MongoTui.TUI/Views/MainWindow.cs:373-379`):

```csharp
Item(localizer.Get("Menu.Connection.Unlock"), Key.F8, UnlockSecrets,
    enabled: _securityMode == SecurityMode.Password),
Item(localizer.Get("Menu.Connection.ChangePassword"), Ctrl(KeyCode.P), OpenChangePassword,
    enabled: _securityMode == SecurityMode.Password),
```

Пункт `Menu.Settings.Security` — новый, всегда доступен. После `SwitchModeAsync`:

- обновить `_securityMode`;
- вызвать `RefreshMenuBar()` (механизм уже есть в `MainWindow.cs:870+`);
- обновить статус.

`Item` уже регистрирует shortcut только для enabled-пунктов, поэтому `Ctrl+P` и `F8` перестанут
работать в `Keyring`/`None` автоматически.

### 9.4. Локализация

Добавить в `Strings.resx` и `Strings.ru.resx`:

- `Menu.Settings.Security`;
- `Security.Mode.Title`, `Security.Mode.Password`, `Security.Mode.Keyring`, `Security.Mode.None`;
- `Security.Mode.PasswordField`, `Security.Mode.ConfirmField`;
- `Security.Mode.NoneWarning`;
- `Security.Mode.NotAvailable`;
- `Security.Settings.Title`, `Security.Settings.Current`, `Security.Settings.Switch`;
- `Security.Settings.Switched`;
- `Security.Settings.DowngradeConfirm`;
- `Security.Error.KeyringUnavailable`, `Security.Error.KeyringKeyMissing`,
  `Security.Error.KekFileMissing`, `Security.Error.PasswordMaterialMissing`,
  `Security.Error.UnknownKeyMaterial`;
- `Security.Error.ResetSecrets`, `Security.Error.ResetConfirm`.

## 10. Изменения по файлам

### Application

| Файл | Изменение |
|---|---|
| `Models/SecurityMode.cs` | новый enum |
| `Models/SecurityModeDetection.cs` | новый record + `SecurityState`/`SecurityProblem` |
| `Abstractions/Security/IKekSource.cs` | новый интерфейс |
| `Abstractions/Security/IKekFileStore.cs` | новый интерфейс |
| `Services/SecurityModeService.cs` | новый сервис |
| `Services/SecretKeyService.cs` | убрать провайдеры/режимы, добавить `BeginRotation` |
| `Services/MasterPasswordRotation.cs` | переименовать в `KekRotation.cs` (или обобщить) |
| `Services/MasterPasswordService.cs` | удалить |
| `Services/ConnectionService.cs` | убрать unlock-методы, открыть `MigrateCredentialsAsync` |
| `DependencyInjection/ApplicationServiceCollectionExtensions.cs` | регистрация `SecurityModeService`, `KekRotation` не нужен в DI |

### Infrastructure

| Файл | Изменение |
|---|---|
| `Security/PasswordKekSource.cs` | новый, на базе `MasterPasswordKeyProvider` |
| `Security/KeyringKekSource.cs` | новый, на базе `KeyringKeyProvider` |
| `Security/FileKekSource.cs` | новый |
| `Security/FileKekFileStore.cs` | новый |
| `Security/LatchkeyKeyring.cs` | новый адаптер `IKeyring` |
| `Security/MasterPasswordKeyProvider.cs` | удалить/заменить |
| `Security/KeyringKeyProvider.cs` | удалить/заменить |
| `Security/FallbackKeyProvider.cs` | удалить |
| `DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | DI по ОС, Latchkey, источники KEK |
| `FileSystem/AppPaths.cs` | добавить `KekFile` |
| `MongoTui.Infrastructure.csproj` | `PackageReference Include="Latchkey"` |

### TUI

| Файл | Изменение |
|---|---|
| `Views/MainWindow.cs` | стартовый flow, `_securityMode`, меню, `SecuritySettingsDialog` |
| `Views/MasterPasswordDialog.cs` | зависимость от `SecurityModeService` |
| `Views/ChangeMasterPasswordDialog.cs` | зависимость от `SecurityModeService`, только `Password` |
| `Views/SecurityModeDialog.cs` | новый |
| `Views/SecuritySettingsDialog.cs` | новый |
| `Views/SecurityErrorDialog.cs` | новый (или переиспользовать `ErrorDialog` с actions) |
| `Resources/Strings.resx` | новые строки |
| `Resources/Strings.ru.resx` | новые строки |
| `Views/ShortcutsDialog.cs` | обновить, если меняются shortcuts |

### Корневые файлы

- `Directory.Packages.props` — `Latchkey` 0.1.0.
- `THIRD-PARTY-LICENSES.md` — Latchkey, MIT.
- `README.md`, `README_RU.md` — раздел о режимах и предупреждение для `None`.
- `CHANGELOG.md` — запись о новой возможности.

## 11. Latchkey: интеграция

Регистрация:

```csharp
if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
{
    services.AddSingleton<ILatchkey>(_ => LatchkeyFactory.Create(new LatchkeyOptions
    {
        ServiceName = "com.amest.mongotui",
        DisplayName = "MongoTui",
    }));
    services.AddSingleton<IKeyring, LatchkeyKeyring>();
}
else
{
    services.AddSingleton<IKeyring, UnavailableKeyring>();
}
```

`LatchkeyKeyring`:

```csharp
public sealed class LatchkeyKeyring : IKeyring
{
    private readonly ILatchkey _store;
    private readonly Lazy<bool> _available;

    public LatchkeyKeyring(ILatchkey store)
    {
        _store = store;
        _available = new Lazy<bool>(() =>
            Latchkey.Latchkey.VerifyPersistence("com.amest.mongotui"));
    }

    public bool IsAvailable => _available.Value;

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        await _store.GetBytesAsync(key, cancellationToken);

    public Task SetAsync(string key, byte[] value, CancellationToken cancellationToken) =>
        _store.SetAsync(key, value, cancellationToken).AsTask();

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _store.DeleteAsync(key, cancellationToken).AsTask();
}
```

Замечания:

- `VerifyPersistence` создаёт и удаляет тестовую запись; вызывать один раз, результат кэшировать.
- `ServiceName` менять нельзя после релиза — иначе старые записи станут недоступны.
- Windows Credential Manager ограничивает значение 2560 байтами; 32-байтный KEK помещается.
- На macOS возможен системный запрос доступа к Keychain. Это ожидаемо; в документации указать,
  что первый запуск может потребовать подтверждения.
- Если `GetBytesAsync` бросает `LatchkeyBackendUnavailableException`, `SecurityModeService`
  должен вернуть `KeyMaterialUnavailable`, а не падать.

## 12. Режим `None`

- KEK: `RandomNumberGenerator.GetBytes(32)`.
- Файл: `~/.mongotui/kek.key`.
- Unix: `0600`.
- Windows: файл создаётся в профиле пользователя; стандартные ACL. В README указать, что это
  защита от случайного просмотра/бэкапа БД, а не от malware под тем же пользователем.
- При удалении файла и наличии секретов — `Invalid`, `KekFileMissing`; новый файл не создаётся.
- При выборе `None` в UI показывать предупреждение и требовать подтверждение.
- `None` не является «без шифрования»: секреты в SQLite остаются зашифрованными, но ключ лежит
  рядом без пароля.

## 13. Миграция и обратная совместимость

1. Существующая база с `security.kek_salt`/`security.kek_check` открывается в режиме `Password`
   без выбора режима.
2. После успешной разблокировки записывается `security.kek_mode = password`.
3. Если база пустая (нет подключений с секретами и нет `ai.api_key`) и маркера нет — первый запуск.
4. Если база содержит секреты, но ключевой материал не найден — `Invalid`, без молчаливого
   создания нового ключа.
5. Старые настройки salt/check не удаляются при обычном запуске в `Password`.
6. При переходе из `Password` в другой режим salt/check удаляются после успешного commit.
7. При переходе в `Password` всегда создаётся новый salt; старый пароль не сохраняется.

## 14. Тестирование

### Unit

- `SecurityModeServiceTests`:
  - первый запуск, legacy password, invalid-состояния;
  - `AvailableModes` на Windows/macOS/Linux (через fake `IKekSource`);
  - `TryAutoUnlockAsync` для всех режимов;
  - запрет создания нового KEK при отсутствии материала и наличии секретов;
  - `InitializeAsync` для всех режимов;
  - `SwitchModeAsync` для всех переходов.
- `PasswordKekSourceTests`: создание/разблокировка/неверный пароль/удаление.
- `KeyringKekSourceTests`: fake `IKeyring`, отсутствие записи, перезапись, unavailable.
- `FileKekSourceTests`: temp-каталог, права `0600` на Unix, отсутствие файла, неверная длина.
- `KekRotationTests`: перешифровка и commit.
- `SecurityModeDetectionTests`: таблица из раздела 5.2.

Именование тестов по `AGENTS.md`:
`Should{Not|Throw}_{ExpectedOutcome}_When{Scenario}()`.

### Integration

- `SecurityModeTransactionTests` по образцу `MasterPasswordTransactionTests`:
  - все переходы перешифровывают connection secrets и AI key;
  - ошибка в середине транзакции откатывает изменения;
  - старый режим остаётся рабочим после отката;
  - старый ключевой материал удаляется только после commit.

### TUI

- `SecurityModeDialogTests`: доступность вариантов, пароль/подтверждение, отмена.
- `SecuritySettingsDialogTests`: переключение, подтверждение downgrade, ошибки.
- `SecurityMenuTests`: `ChangePassword`/`Unlock` включены только в `Password`; `Ctrl+P`/`F8`
  не срабатывают в других режимах.

### Keyring

- Unit-тесты с fake `IKeyring`/`ILatchkey` — на всех ОС.
- Интеграционные тесты с реальным keyring — только Windows/macOS, с trait
  `Category=Keyring` и skip при недоступности.
- Linux CI покрывает все режимы, кроме `Keyring`.

## 15. Критерии готовности

1. Чистая установка: при первом запуске показывается выбор режима; на Linux `Keyring` отсутствует.
2. `Password` работает как раньше; legacy-база открывается без миграции.
3. `Keyring` на Windows/macOS разблокируется без пароля; при недоступности keyring показывается
   понятная ошибка, секреты не теряются и новый ключ не создаётся.
4. `None` на всех ОС разблокируется без пароля; файл KEK имеет права `0600` на Unix; удаление
   файла при наличии секретов даёт `KekFileMissing`.
5. Все переходы режимов перешифровывают пароли подключений и AI API key в одной транзакции.
6. При сбое перешифровки старый режим остаётся рабочим.
7. `Change master password` и `Ctrl+P` доступны только в `Password`; `Unlock`/`F8` — только там же.
8. В логах и БД нет plaintext-секретов.
9. `dotnet build` без warnings, `dotnet test` зелёный.
10. README (EN/RU), CHANGELOG и THIRD-PARTY-LICENSES обновлены.

## 16. План PR и оценка

| PR | Содержание | Оценка |
|---|---|---|
| 1 | `SecurityMode`, `IKekSource`, `SecurityModeService`, `KekRotation`, рефакторинг `SecretKeyService`/`ConnectionService`; поведение по умолчанию — `Password` | 3–4 дня |
| 2 | `FileKekSource` + `FileKekFileStore` + режим `None` + тесты | 1–2 дня |
| 3 | Latchkey, `LatchkeyKeyring`, `KeyringKekSource`, DI по ОС, лицензия | 1–2 дня |
| 4 | `SecurityModeDialog`, `SecuritySettingsDialog`, ошибки, меню, локализация | 2–3 дня |
| 5 | Интеграционные тесты, README/CHANGELOG, финальная проверка | 1–2 дня |
| **Итого** | | **8–12 дней** |

PR 4 при превышении 400 строк дробить на «первый запуск» и «настройки/меню».

## 17. Риски и открытые вопросы

| Риск | Смягчение |
|---|---|
| Latchkey 0.1.0 — молодой пакет | Обернуть в `IKeyring`, зафиксировать версию, при проблемах заменить на собственный P/Invoke |
| macOS Keychain показывает запрос доступа | Отразить в README; первый запуск может требовать подтверждения |
| Windows: любой процесс пользователя читает Credential Manager | Указать в документации; это ограничение ОС |
| `None` создаёт ложное чувство защиты | Явное предупреждение в UI и README; не делать режимом по умолчанию |
| Потеря keyring-записи/файла KEK | Не создавать новый ключ молча; показывать ошибку и destructive reset |
| Сбой между созданием нового материала и commit | Порядок из раздела 8.2 + best-effort cleanup |
| Linux без keyring | `Keyring` скрыт; доступны `Password` и `None` |
| CI не может тестировать реальный keyring на Linux | Unit с fake + Windows/macOS job с trait `Category=Keyring` |

Открытые вопросы для продукта:

1. Оставлять ли `None` доступным на Linux? Рекомендация — да, это основной сценарий для headless.
2. Требовать ли повторный ввод текущего пароля при переходе `Password` → `None`/`Keyring`?
   Рекомендация — достаточно подтверждения в диалоге.
3. Нужен ли автоматический `Lock` после простоя? Вне области текущей задачи.

## 18. Ссылки на текущий код

- Старт и разблокировка: `src/MongoTui.TUI/Views/MainWindow.cs:258`, `:1113`.
- Диалог пароля: `src/MongoTui.TUI/Views/MasterPasswordDialog.cs`.
- Смена пароля: `src/MongoTui.TUI/Views/ChangeMasterPasswordDialog.cs`,
  `src/MongoTui.Application/Services/MasterPasswordService.cs`.
- KEK и шифрование: `src/MongoTui.Application/Services/SecretKeyService.cs`,
  `src/MongoTui.Application/Services/MasterPasswordRotation.cs`.
- Keyring/master-password провайдеры: `src/MongoTui.Infrastructure/Security/`.
- Регистрация DI: `src/MongoTui.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:40-44`.
- Перешифровка секретов: `src/MongoTui.Application/Services/ConnectionService.cs:53`,
  `src/MongoTui.Application/Services/AiSettingsService.cs:71`.
- Меню: `src/MongoTui.TUI/Views/MainWindow.cs:373-379`, пересборка `:870`.
