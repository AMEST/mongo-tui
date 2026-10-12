# AGENTS.md

Проект: MongoDB TUI Viewer (C# / .NET 10, Terminal.Gui 2.5).

## Архитектура
- Onion: Domain → Application → Infrastructure → TUI.
- Domain не зависит ни от чего, кроме System.* и MongoDB.Bson.
- Application зависит только от Domain.
- Infrastructure реализует интерфейсы Application.
- TUI знает только Application; Infrastructure подключается в Program.cs через DI.

## Принципы
- SOLID, SRP, KISS, DRY. Никаких абстракций "на будущее".
- Класс < 300 строк, метод < 40 строк.
- Всё через конструкторный DI. Никаких `new` сервисов внутри сервисов.
- Async всюду, где есть I/O. `CancellationToken` — всегда последний параметр.
- Никаких `static` кроме чистых утилит без состояния.

## Стиль C#
- `nullable enable`, `TreatWarningsAsErrors=true`.
- `using`-директивы — перед file-scoped `namespace`.
- File-scoped namespaces.
- Точка входа (`Program.cs`) — top-level statements, без `class Program` и `Main`.
- `var` — когда тип очевиден из правой части.
- Records для DTO/иммутабельных моделей.
- `sealed` для классов по умолчанию.
- Никаких регионов.

## Нейминг
- Интерфейсы: `IName`.
- Приватные поля: `_camelCase`.
- Async-методы: суффикс `Async`.
- Тесты: файл `XxxTests.cs`, класс `XxxTests`.

## Тесты
- Фреймворки: xUnit + FakeItEasy + AwesomeAssertions.
- Именование тестов: `Should{Not|Throw}_{ExpectedOutcome}_When{Scenario}()`.
- AAA-разделение пустыми строками.
- Один `Act` на тест.
- Интеграционные тесты — в `MongoTui.IntegrationTests` с Testcontainers.
- Покрытие: Domain ≥ 90%, Application ≥ 80%, Infrastructure — по критичным местам.

## Логирование
- Serilog, только в файл `~/.mongotui/logs/app-.log`.
- Никогда не логировать connection string, пароли, содержимое документов.
- Уровни: Debug — детали, Info — действия пользователя, Warning — recoverable, Error — баги.

## Локализация
- Все строки — через `.resx` (en default, ru вторым).
- Логи и исключения — по-английски.

## Коммиты
- Conventional Commits: `feat:`, `fix:`, `test:`, `chore:`, `docs:`, `refactor:`.
- Заголовок ≤ 72 символов, императив.

## Definition of Done
- Собирается без warnings.
- Все тесты зелёные (`dotnet test`).
- Новая функциональность покрыта тестами.
- Нет закомментированного кода и `TODO` без issue-ссылки.
- Публичные API задокументированы XML-doc (только там, где неочевидно).
- PR ≤ 400 строк изменений, иначе дробить.

## Do / Don't
- DO: маленькие PR, явные типы в публичных API, DI, тесты рядом с фичей.
- DON'T: `Thread.Sleep`, `.Result`/`.Wait()` в async-коде, `Console.WriteLine` (ломает TUI), `dynamic`, обход интерфейсов.
