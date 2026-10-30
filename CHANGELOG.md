# Changelog

All notable changes to this project are documented in this file.

## v0.8.0 — since v0.7.0

### Added

- **Local web interface** — run `mongo-tui web --open` to browse databases and collections,
  execute JavaScript, view Table/Tree/JSON results, edit documents, manage indexes and use
  the AI assistant in a browser. The host listens only on `127.0.0.1` (port `8765` by default).
  Web assets are embedded in the existing executable; no separate frontend deployment is needed.
- **Web sign-in** — master-password authentication or a single-use launch link for first setup
  and keyring/local-key-file modes, with expiring browser sessions, sign-out and revocation
  when security settings change.
- **Web user management** — database and cluster permissions, user editing, password generation
  and one-time credentials, with localized errors, current-user protection and cancellation.
- **Web transfers and history** — JSON array/JSON Lines import, canonical JSON Lines export,
  downloading displayed query results, and reopening saved history entries without executing them.
- **Query deadlines** — a shared **Settings → Queries** timeout for TUI and Web: 30 seconds
  by default, `0` to disable. Web queries can also be cancelled from the workspace.
- **Application logs** — a Web log viewer and persisted logging-level settings in both clients,
  with live updates and a diagnostic environment override.
- **Co-author credit** — `--version` and **Help → About** credit Medvedev Denis (medved91)
  below Copyright in English and Russian.

### Changed

- Creating a database now asks for its first collection and creates it on MongoDB immediately.
  Saved connections stay visible in the explorer after disconnecting.
- Both clients share document-editing and permission models, connection preparation and
  localization keys. Document text, copied data and default JSON exports use Canonical Extended JSON.
- Framework-dependent builds now require the ASP.NET Core 10 runtime as well as the .NET 10 runtime;
  self-contained release archives include both. Source builds default to version `0.8.0`.

### Fixed

- Nested TUI dialogs apply asynchronous results without hanging on saving/loading or requiring
  another keypress, including connection forms and document/user operations.
- Opening a document reloads it by its original `_id` through the driver, preserving numeric
  types lost in shell output. Editing is refused when Extended JSON cannot preserve the loaded BSON.
- Document actions keep the source of the executed query. Web refresh after saving uses the
  successful query snapshot and preserves an unsubmitted editor draft.
- Web pagination commits the page only after a successful read, keeps the applied filter while
  preserving a draft, and retains the previous results after errors or cancellation.
- Reaching the Web tab limit displays an error without ending the browser circuit; editor
  selection remains visible in the active line.
- Security initialization checks persisted state before creating new key material. Failed Web
  password attempts do not lock an encryption key already unlocked by another session.

### Known limitations

- Saving replaces the complete document without detecting writes made after the editor opened;
  a concurrent update can be overwritten. Driver reads also normalize `Double(-0.0)` to `+0.0`,
  which is then persisted on save. See [BSON editing limits](docs/bson-json.md).
- Cancelling or timing out a write does not undo changes already accepted by MongoDB.
  Web transfers are capped at 32 MiB and exports at 10,000 documents; SOCKS transfers remain unavailable.

### Docs and tests

- Added Web, BSON-editing and logging guides; updated installation, runtime and release instructions.
- Added Web security, pagination and UI regression coverage, BSON round-trip checks with real MongoDB
  and `mongosh`, and a separate integration-test job in CI.

## v0.7.0 — since v0.6.0

### Added

- **User management** — manage MongoDB users from the tree context menu: right-click a database,
  or a connection for the `admin` database. Create, edit and delete users, assign `read` /
  `readWrite` / `dbOwner` access per database and cluster roles (`readAnyDatabase`,
  `readWriteAnyDatabase`, `clusterAdmin`, `clusterManager`, `clusterMonitor`, `root`),
  generate passwords, preserve unmanaged roles on edit, and copy the password or a ready-to-use
  connection string after creating a user or changing a password.
  Passwords and connection strings are shown once and are never persisted or logged.

### Docs

- AGENTS.md: documented asynchronous requests, UI dispatch around the Terminal.Gui 2.5 timer
  lock, and regression testing for nested dialogs with background request completion.

### Tests

