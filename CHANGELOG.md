# Changelog

All notable changes to this project are documented in this file.

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

[v0.5.0 to v0.5.1]: https://github.com/AMEST/mongo-tui/compare/v0.5.0...v0.5.1
[v0.4.0 to v0.5.0]: https://github.com/AMEST/mongo-tui/compare/v0.4.0...v0.5.0
[v0.3.0 to v0.4.0]: https://github.com/AMEST/mongo-tui/compare/v0.3.0...v0.4.0
[v0.2.0 to v0.3.0]: https://github.com/AMEST/mongo-tui/compare/v0.2.0...v0.3.0
[v0.1.0 to v0.2.0]: https://github.com/AMEST/mongo-tui/compare/v0.1.0...v0.2.0
