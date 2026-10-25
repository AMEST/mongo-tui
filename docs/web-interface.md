# Local web interface

`mongo-tui web [--port 8765] [--open]` starts Kestrel on IPv4 loopback.
`mongo-tui` retains the terminal interface. `web --help` prints usage and exits.
Run from source with `dotnet run --project src/MongoTui.TUI -- web --open`.

## Architecture

`MongoTui.Web` is a Razor component library using Blazor Interactive Server. The existing
executable hosts it and composes the same Application and Infrastructure services as the TUI.
MongoDB and SSH operations run in .NET. No Node server, browser .NET runtime or CDN is required.
The visual reference is [oMLX](https://github.com/jundot/omlx); styling and components are original.

Each browser circuit owns its connections, SSH challenges, query sessions, tabs and cancellations.
Closing a tab cancels its request; disconnected circuits are retained for 30 seconds before cleanup.
Reloading the page starts a fresh workspace. Saved profiles, query history and settings persist in
the existing SQLite store. Optional `MONGOTUI_DATA_DIR` selects a separate directory for testing.

The host rejects foreign Host/Origin headers and cross-site requests, applies antiforgery protection
and denies framing. It deliberately has no external bind option, remote login or multi-user model.
The existing master password/keyring/local-key-file choice protects stored secrets; it is not a
remote web authentication system. Do not expose this server through a proxy or port forward.

## Working with data

- The explorer keeps all saved connections visible, with active connections first and names sorted
  within each group. Connections start collapsed; expanding or collapsing a node does not disconnect
  it or close query tabs. Refresh preserves expanded nodes for the current session.
- Connection actions are available from the three-dot button or a right-click on the connection.
  Create a database by entering its name and first collection; the empty collection creates a real
  database on the server. The TUI offers the same actions from its context menu and toggles nodes
  with Enter. Disconnecting keeps the saved profile in both explorers.
- Browse mode uses the driver, fetching 50 documents and one look-ahead document per page.
  Results have no size cap.
- Script mode uses installed `mongosh` and supports cancellation.
  Shell stdout/stderr are collected in memory without a size cap.
  Cancelling a write does not undo changes already accepted by MongoDB.
- **Settings → Queries** configures the query timeout shared with the TUI (default: 30 seconds).
  Set it to `0` to disable the timeout. The value is stored as `query.timeoutSeconds` in SQLite
  and is read for each new query. Running queries retain the value they started with.
  Shell scripts use a cancellation deadline which stops `mongosh`; driver browse queries use
  the same deadline plus MongoDB's server-side `maxTimeMS`. The TUI exposes the same setting
  under **Settings → Queries…**. User cancellation is reported separately from timeout.
- A circuit supports 12 query tabs; at most two queries run concurrently across the web host.
- Canonical Extended JSON preserves BSON types, including Int64 and Decimal128. Table view shows
  up to 20 fields; use Tree/JSON or open a document for the rest. Results render 50 documents at a time.
- Editing is enabled for browse results and recognized complete `find`/`findOne` results.
  Projections and aggregation results are read-only. Opening an editor reloads the document
  by `_id` through the driver and verifies that canonical JSON preserves its BSON representation.
  Replacing a document cannot change `_id` or its BSON type. See [BSON editing guarantees and tests](bson-json.md)
  for ambiguous JSON wrappers, shell numeric types and driver limitations.
- Collection tools expose statistics and index creation/deletion. New collections can be created
  from the database tree. Other administrative operations are available through mongosh scripts.
- **Manage users** in a connection's menu opens users of `admin`; the users button beside a
  database opens that authentication database. Users may hold roles on other databases.
  Create, edit, delete and refresh users; configure `read`, `readWrite`, `dbOwner` and cluster
  roles. Administrator (`root`) is exclusive among managed permissions. Additional/custom roles
  are displayed and preserved on edit. An empty password on edit leaves the password unchanged.
- Creating a user or changing a password shows the new password and connection string once,
  with separate copy buttons. These credentials are never saved in profiles, history or logs.
  The URI uses the saved profile and the user's authentication database, excluding tunnel proxy
  options. SSH connections show a warning that the tunnel must be configured separately.
  Deleting the connection's current MongoDB user is blocked; changing its password shows a
  reconnect notice. Deletes require confirmation. Closing a dialog cancels pending requests;
  commands have a 30-second deadline and duplicate form submissions are blocked.
- Import accepts JSON Lines or a JSON array. Export writes canonical JSON Lines. Transfers use
  private temporary files, a 32 MiB cap and at most 10,000 exported documents. They require
  `mongoimport`/`mongoexport`; MongoDB tools do not support the application's SSH SOCKS transfer path.
- AI is optional and requires an explicitly configured compatible endpoint. The assistant sends
  the prompt and schema field names/types; generated queries open for review and never auto-run.
- **History** shows the latest 100 shell executions for the selected connection, including their
  script, namespace, timestamp, success status and duration. Driver browse queries are not recorded.
- **Logs** displays the complete contents of retained application log files. Select a file or follow
  the latest one, refresh manually or enable polling every two seconds. Logs are shared with the TUI;
  seven files are retained. **Settings → Logging** stores the level in the local SQLite database
  and applies it immediately. Other processes sharing the data directory refresh it every three seconds.
  The default is `Information`; an environment override is shown separately in settings.
  Services write directly through standard `ILogger<T>` loggers. Query completion records include
  namespace, duration and result count; standard scopes carry connection and tab IDs.
  Failures include safe exception types and stack frames; cancellation and timeout are distinct.
  Connection strings, credentials, query/filter text and document contents are excluded.
  Shell script text remains in **History**. See [logging configuration](logging.md).

## Build and distribution

Normal .NET builds use the checked-in CodeMirror bundle. To change the editor:

```sh
cd src/MongoTui.Web/ClientAssets
npm ci
npm run build
```

Publish the existing executable project with the repository release options. Keep the whole output
folder, including `wwwroot` and the static asset endpoint manifest, next to the executable.
The host opts into `RequiresAspNetWebAssets` because its Razor components live in a library;
see [Microsoft's project structure guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/project-structure?view=aspnetcore-10.0).

`dotnet test tests/MongoTui.Web.Tests` checks origin restrictions, command-line parsing,
BSON round trips, retained connection secrets, cancellation, pagination, editability and SSH prompt isolation.
User-management tests additionally cover authentication scope, preserved custom roles and passwords,
one-time credentials, English/Russian errors, duplicate submissions and cancellation on dialog disposal.
The full solution's integration tests additionally require a running Docker daemon.

## UI stability checks

Refresh keeps the displayed data until a new response is ready. Pending actions keep their labels,
contrast and icon slots; progress indicators appear after 150 ms. Database nodes keep their identity,
and reopening a database displays cached collections while refreshing them. Log and history selectors
cancel superseded reads so a late response cannot replace the current selection.

Dialogs reserve space for feedback and keep focus inside the topmost window. Closing a dialog restores
focus to its trigger. User lists, collection details and the AI assistant keep a stable outer size and
scroll their contents when needed. Result actions wrap on narrow screens.

For browser regression checks, start with a separate `MONGOTUI_DATA_DIR` and a disposable MongoDB.
Exercise each scenario at desktop and narrow widths, in English/Russian and light/dark themes:

| Area | Scenarios |
| --- | --- |
| Security | Setup/unlock errors, password change, successful save |
| Connections | Add/edit/test, SSH fields and nested challenge, connect/disconnect, confirmed removal |
| Explorer | First expansion, cached reopening, collapse, refresh, create database/collection |
| Queries | Browse/filter, pagination, script run/cancel, scalar results, Table/Tree/JSON |
| Documents | Invalid JSON, insert/update, copy failure, confirmed delete |
| Collection details | Initial statistics, invalid index JSON, create/drop index |
| Users | Empty/populated refresh, permission errors, create/edit/delete, credentials copy |
| Transfer / AI | Validation, success, cancellation, retained result and review before use |
| History / Logs | Change selection during loading, polling with a dropdown open, retained scroll |
| Settings / Recovery | Save feedback, language/theme changes, connection recovery states |

Include both fast responses and responses delayed by 500–1000 ms. During refresh, compare bounding
rectangles before, during and after the request; unchanged content should move by no more than 1 px.
Also check that existing tree rows and selectors remain connected to the DOM, the initiating button
keeps focus, and scroll positions stay unchanged. Inject delays/errors only into the disposable server.
