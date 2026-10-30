# MongoTui

**English** | [Русский](README_RU.md)

A MongoDB client for your terminal and browser. Browse databases, run queries, edit documents,
and manage connections from the TUI or local Web interface. Available for Linux, macOS and Windows,
with English and Russian interfaces.

| Main window | Connection manager |
|---|---|
| [![Main window](docs/work.jpg)](docs/work.jpg) | [![Connection manager](docs/connection-manager.jpg)](docs/connection-manager.jpg) |

| Document editor | Master password |
|---|---|
| [![Document editor](docs/insert-document.jpg)](docs/insert-document.jpg) | [![Master password](docs/master-password.jpg)](docs/master-password.jpg) |

| Web workspace | Web settings |
|---|---|
| [![Web workspace](docs/web-main.png)](docs/web-main.png) | [![Web settings](docs/web-setting.png)](docs/web-setting.png) |

## Features

- Terminal and [local Web](docs/web-interface.md) interfaces in one executable, sharing saved profiles, settings and history.
- Saved connections with encrypted passwords, selectable security modes (master password, OS keyring on macOS/Windows, or a local key file) and SSH/TPAM support with 2FA.
- Database and collection browser, index management, user management and statistics.
  Empty databases known from the URI, authentication source or user privileges remain visible in the tree.
  Right-click a connection to create a database with its first collection, or a database to create a collection or open an empty query tab bound to it. Creating a database writes to the server immediately.
  Right-click a database (or a connection for `admin`) to create, edit and delete MongoDB users, assign database and cluster roles, generate passwords and copy a ready-to-use connection string.
- JavaScript query tabs with autocomplete and query history.
- Table, Tree and JSON result views; document editing as Canonical Extended JSON with BSON type checks.
- Collection import and export in JSON.
- AI query assistance and translation through an OpenAI-compatible API.

## Requirements