- Added coverage for user save success and failure, refreshing users, copying generated
  credentials after creation or password changes, and credential dialog layout and clipboard
  errors in English and Russian.

## v0.6.0 — since v0.5.3

### Added

- **Security modes** — choose on first launch how the encryption key is protected: a master
  password (default), the OS keyring on macOS and Windows, or a local KEK file with user-only
  permissions. The mode can be changed later in **Settings → Security…**; switching modes
  re-encrypts all saved secrets in one transaction.
- Keyring integration on macOS and Windows via Latchkey. Linux supports the master-password
  and local-key-file modes.
- **Reset secrets** in the security settings removes saved connection passwords and the AI API
  key, then lets you choose a new security mode.
- Existing master-password installations are detected automatically and keep working without
  re-entering secrets.
- **Connection test feedback** — show a localized running status immediately and disable
  **Test** until the request finishes. Results include a success/error label and error details;
  successful tests are green and errors are red.

### Changed

- **Unlock secrets** (`F8`) and **Change master password** (`Ctrl+P`) are available only in
  master-password mode.
- Testing a connection no longer requires or fills in a profile name; a name is still required
  when saving. Connection strings and SSH settings remain validated.
- Connection tests are cancelled after 10 seconds, with a localized timeout message and
  **Test** enabled again.

### Fixed

- Automatic unlock validates the key against every stored connection password and AI API key
  before accepting it or migrating legacy credentials; a mismatched key leaves secrets locked.
- Resetting secrets also removes passwords embedded in legacy connection URIs. Connection forms
  strip URI credentials even when separate login/password fields are already populated, keeping
  explicitly entered values and filling missing fields from the URI.
- Connection form errors and test results appear below all fields and above the action buttons,
  without overlapping authentication options. Status changes repaint without another keypress.
- Locked-secret errors use the selected interface language in both the connection form and
  connection manager.
- Connection test failures, including network timeouts, are displayed in the form with their
  cause instead of only being logged.

### Docs

- README (EN / RU): documented security mode selection, switching modes and local key-file
  storage. Updated third-party license notices for Latchkey and its dependencies.

### Tests

- Added tests for security mode detection, initialization, automatic unlock, switching and
  reset, plus password, keyring and local key-file storage.
- Retained transaction rollback, cancellation and concurrent-save coverage for master-password
  rotation after moving it to the security mode service.
- Added coverage for connection status localization, colors and layout, testing without a name,
  required names when saving, and cancellation after the 10-second timeout.
- Added Terminal.Gui rendering tests with delayed responses to check progress and results in
  nested dialogs without extra keyboard input.

## v0.5.3 — since v0.5.2

### Fixed

- The connection manager shows and enables **Copy URI** only when a saved connection is
  selected; the button is hidden when no connection is selected or the list is empty.

## v0.5.2 — since v0.5.1

### Added

- The AI assistant receives the active database, the database's collections and the sampled
  field names with their BSON types, so it can generate queries against the real schema.
- The AI assistant dialog shows a requesting state and disables **Ask** while a request is
  in flight.

### Changed

- Release archives ship without debug symbols: publishing disables `DebugType` /
  `DebugSymbols` and removes leftover `.pdb` files from the artifact directory.

### Fixed

- In the AI assistant dialog, **Enter** in the prompt runs the request instead of closing
  the dialog.
- The AI assistant runs the request off the UI thread and applies the response from the
  dialog's main loop, so the answer is shown and the dialog no longer stays stuck on
  **Requesting…**.

## v0.5.1 — since v0.5.0

### Changed

- Running a query uses **Ctrl+R** (or `F5`) instead of `Ctrl+Enter`; some terminal emulators,
  notably on macOS, intercept `Ctrl+Enter` for their own context menu, and report
  `Shift+Enter` as a plain newline.
- Saving or testing a connection fills empty login and password fields from credentials
  embedded in the URI, so a pasted connection string works without the **From URI** button.

## v0.5.0 — since v0.4.0

### Added

- **Create database and collection** — right-click a connection to add a database, or a
  database to create a collection. A new database stays in the tree until refresh or
  reconnect; creating its first collection persists it on the server.
- **New query** — right-click a database to open an empty query tab bound to it, or a
  collection to open a tab with a ready-to-run `find` query, as with a double-click.
