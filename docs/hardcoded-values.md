# Named values and remaining literals

The application uses names according to the owner and meaning of each value.

| Values | Owner |
| --- | --- |
| Shared log scope fields | Application `LogPropertyNames` |
| User-management log action identifiers | Private constants in `WebUserManagement` |
| Index operation state | Private `CollectionOperation` enum; explicit mapping preserves existing log action strings |
| Result display mode | Application `ResultViewMode`, shared by TUI and Web |
| Main Web page | Private `MainPage` enum |
| `_id`, `_id_`, `admin` | Domain `MongoSystemNames` |
| MongoDB built-in roles | Domain `MongoRoleNames` |
| Driver command and response names | Infrastructure `MongoWireNames` |
| SQLite preference keys | Existing constants in their owning settings/security services |
| Shared SSH askpass environment keys | Infrastructure `SshAskPassEnvironment` |
| Application data directory variable | Infrastructure `AppPaths.DataDirectoryEnvironmentVariable` |
| Browser interop methods and editor module | Web `JavaScriptCalls` |
| Icon names, browser themes, language codes | `IconNames`, `ThemeNames`, `UiLanguages` |
| Interactive Web command deadlines | `WebOperationTimeouts` |
| Other sampling, display and resource limits | Constants local to their owning component/service |
| UI wording | `.resx` resources referenced through `LocalizationKeys` |

Existing stored preference keys, role names, MongoDB command fields, environment
variables, JavaScript exports and log metadata retain their previous string values.
The TUI result mode names (`Table`, `Tree`, `Json`, `Raw`) remain compatible with
existing saved preferences. The Web uses the same enum internally.

The following literals deliberately remain close to their use:

- Log message templates and English exception messages: these describe individual
  events/errors. Templates stay literal and retain structured placeholders.
- Explicit serialization mappings such as enum-to-log-action names: these are the
  boundary at which an internal type becomes an existing external identifier.
- SQL table/column declarations, JSON property attributes and single-use CLI flags:
  these directly describe the external schema or grammar owned by that adapter.
- JavaScript export declarations: the C# interop catalog references these exact
  browser-side names; browser checks exercise that boundary.
- MongoDB response text fragments used as compatibility fallbacks, query examples,
  Extended JSON syntax, regex tokens and source-code completion catalogs: these
  are literal protocol/grammar definitions or example data, not UI state.
- HTML/CSS classes, accessibility attribute values, keyboard names, SVG paths,
  layout sizes, formatting characters, units and brand/native language names:
  these belong to their rendering or platform contract.
- Test expectations for stored keys, log fields, action identifiers, Mongo commands
  and BSON data: these remain independent of production constants so that an
  accidental external-format change is detected.

No database migration or new operation-logging abstraction is required. Log files
continue to be read in full; the existing viewer behavior is unchanged.
