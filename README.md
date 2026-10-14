# MongoDB TUI Viewer

**English** | [Русский](README_RU.md)

A cross-platform terminal client for MongoDB:
connection manager, server/database/collection tree, query tabs, document viewing and
editing, import/export, autocomplete, and an AI assistant built on an OpenAI-compatible API.

## Screenshots

| Main window | Connection manager |
|---|---|
| [![Main window](docs/work.jpg)](docs/work.jpg) | [![Connection manager](docs/connection-manager.jpg)](docs/connection-manager.jpg) |

| Insert document | Master password |
|---|---|
| [![Insert document](docs/insert-document.jpg)](docs/insert-document.jpg) | [![Master password](docs/master-password.jpg)](docs/master-password.jpg) |
## Features

- **Connection manager** — SQLite-backed profiles; secrets encrypted with AES-256-GCM.
- **Master password** — unlocks the secrets; the key is derived with Argon2id.
- **Query tabs** — query scripts run through `mongosh --eval` as Extended JSON, run with `F5` or `Ctrl+Enter`.
- **Result pane** — table, tree and JSON views with elapsed time and error reporting.
- **Documents** — insert / edit / delete as JSON.
- **Indexes & stats** — list, create and drop indexes; collection document/index counts.
- **Export / Import** — `mongoexport`/`mongoimport` (json, canonical json, csv) and `mongodump`/`mongorestore`.
- **Autocomplete** — databases, collections and fields (with a field cache).
- **AI assistant** — ask for a query from a schema; translate foreign queries (e.g. SQL) to MongoDB.
- **Query history**, **dark theme**, **localization** (English/Russian).

## Requirements

Install the following components on the machine where **`mongo-tui` runs**. Normal use
requires an interactive terminal, **MongoDB Shell (`mongosh`)** for query tabs, and access
to a MongoDB server (local or remote).