- **Empty database discovery** — the tree also lists databases known from the connection
  URI, authentication source and granted privileges that the server's catalog omits.
- **Keyboard shortcuts dialog** — **Help → Keyboard shortcuts…** lists every command and its
  hotkey, including why copy uses `Ctrl+C` on macOS.

### Changed

- Collection query tabs start from `db.getCollection(...)`; mongosh already runs with the
  tab's database, so the `getSiblingDB` wrapper is no longer inserted into the template.
- Index creation uses **Unique** and **Background** checkboxes instead of a `y/n` text field.

### Fixed

- Preserve the original document id during editing and deletion; reject id changes and
  report missing documents correctly, including unchanged replacements.
- Resolve document actions from the executed query's database and collection. Disable
  editing and deletion for ambiguous scripts, projections, aggregations and results without ids.
- Rotate the master password, connection secrets and AI API key in one SQLite transaction.
  Roll back on failure or cancellation and serialize concurrent secret storage operations.
- Extract and encrypt credentials from full connection URIs before saving; migrate legacy
  profiles after unlock. Remove scripts, raw diagnostics and exception contents from logs.
- Copy the selected text to the system clipboard with **Ctrl+C** in the editor, results and
  dialogs. When the platform clipboard is unavailable (headless Linux, SSH without `xclip`),
  the text is written through the OSC 52 terminal escape sequence instead of being dropped.

## v0.4.0 — since v0.3.0

### Added

- **Query editor autocomplete** — press `Ctrl+Space` or choose **Query → Autocomplete**
  for database, collection and field names, including dotted paths through nested objects
  and arrays of documents.
- **Database selection** — completing `db.` lists databases and inserts
  `db.getSiblingDB("…")`; collections can then be selected after the next dot and are
  inserted through `getCollection("…")`, including names that overlap with shell methods.
- **Method completion** — `db.get` suggests `getSiblingDB` and `getCollection`; collections
  offer `find`, `findOne`, `countDocuments` and `estimatedDocumentCount`. Supported cursor
  chains offer `limit`, `skip`, `sort`, `toArray` and `count` after `find(...)` and `limit(...)`.
  Method suggestions open automatically while typing a prefix or a dot after a supported call
  and require no database request.
- **Completion controls** — filter suggestions while typing, choose with `↑` / `↓`, insert
  with `Enter` / `Tab`, and dismiss or cancel loading with `Esc`. Method completion inserts
  parentheses and places the caret inside arguments, preserving existing call arguments
  when correcting a method name.

### Changed

- Collection fields are sampled from up to 20 documents, with nested paths up to eight levels.
  The field cache is scoped to the connection, database and collection, expires after one
  minute, and is cleared by `F9` or running a query.

### Fixed

- Query execution now selects the tab's database before every run, so
  `db.getCollection(...).find({}).toArray()` works without an explicit `getSiblingDB(...)`.
  Explicit database selection in the script still takes precedence; tabs without a selected
  database retain the connection's default database.
- Completion replaces the entire name when the caret is inside a token, correctly handling
  escaped quotes, backslashes and Unicode escapes while preserving the surrounding query.
- Pending completion requests are cancelled and stale suggestions discarded when editing,
  changing focus, switching or closing tabs, refreshing, or disconnecting.

### Docs

- README (EN / RU): documented autocomplete controls, method chains, field sampling,
  cache invalidation and supported contexts.
- Clarified AI configuration and existing TUI limitations for result views, transfers,
  database backups and query history.

### Tests

- Added unit and TUI tests for completion contexts, escaping, cancellation, cache isolation,
  method chains, popup rendering, keyboard selection and caret placement.
- Added MongoDB Testcontainers tests for completed queries, escaped collection names,
  names that overlap with shell methods, and database selection during query execution.

## v0.3.0 — since v0.2.0

### Added

- **SSH / TPAM SOCKS5 connections** — configure an SSH host or config alias, port, username,
  optional private key and local SOCKS port per connection profile. **Connect** and **Test**
  automatically start an OpenSSH proxy; local port `0` selects an available port.
- **SSH authentication dialogs** — answer keyboard-interactive prompts, including 2FA after
  key authentication, inside the TUI. Responses are never saved; ssh-agent and SSH config
  can be used without specifying a key file.
