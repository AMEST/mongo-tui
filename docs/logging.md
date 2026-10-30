# Application logging

TUI and Web write through standard `Microsoft.Extensions.Logging.ILogger<T>` loggers
to the same Serilog file sink: `~/.mongotui/logs/app-.log`, with daily rotation and
seven retained files. `MONGOTUI_DATA_DIR` changes the application data directory.
The Web Logs page reads complete files; no tail limit is applied.

## Level preferences

Choose **Settings → Logging** in either TUI or Web. Supported levels are `Verbose`,
`Debug`, `Information`, `Warning`, `Error` and `Fatal`. The preference is stored as
`logging.level` in the existing `settings` table of `~/.mongotui/mongotui.db`.
The default is `Information`; an absent or invalid stored value uses that default.
No separate JSON configuration file is required. `serilog.json` is no longer read.

Saving first persists the selected level, then applies it to the running logger.
If saving fails, the active level remains unchanged. Existing loggers keep working;
changing levels does not recreate them or interrupt the log viewer. Other running
processes using the same data directory pick up the stored preference every three
seconds. The preference is global to the application, not a MongoDB connection or
browser tab. An already open settings form reloads the saved choice when reopened.

Microsoft, System and MongoDB categories use at least `Warning`, becoming stricter
when the selected level is `Error` or `Fatal`. Application `Debug`/`Verbose` does not
enable framework payload logging. Standard .NET `Trace` maps to Serilog `Verbose`.
Increasing verbosity affects subsequent events only.

## Startup and diagnostic override

File logging is available before SQLite opens, initially at `Information`. The
saved level is loaded before the terminal UI or Web host starts. This setting is
not encrypted and does not require unlocking stored secrets. A polling read failure
keeps the last active level; the monitor retries and reports the first failure.

`MONGOTUI_Serilog__MinimumLevel__Default=Debug` temporarily overrides the stored
level for that process, including early startup. The settings UI displays the
active override separately from the saved choice. Saving another choice does not
replace the override; it takes effect after starting without the variable.
An invalid environment level is ignored with a warning.

Precedence is **built-in default → SQLite preference → environment override**.
The override is never written into the database. Other `Serilog` environment keys
and category overrides from JSON are no longer read.

## Diagnostics and sensitive data

Categories identify the source class. Standard scopes attach connection IDs,
tab IDs and namespaces where available. Query completion records contain duration
and result count; document/user writes and connection changes are recorded at
`Information`. Internal stages and shell output sizes are `Debug`. Timeout and
cancellation have separate messages.

Do not include connection strings, credentials, query/filter text, document bodies
or command output in logging calls at any level. Exception diagnostics contain types
and stack frames without exception messages, `Data` or MongoDB responses.
`ExceptionDiagnostics` is a pure formatter, not an operation runner or logger.
Shell scripts remain in the separately managed query history.
