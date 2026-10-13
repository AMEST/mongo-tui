# Changelog

All notable changes to this project are documented in this file.

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

[v0.1.0 to v0.2.0]: https://github.com/AMEST/mongo-tui/compare/v0.1.0...v0.2.0