- **Proxy routing** — apply the local SOCKS proxy to `mongodb://` and `mongodb+srv://`
  connections without changing the saved URI, MongoDB hosts or other connection options.
- **Version information** — `--version` and **Help → About** show the application version,
  `MIT License` and `Copyright (c) 2026 Klabukov Erik (AMEST)`.
- **License files** — added the MIT license and third-party license notices; both files
  are included in build output and release archives.

### Changed

- Release builds now use the Git tag as the application version, without a commit-hash suffix.
- Import/export and backup/restore via MongoDB Database Tools are blocked for SOCKS-proxied
  connections with a localized explanation, because the tools ignore SOCKS settings.

### Fixed

- SSH processes are closed on disconnect, connection/test failure and application exit;
  cached MongoDB clients are released when their tunnel closes.
- Reconnecting with a fixed local SOCKS port closes the previous tunnel before starting
  its replacement.
- Closing the connection form cancels a pending connection test.

### Docs

- README (EN / RU): documented SSH/TPAM setup, authentication, proxy limitations and
  dependency requirements, including OpenSSH 8.4+, MongoDB Shell and Database Tools.

### Tests

- Added tests for SSH startup, authentication, cancellation, cleanup, proxy routing,
  transfer restrictions and version/About output.
- Added Coverlet coverage collection and a unit-test coverage summary in CI.

## v0.2.0 — since v0.1.0

### Added

- **Keyboard-only navigation** — `Tab` / `Shift+Tab` move focus between the connection tree,
  the query editor and the result pane; `Ctrl+Tab` inserts a tab character in the editor.
  The app is fully usable without a mouse.
- **Context menus** — right-click (or `Shift+F10`) a query editor or the result pane:
  - Editor: **Run** the query.
  - Result: **Copy all**, **Copy document**, **Export**, **Edit document**, **Delete document**.
- **`Ctrl+Enter`** runs the active query, alongside `F5`.
- **Delete document** asks for confirmation and shows the generated id filter.
- Result actions (copy / edit / delete a document) now target the document **under the cursor**
  in all three view modes (Table, Tree, JSON); previously they always used the first document.

### Fixed

- **Paste** in the query editor, document-edit and translate dialogs now repaints immediately
  instead of only after an unrelated redraw (workaround for a Terminal.Gui `TextView` defect).
- **Master password dialog** — `Enter` now submits the password.
- **Default dialog button** is preserved, so `Enter` triggers the intended action instead of the
  last-added (Cancel / Close) button.
- **Localization** is fully re-applied (title, tree, panes, menu, status bar) when switching
  language.
- Russian README menu names corrected.

### Docs

- README (EN / RU): dropped the OS-keyring note; documented running with the master password only.
- macOS install: added `xattr` commands to clear quarantine on `libe_sqlite3.dylib` and
  `libonigwrap.dylib`.

[v0.7.0 to v0.8.0]: https://github.com/AMEST/mongo-tui/compare/v0.7.0...v0.8.0
[v0.6.0 to v0.7.0]: https://github.com/AMEST/mongo-tui/compare/v0.6.0...v0.7.0
[v0.5.3 to v0.6.0]: https://github.com/AMEST/mongo-tui/compare/v0.5.3...v0.6.0
[v0.5.2 to v0.5.3]: https://github.com/AMEST/mongo-tui/compare/v0.5.2...v0.5.3
[v0.5.1 to v0.5.2]: https://github.com/AMEST/mongo-tui/compare/v0.5.1...v0.5.2
[v0.5.0 to v0.5.1]: https://github.com/AMEST/mongo-tui/compare/v0.5.0...v0.5.1
[v0.4.0 to v0.5.0]: https://github.com/AMEST/mongo-tui/compare/v0.4.0...v0.5.0
[v0.3.0 to v0.4.0]: https://github.com/AMEST/mongo-tui/compare/v0.3.0...v0.4.0
[v0.2.0 to v0.3.0]: https://github.com/AMEST/mongo-tui/compare/v0.2.0...v0.3.0
[v0.1.0 to v0.2.0]: https://github.com/AMEST/mongo-tui/compare/v0.1.0...v0.2.0
