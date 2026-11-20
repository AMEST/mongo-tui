# Тестирование MongoTui в tmux

Инструкция для агента, который проверяет TUI в живом терминале. Используйте
отдельную сессию tmux, тестовую MongoDB на localhost и временный каталог
настроек. Пользователь может наблюдать за той же сессией из своей вкладки.

## Подготовка стенда

Выполните команды из корня репозитория. Нужны Docker, tmux, .NET и `mongosh`
в `PATH`. Docker выделит свободный порт на `127.0.0.1`.

```sh
dotnet build src/MongoTui.TUI/MongoTui.TUI.csproj

check_dir=$(mktemp -d /tmp/mongotui-tui.XXXXXX)
check_mongo="mongotui-tui-$(basename "$check_dir")"
check_socket="$check_dir/tui.sock"

docker run --rm -d --name "$check_mongo" -p 127.0.0.1::27017 mongo:7
check_address=$(docker port "$check_mongo" 27017/tcp)
printf 'mongodb://%s/diag_tui_a\n' "$check_address"
docker exec "$check_mongo" mongosh --quiet --eval 'db.adminCommand({ping: 1})'
```

Дождитесь успешного `ping`, затем наполните две тестовые БД. Сохраните
исходный ответ `profile: -1` для сверки после выхода.

```sh
docker exec "$check_mongo" mongosh --quiet --eval '
const a = db.getSiblingDB("diag_tui_a");
const b = db.getSiblingDB("diag_tui_b");
a.orders.insertMany(Array.from({length: 200}, (_, seq) => ({
    seq, status: seq === 0 ? "new" : "closed"
})));
a.orders.createIndex({status: 1}, {name: "status_1"});
b.orders.insertOne({seq: 0, status: "new"});
printjson(a.runCommand({profile: -1}));
' > "$check_dir/profile-before.txt"

mkdir -p "$check_dir/data"
tmux -f /dev/null -S "$check_socket" new-session -d -s diagnostics -x 120 -y 40 \
    "exec env LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 MONGOTUI_DATA_DIR='$check_dir/data' dotnet '$PWD/src/MongoTui.TUI/bin/Debug/net10.0/mongo-tui.dll'"
tmux -S "$check_socket" set-option -g status off
tmux -S "$check_socket" set-option -g remain-on-exit on
tmux -S "$check_socket" resize-window -t diagnostics -x 120 -y 40
tmux -S "$check_socket" capture-pane -p -t diagnostics
```

Запишите имя контейнера, URI и путь к сокету. Используйте этот сокет во всех
последующих командах tmux. Дождитесь окна приложения на первом снимке экрана.

## Команда для наблюдателя

Как только TUI откроется, отправьте пользователю вывод этой команды. Он содержит
абсолютный путь к сокету и готов для вставки в отдельную вкладку терминала.

```sh
printf "tmux -S '%s' attach-session -r -t diagnostics\n" "$check_socket"
```

`-r` открывает сессию для просмотра. Агент продолжает управлять приложением
через `send-keys`, а пользователь видит тот же экран. Для выхода из просмотра
нажмите **Ctrl+B**, затем **D**. Для самостоятельного ввода подключитесь без
`-r` после завершения действий агента.

## Управление и снимки экрана

Снимайте экран перед вводом, чтобы проверить окно и фокус. После операции
дождитесь ожидаемого текста на снимке. Сохраняйте снимки в `check_dir`.
Флаг `-e` в `capture-pane` добавляет цвета и атрибуты для проверки фокуса.

```sh
tmux -S "$check_socket" capture-pane -p -t diagnostics
tmux -S "$check_socket" resize-window -t diagnostics -x 80 -y 24
tmux -S "$check_socket" capture-pane -p -t diagnostics > "$check_dir/compact.txt"
tmux -S "$check_socket" resize-window -t diagnostics -x 120 -y 40
```

Переведите фокус в редактор и передайте запрос через `send-keys -l`. Отправляйте
управляющие клавиши отдельной командой.

```sh
tmux -S "$check_socket" send-keys -t diagnostics -l -- 'db.orders.find({status:"new"}).limit(10).toArray()'
tmux -S "$check_socket" send-keys -t diagnostics F8
tmux -S "$check_socket" capture-pane -p -t diagnostics > "$check_dir/explain.txt"
```

## Сценарии проверки

1. **Подключение.** В первом запуске выберите файл ключа. Создайте профиль
   с URI из подготовки стенда. Нажмите **Test**, дождитесь успеха, сохраните
   профиль и выберите **Connect**. В дереве раскройте подключение и
   `diag_tui_a` клавишей **Enter**, затем откройте коллекцию `orders`.
2. **Explain.** Замените запрос в редакторе на пример выше и нажмите **F8**
   до запуска изменённого запроса. Ожидаются `IXSCAN`, индекс `status_1` и
   один возвращённый документ. В диалоге задайте `db.orders.findOne({seq: 0})`
   и повторите **F8**. Проверьте `COLLSCAN` и возврат в редактор по **Esc**.
   Отдельно выделите одно выражение в многострочном скрипте и сверьте текст
   в Explain.
3. **Диагностика и раскладка.** Выполните запрос с `.toArray()` через **F5**.
   Откройте **Diagnostics** и проверьте Overview, Slow queries, Database и
   Server. На **120×40** и **80×24** пройдите поля, таблицы и кнопки через
   **Tab** и **Shift+Tab**. Убедитесь, что поле лимита видно и подписи читаемы.
   Повторите открытие Diagnostics, Profiler и Explain на русском и английском.
4. **Профайлер.** Отмените подтверждение **Server all** и сверьте исходное
   состояние через `mongosh`. Затем примените полный режим к `diag_tui_a` с
   восстановлением при выходе и таймером `0`. Проверьте фактический уровень
   на сервере, имя БД в сводке и индикатор. Выйдите через **File → Exit** или
   **Ctrl+Q**. Повторный `profile: -1` должен показать исходные уровень,
   фильтр, `slowms` и `sampleRate`.
5. **Задержка ответов.** Выполните ANSI-регрессии. Они открывают вложенный
   диалог, завершают запрос из фонового продолжения и проверяют перерисовку
   без дополнительного ввода.

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test tests/MongoTui.TUI.Tests --no-restore \
    --filter 'FullyQualifiedName~ExplainDialogRenderingTests|FullyQualifiedName~ProfilerDialogRenderingTests|FullyQualifiedName~DiagnosticsLayoutTests'
```

Для задачи, затрагивающей SSH, повторите сценарий выхода с отдельным тестовым
SSH-профилем. Сверьте `profile: -1` через независимое подключение к MongoDB.
Так можно проверить, что восстановление завершилось до закрытия туннеля.

## Завершение

После штатного выхода проверьте код завершения в панели и состояние MongoDB.
Сообщите пользователю результаты живого прогона и автоматических тестов.
Для каждого дефекта укажите язык, размер окна, шаг и наблюдаемый результат.
Остановите выделенную сессию и контейнер после завершения проверки.

```sh
tmux -S "$check_socket" display-message -p -t diagnostics '#{pane_dead}:#{pane_dead_status}'
docker exec "$check_mongo" mongosh --quiet --eval 'printjson(db.getSiblingDB("diag_tui_a").runCommand({profile: -1}))'
tmux -S "$check_socket" kill-server
docker stop "$check_mongo"
```