[Requirements and dependency installation](docs/requirements.md):
[macOS](docs/requirements.md#macos) · [Linux](docs/requirements.md#linux) · [Windows](docs/requirements.md#windows).

## Installation

Download the archive for your platform from [Releases](https://github.com/AMEST/mongo-tui/releases)
and extract it in full. The .NET runtime is included.

Run `mongo-tui --version` to check the build version, license and author credits.

### macOS — required before first launch

**Release binaries are unsigned and not notarized, so Gatekeeper blocks downloaded builds.**   
Before launching a downloaded build for the first time, open a terminal in the extracted
application directory and remove the quarantine attribute from the executable and its libraries:

```sh
xattr -d com.apple.quarantine ./mongo-tui
xattr -d com.apple.quarantine ./libe_sqlite3.dylib
xattr -d com.apple.quarantine ./libonigwrap.dylib
chmod +x ./mongo-tui
./mongo-tui
```

### Linux

```sh
chmod +x ./mongo-tui
./mongo-tui
```

### Windows

Run `mongo-tui.exe`.

## Web interface

```sh
mongo-tui web --open
```

The default address is `http://127.0.0.1:8765`; use `--port 9000` to change it.
Running `mongo-tui` without arguments still starts the terminal interface.
Sign in with your master password. For first setup or keyring/local-key-file modes, use the
one-time link printed in the terminal; `--open` opens that link automatically.
Web resources are embedded; no `wwwroot` folder is required beside the executable.

Connections and settings are shared with TUI. Features include database and collection browsing,
JavaScript queries, Table/Tree/JSON results, document editing, indexes, history, application logs,
JSON import/export and AI assistance. [Details](docs/web-interface.md).

Browse mode reads 50 documents per page through the driver; Script mode uses installed `mongosh`.
The explorer also supports MongoDB user management, and Web history can open a query in a new tab.
Web transfers are capped at 32 MiB and exports at 10,000 documents. The server is intended for
local access; keep it on loopback without a reverse proxy or port forwarding.

The query timeout is shared by TUI and web. Configure it under **Settings → Queries**
(default: 30 seconds; `0` disables the timeout). Changes apply to subsequent queries.

## Getting started

1. Launch MongoTui and choose how to protect saved secrets: a master password (recommended), the OS keyring on macOS/Windows, or a local key file. You can change the mode later in **Settings → Security…**.
2. Open **Connection → Manage connections…**, add a MongoDB URI, save the profile and connect.
   **From URI** fills the authentication fields from a full connection string.
   **Copy URI** copies the selected profile's full connection string, including credentials, to the clipboard.
3. Expand a database and open a collection with **Enter** or a double-click. A query tab opens
   with a ready-to-run `find` query.
4. Edit the query and press **F5** or **Ctrl+R**. Choose Table, Tree or JSON in
   **Settings → Result view**.

Queries use the tab's database. For example:

```javascript
db.getCollection('users').find({ active: true }).limit(50).toArray()
```

Right-click a tree node or result to see its available actions. **New query** on a database
opens an empty tab bound to that database; on a collection it opens the same tab as a double-click.

### Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Connection manager |
| `Ctrl+T` / `Ctrl+W` | New / close query tab |
| `F5` / `Ctrl+R` | Run query |
| `Ctrl+Space` | Autocomplete in the editor; context menu in the tree |
| `Ctrl+H` | Query history |
| `F2` / `F3` | Export / import |
| `F9` | Refresh |
| `Tab` / `Shift+Tab` | Move between the tree, editor and results |
| `F10` | Focus the menu bar |
| `Ctrl+C` | Copy the selected text (Ctrl+C on macOS; the terminal reserves Cmd+C) |
| `Ctrl+V` / `Cmd+V` | Paste from the clipboard |
| `Ctrl+Q` | Exit |

For autocomplete, use **↑/↓** to choose, **Enter** or **Tab** to insert, and **Esc** to dismiss.
Database, collection and field names are available through `Ctrl+Space`; method suggestions
also appear while typing.

The full list is always available in the app under **Help → Keyboard shortcuts**. Shortcuts act on
the focused pane, so the same `Ctrl+C` copies from the editor, the results or a dialog field.

**Copy on macOS.** Terminals intercept `Cmd+C` for their own copy and never forward it to the
application, so MongoTui copies with `Ctrl+C` (into the system clipboard via `pbcopy`). To reuse
`Cmd+C`, remap it in the terminal to send `Ctrl+C` (`0x03`) — in Terminal.app via
*Settings → Profiles → Keyboard* and in iTerm2 via *Settings → Keys*. On Linux and over SSH
without `xclip`, copy falls back to the OSC 52 escape sequence, which modern terminals support.

## Optional setup

**SSH/TPAM.** Open **SSH SOCKS…** in the connection form and enter the SSH host, port and login.   
Provide a key file or use your ssh-agent and SSH config. Authentication prompts, including 2FA,
appear inside MongoTui. Import and export are unavailable over SOCKS connections.

**AI assistant.** Set the base URL, model and API key in **Settings → AI settings…**.   
Then use **Query → AI assistant…** or **Translate…**. Responses are shown in a dialog.
The assistant receives the active database, the list of collections and the sampled field
names with their BSON types, so it can draft queries against the real schema.

## Current limitations

- Editing and deletion require a simple `find`/`findOne` result without a projection.
  Web Browse results are also editable. The document's `_id` cannot be changed.
  Opening an existing document reloads its current stored value through the driver.
- Saving replaces the whole document and can overwrite changes made after opening the editor.
  The driver also normalizes `Double(-0.0)` to `+0.0` on read; saving persists this change.
  See [BSON editing limits](docs/bson-json.md). Cancelling or timing out a write does not roll it back.
- TUI Tree mode displays formatted documents without expandable nodes; Web Tree nodes can expand.
- Use JSON for transfers: TUI CSV options and BSON support in the dialogs are incomplete.
  Database dump/restore is not available in the interface.
- TUI query history supports viewing only; Web history can reopen queries without running them.

## Local data

Connections, settings and history are stored in `~/.mongotui/mongotui.db`
(`%USERPROFILE%\.mongotui\mongotui.db` on Windows). `MONGOTUI_DATA_DIR` overrides the data directory.
Logs are in its `logs` subdirectory. Shell query text is stored in history; it is excluded from logs.

Connection passwords and the AI API key are encrypted with a key protected by the selected security mode. In **No master password** mode the key is stored in `~/.mongotui/kek.key` with user-only permissions; anyone who can read that file can decrypt the secrets.

Choose the logging level in Settings → Logging in TUI or Web. The preference is stored in the local database and applied without restarting; see [logging configuration](docs/logging.md).

## Development

[Build, run and test](docs/requirements.md#build-from-source) ·
[Publish a build](docs/requirements.md#publish-a-build) ·
[Project conventions](AGENTS.md) · [Release preparation](docs/releasing.md) · [Changelog](CHANGELOG.md).

## Authors

Klabukov Erik (AMEST) · Co-author: Medvedev Denis (medved91).

## License

[MIT](LICENSE). Dependency notices: [Third-party licenses](THIRD-PARTY-LICENSES.md).