| Dependency | When required |
|---|---|
| [MongoDB Shell (`mongosh`)](https://www.mongodb.com/docs/mongodb-shell/install/) | Running scripts in query tabs. The connection tree and document operations use the bundled MongoDB driver. |
| **.NET 10 runtime** | Running a framework-dependent build. Prebuilt release archives and self-contained builds include the runtime. |
| [MongoDB Database Tools](https://www.mongodb.com/docs/database-tools/installation/) | `mongoexport` / `mongoimport` for JSON/CSV export/import; `mongodump` / `mongorestore` for database backups/restores. |
| [OpenSSH client 8.4+ (`ssh`)](https://www.openssh.org/txt/release-8.4) | Automatic SSH/TPAM SOCKS5 proxy startup and interactive 2FA through `SSH_ASKPASS_REQUIRE=force`. |

`mongosh` is installed separately from **MongoDB Database Tools**. External executables must
be available on the `PATH` of the `mongo-tui` process; `MongoToolsOptions.Directory` can
override the lookup directory for MongoDB tools. For SSH key authentication, provide a key
file or have the required key loaded in an accessible ssh-agent.

On **Linux**, self-contained builds still need the .NET system dependencies: glibc,
libgcc, libstdc++, ICU, OpenSSL, zlib, GSSAPI/Kerberos, CA certificates and timezone data;
some distribution versions also require Brotli. Package names and versions depend on the OS:
see Microsoft's requirements for [Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install#dependencies)
and [Debian](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies).
Extract the whole release archive and preserve its file layout: the native SQLite and
Oniguruma libraries are shipped with the app.

The **.NET 10 SDK** is required to build from source, **Docker** to run integration tests,
and **Node.js/npm** when installing `mongosh` through npm.

Check the components needed for your workflow:

```sh
mongosh --version

# Export/import and database backups/restores
mongoexport --version
mongoimport --version
mongodump --version
mongorestore --version

# SSH/TPAM proxy
ssh -V

# Framework-dependent builds: Microsoft.NETCore.App 10.0.x must be listed
dotnet --list-runtimes
```

## Installation

### Prebuilt binaries (no .NET runtime required)

Download the archive for your OS and architecture from [Releases](https://github.com/AMEST/mongo-tui/releases), extract it and
run the `mongo-tui` binary. Available archives: `mongotui-linux-x64`, `mongotui-linux-arm64`,
`mongotui-osx-x64`, `mongotui-osx-arm64`, `mongotui-windows-x64`.

**macOS** — the binaries are not code-signed or notarized, so Gatekeeper blocks them on first
launch. Clear the quarantine attribute before running:

```bash
xattr -d com.apple.quarantine ./mongo-tui
xattr -d com.apple.quarantine ./libe_sqlite3.dylib
xattr -d com.apple.quarantine ./libonigwrap.dylib
chmod +x ./mongo-tui
./mongo-tui
```

**Linux:**

```bash
chmod +x ./mongo-tui
./mongo-tui
```

**Windows:** run `mongo-tui.exe`.

### From source (requires the .NET 10 SDK)

```bash
git clone https://github.com/AMEST/mongo-tui.git
cd mongo-tui
dotnet run --project src/MongoTui.TUI
```

See [Building and testing](#building-and-testing) for the full build and test workflow, and
[Self-contained build](#self-contained-build) to produce your own single-file binary.

### MongoDB tools

Install **MongoDB Shell (`mongosh`)** for query tabs and **MongoDB Database Tools** for
import/export and dump/restore. These are separate packages and their executables must be
available on your `PATH` (see [Requirements](#requirements)).

**macOS (Homebrew):**

```bash
brew install mongodb/brew/mongosh
```

Install the full tool set with `brew install mongodb/brew/mongodb-database-tools`.

**Linux (Debian / Ubuntu, with the official MongoDB package repository configured):**

```bash
sudo apt-get install -y mongodb-mongosh mongodb-database-tools
```

**Linux (RHEL / Fedora, with the appropriate MongoDB package repository configured):**

```bash
sudo dnf install -y mongodb-mongosh mongodb-database-tools
```

**Any platform with Node.js (npm):**

```bash
npm install -g mongosh
```

Download standalone archives from the [MongoDB Shell downloads](https://www.mongodb.com/try/download/shell)
and [Database Tools downloads](https://www.mongodb.com/try/download/database-tools), then add
their executable directories to `PATH`. The app invokes `mongosh` directly, so an installation
accessible only through `npx mongosh` does not satisfy the shell dependency.

## Usage

1. Launch the app. If secrets are locked, the master-password dialog opens first; then the
   connection manager opens.
2. **Connection → Manage connections…** — add or edit a connection. Paste a full connection
   string (with credentials) and press **From URI** to auto-fill the fields; credentials are
   moved into the username/password fields and the auth database/mechanism are parsed.
   Select a connection and press **Connect** — it appears in the **active-connections tree**
   on the left, expanded to the list of its databases (empty ones are shown with an `(empty)`
   marker).
3. **Double-click** in the tree (or press **Enter**): on a connection to collapse/expand it,
   on a database to load and show its collections, on a collection to open a query tab with a
   ready `find` script.
4. Write a JavaScript query (e.g. `db.users.find({})`) and press **F5** or **Ctrl+Enter** to run it.
5. Inspect the result in the table/tree/JSON pane; the status bar shows the elapsed time and the
   number of returned documents.
6. **Right-click** a tree node (or press **Ctrl+Space**) to open its context menu: connection —
   refresh/disconnect; database — refresh/statistics/drop; collection — statistics/indexes/clear/
   drop (destructive actions require confirmation).

To enable an SSH SOCKS5 proxy for a profile, open **SSH SOCKS…** in the connection form,
enable automatic startup, and enter the SSH host, port and login. For TPAM use port `4422`
and a login such as `requester+user@domain+system`. Leave the key file empty to use ssh-agent
and `~/.ssh/config`; local port `0` selects an available loopback port. Save the SSH settings
and then save the connection profile.

**Connect** and **Test** start `ssh -D` and show SSH authentication prompts, including the 2FA
code after key authentication. Responses are passed through a private pipe and never saved.
OpenSSH 8.4+ must be installed in `PATH` for
[`SSH_ASKPASS_REQUIRE=force`](https://man.openbsd.org/ssh.1#SSH_ASKPASS_REQUIRE).
Host keys are checked using the usual SSH configuration and `known_hosts`.

Both `mongodb://` and `mongodb+srv://` URIs are supported. SRV/TXT DNS records must resolve
on the client; DNS queries are not sent through SOCKS. The effective URI gets
`proxyHost=127.0.0.1` and the actual `proxyPort`, replacing
existing values. Old proxy credentials are removed because OpenSSH SOCKS5 has no proxy
authentication. MongoDB hosts, replica-set and TLS options are preserved, as is the saved URI.
The SSH process stops on disconnect, failed connection/test, or application exit. This mode
supports 2FA via SSH keyboard-interactive authentication; a TPAM prompt inside a remote shell
requires a different flow because `ssh -N` does not open a shell.

Import/export and backup/restore through MongoDB Database Tools are unavailable for
connections with a SOCKS proxy. The tools ignore SOCKS settings, so MongoTui blocks their
launch and displays the reason to prevent a direct connection to MongoDB.

Menu overview:

| Menu | Actions |
|---|---|
| File | Exit (Ctrl+Q) |
| Connection | Manage connections… (Ctrl+O), Refresh (F9), Unlock secrets… (F8), Change master password… (Ctrl+P) |
| Query | New tab (Ctrl+T), Run (F5/Ctrl+Enter), Close tab (Ctrl+W), AI assistant… (F6), Translate… (F7), History… (Ctrl+H) |
| Document | Insert… (Ctrl+I), Edit… (Ctrl+E), Delete… (Ctrl+D) |
| Collection | Indexes… (Ctrl+K), Export… (F2), Import… (F3) |
| Settings | Result view (Table / Tree / JSON), Language (English / Русский), AI settings… |

## Settings and data

All data lives under the user's home directory:

- **Linux / macOS**: `~/.mongotui/`
- **Windows**: `%USERPROFILE%\.mongotui\`

| Path | Purpose |
|---|---|
| `mongotui.db` | SQLite database: connections, settings, query history |
| `logs/app-*.log` | Serilog logs (file only) |

| Setting | Where | Default |
|---|---|---|
| AI base URL | code (`AiOptions.BaseUrl`) | `https://api.openai.com/v1` |
| AI model | code (`AiOptions.Model`) | `gpt-4o-mini` |
| AI API key | settings store, key `ai.api_key` | — |
| MongoDB tools directory | `MongoToolsOptions.Directory` | system `PATH` |
| Master-password salt | settings store, key `security.kek_salt` | generated on first unlock |
| Master-password check | settings store, key `security.kek_check` | written on first unlock |
| Language | OS locale (en default, ru available) | `en` |

AI works with any OpenAI-compatible endpoint (OpenAI, Azure OpenAI, Ollama, OpenRouter, local).

## Building and testing

Requires the **.NET 10 SDK**.

Build and test the whole solution:

```sh
dotnet build MongoTui.sln
dotnet test MongoTui.sln
```

Integration tests require Docker (Testcontainers) and are tagged
`[Trait("Category", "Integration")]`. To exclude them:

```sh
dotnet test MongoTui.sln --filter "Category!=Integration"
```

## Self-contained build

Publish a single-file, self-contained executable (no .NET runtime needed on the target):

```sh
# Linux x64
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true

# Windows x64
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true

# macOS (Apple Silicon)
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true
```

Other runtimes: `osx-x64`, `linux-arm64`. The output goes to
`src/MongoTui.TUI/bin/Release/net10.0/<runtime>/publish/`.

A framework-dependent build (smaller, needs the .NET 10 runtime) is just:

```sh
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release
```

## Architecture

Four-layer Onion architecture:

```
TUI (Terminal.Gui)  →  Application  →  Domain
        ↘ Infrastructure ────────────────────────↗
```

| Project | Purpose |
|---|---|
| `MongoTui.Domain` | Pure core: models, value objects, invariants |
| `MongoTui.Application` | Interfaces (abstractions) and orchestrating services |
| `MongoTui.Infrastructure` | Implementations: Mongo, SQLite, Crypto, AI, Process |
| `MongoTui.TUI` | TUI: views, view models, editor, themes |

The MongoDB access goes through a **Composite Gateway** that routes queries/CRUD to the
official driver and bulk operations (export/import, dump/restore) to the Database Tools CLI.

## Stack

- .NET 10 (LTS), Terminal.Gui 2.5
- MongoDB.Driver + MongoDB Database Tools
- Microsoft.Data.Sqlite + LINQ to DB (linq2db) — SQLite via LINQ, raw SQL only for migrations
- AES-256-GCM + Argon2id
- Serilog (file logging only)
- xUnit + FakeItEasy + AwesomeAssertions, Testcontainers

## Project conventions

- [`AGENTS.md`](AGENTS.md)
