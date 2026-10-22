using System.Collections.ObjectModel;
using System.Drawing;
using MongoDB.Bson;
using Serilog;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Root window of the application shell.</summary>
public sealed partial class MainWindow : Window
{
    private const string EmptyQueryScript = "// write your query below this line";

    private readonly ILocalizer _localizer;
    private readonly ConnectionService _connections;
    private readonly DocumentService _documents;
    private readonly IndexService _indexes;
    private readonly DatabaseService _databases;
    private readonly ExportService _export;
    private readonly ImportService _import;
    private readonly IAiAssistant _assistant;
    private readonly IQueryHistoryRepository _history;
    private readonly CollectionSchemaService _schema;
    private readonly TranslateQueryService _translate;
    private readonly SettingsService _settings;
    private readonly AiSettingsService _aiSettings;
    private readonly SecurityModeService _security;
    private readonly IMongoGateway _gateway;
    private readonly ConnectionTunnelService _tunnels;
    private readonly ListView _treeList = new();
    private readonly TreeViewModel _tree;
    private readonly List<TreeRow> _rows = [];
    private readonly Dictionary<Key, Action> _shortcuts = [];
    private readonly QueryTabsView _queryTabs;
    private readonly Label _status;
    private MenuBar? _menuBar;
    private FrameView? _treeFrame;
    private ResultViewMode _resultViewMode = ResultViewMode.Table;
    private string? _language;
    private bool _aiConfigured;

    public MainWindow(
        ILocalizer localizer,
        ConnectionService connections,
        QueryTabsView queryTabs,
        DocumentService documents,
        IndexService indexes,
        DatabaseService databases,
        ExportService export,
        ImportService import,
        IAiAssistant assistant,
        IQueryHistoryRepository history,
        CollectionSchemaService schema,
        TranslateQueryService translate,
        SettingsService settings,
        AiSettingsService aiSettings,
        SecurityModeService security,
        IMongoGateway gateway,
        ConnectionTunnelService tunnels)
    {
        _localizer = localizer;
        _connections = connections;
        _documents = documents;
        _indexes = indexes;
        _databases = databases;
        _export = export;
        _import = import;
        _assistant = assistant;
        _history = history;
        _schema = schema;
        _translate = translate;
        _settings = settings;
        _aiSettings = aiSettings;
        _security = security;
        _gateway = gateway;
        _tunnels = tunnels;
        _tree = new TreeViewModel(gateway, localizer);
        _queryTabs = queryTabs;
        _queryTabs.RunRequested += (_, _) => Safe(RunActiveQueryAsync);
        _queryTabs.ExportRequested += (_, _) => OpenExport();
        _queryTabs.EditDocumentRequested += (_, _) => EditDocument();
        _queryTabs.DeleteDocumentRequested += (_, _) => DeleteDocument();

        Title = localizer.Get("App.Title");
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var menuBar = BuildMenuBar(localizer);
        _menuBar = menuBar;
        Add(menuBar);

        var treeFrame = new FrameView
        {
            Title = localizer.Get("Tree.Active"),
            X = 0,
            Y = 1,
            Width = Dim.Percent(30),
            Height = Dim.Fill(),
        };
        _treeFrame = treeFrame;

        _treeList.X = 0;
        _treeList.Y = 0;
        _treeList.Width = Dim.Fill();
        _treeList.Height = Dim.Fill();
        _treeList.Accepted += (_, _) => Safe(OpenTreeItemAsync);
        _treeList.MouseEvent += OnTreeMouseEvent;
        treeFrame.Add(_treeList);
        Add(treeFrame);

        _queryTabs.X = Pos.Right(treeFrame);
        _queryTabs.Y = 1;
        _queryTabs.Width = Dim.Fill();
        _queryTabs.Height = Dim.Fill();
        Add(_queryTabs);

        _status = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Text = localizer.Get("Status.Hint"),
        };
        Add(_status);

        RebuildTree();
        SetStatus();
    }

    private IApplication? _application;

    /// <summary>The running Terminal.Gui application, injected by the host.</summary>
    public IApplication? Application
    {
        get => _application;
        set
        {
            if (ReferenceEquals(_application, value))
            {
                return;
            }

            if (_application is not null)
            {
                _application.Keyboard.KeyDown -= OnApplicationKeyDown;
            }

            _application = value;

            if (_application is not null)
            {
                _application.Keyboard.KeyDown += OnApplicationKeyDown;
            }
        }
    }

    // Application.Keyboard.KeyDown fires before the focused view handles the key, so it
    // wins over editor bindings (e.g. Ctrl+W deleting a word in the query editor).
    private void OnApplicationKeyDown(object? sender, Key key)
    {
        if (Application?.TopRunnableView != this)
        {
            return;
        }

        if (HandleCompletionKey(key)) return;

        if (key == new Key(KeyCode.Tab | KeyCode.CtrlMask))
        {
            key.Handled = true;
            InsertTabInActiveEditor();
            return;
        }

        if (key == new Key(KeyCode.Tab))
        {
            key.Handled = CyclePaneFocus(forward: true);
            return;
        }

        if (key == new Key(KeyCode.Tab | KeyCode.ShiftMask))
        {
            key.Handled = CyclePaneFocus(forward: false);
            return;
        }

        if (_shortcuts.TryGetValue(key, out var action))
        {
            Log.Debug("Shortcut {Key} invoked", key);
            key.Handled = true;
            action();
        }
    }

    private bool CyclePaneFocus(bool forward)
    {
        if (Application?.Popovers?.GetActivePopover() is not null)
        {
            return false;
        }

        var panes = new List<View> { _treeList };

        if (_queryTabs.ActiveEditorView is { } editor)
        {
            panes.Add(editor);
        }

        if (_queryTabs.ActiveResultView is { } result)
        {
            panes.Add(result);
        }

        if (PaneFocusCycle.Resolve(MostFocused, panes, _queryTabs, forward) is not { } target)
        {
            return false;
        }

        target.SetFocus();
        return true;
    }

    private void InsertTabInActiveEditor()
    {
        if (_queryTabs.ActiveEditorView is { HasFocus: true } editor)
        {
            editor.InsertText("\t");
        }
    }

    /// <summary>Loads the initial state into the shell.</summary>
    public void Compose()
    {
    }

    /// <summary>
    /// Startup flow: choose or verify the security mode, unlock secrets (automatically for
    /// keyring/key-file modes, otherwise via the master password dialog), then open the
    /// connection manager.
    /// </summary>
    public async Task OpenStartupAsync()
    {
        try
        {
            await LoadSettingsAsync();

            if (!await EnsureSecurityReadyAsync())
            {
                Application?.RequestStop();
                return;
            }

            await RefreshAiAvailabilityAsync();
            await ShowManagerAsync();
        }
        catch (Exception exception)
        {
            Log.Error("Startup failed ({ExceptionType})", exception.GetType().Name);
            SetStatus(exception.Message);
        }
    }

    private void Safe(Func<Task> action) => _ = RunSafeAsync(action);

    private async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Log.Error("Command failed ({ExceptionType})", exception.GetType().Name);
            SetStatus(exception.Message);
        }
    }

    /// <summary>Opens the connection manager and connects to the chosen profile.</summary>
    public async Task ShowManagerAsync()
    {
        if (Application is null)
        {
            return;
        }

        var dialog = new ConnectionManagerDialog(_connections, _localizer, Application);
        await dialog.LoadAsync();
        Application.Run(dialog);

        if (dialog.ConnectedProfile is not null && dialog.ConnectedTarget is not null)
        {
            await ConnectAsync(dialog.ConnectedProfile, dialog.ConnectedTarget);
        }
    }

    private void OpenAbout()
    {
        if (Application is null)
        {
            return;
        }

        using var dialog = new AboutDialog(_localizer, Application);
        Application.Run(dialog);
    }

    private void OpenShortcuts()
    {
        if (Application is null)
        {
            return;
        }

        using var dialog = new ShortcutsDialog(_localizer, Application);
        Application.Run(dialog);
    }

    private MenuBar BuildMenuBar(ILocalizer localizer) =>
        new(BuildMenuItems(localizer))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
        };

    private MenuBarItem[] BuildMenuItems(ILocalizer localizer)
    {
        static Key Ctrl(KeyCode code) => new(code | KeyCode.CtrlMask);

        _shortcuts.Clear();

        // MenuBar menu items only receive their shortcut while their popover is open
        // (Terminal.Gui bails out of Command.HotKey for non-visible items). Register the
        // action here as an application-level shortcut handled by KeyDownNotHandled instead.
        MenuItem Item(string text, Key key, Action action, bool enabled = true)
        {
            if (enabled && key != Key.Empty)
            {
                _shortcuts[key] = action;
            }

            return new MenuItem(text, string.Empty, action, key) { Enabled = enabled };
        }

        static MenuItem Choice(string text, bool selected, Action action) =>
            new((selected ? "\u2022 " : "   ") + text, string.Empty, action);

        var runQuery = () => Safe(RunActiveQueryAsync);
        _shortcuts[Ctrl(KeyCode.R)] = runQuery;

        return
        [
            new MenuBarItem(localizer.Get("Menu.File"),
            [
                Item(localizer.Get("Menu.File.Exit"), Ctrl(KeyCode.Q), () => Application?.RequestStop()),
            ]),
            new MenuBarItem(localizer.Get("Menu.Connection"),
            [
                Item(localizer.Get("Menu.Connection.Manage"), Ctrl(KeyCode.O), () => Safe(ShowManagerAsync)),
                Item(localizer.Get("Menu.Connection.Disconnect"), Key.Empty, () => Safe(DisconnectActiveAsync)),
                Item(localizer.Get("Menu.Connection.Refresh"), Key.F9, () => Safe(RefreshActiveAsync)),
                Item(localizer.Get("Menu.Connection.Unlock"), Key.F8, UnlockSecrets,
                    _security.CurrentMode == SecurityMode.Password),
                Item(localizer.Get("Menu.Connection.ChangePassword"), Ctrl(KeyCode.P), OpenChangePassword,
                    _security.CurrentMode == SecurityMode.Password),
            ]),
            new MenuBarItem(localizer.Get("Menu.Query"),
            [
                Item(localizer.Get("Menu.Query.New"), Ctrl(KeyCode.T), NewTab),
                Item(localizer.Get("Menu.Query.Run"), Key.F5, runQuery),
                Item(localizer.Get("Menu.Query.Autocomplete"), Key.Empty, () => Safe(CompleteActiveQueryAsync)),
                Item(localizer.Get("Menu.Query.Close"), Ctrl(KeyCode.W), () => Safe(_queryTabs.CloseActiveAsync)),
                Item(localizer.Get("Menu.Query.Ai"), Key.F6, () => Safe(OpenAiAssistantAsync), _aiConfigured),
                Item(localizer.Get("Menu.Query.Translate"), Key.F7, OpenTranslate, _aiConfigured),
                Item(localizer.Get("Menu.Query.History"), Ctrl(KeyCode.H), () => Safe(OpenHistoryAsync)),
            ]),
            new MenuBarItem(localizer.Get("Menu.Document"),
            [
                Item(localizer.Get("Menu.Document.Insert"), Ctrl(KeyCode.I), InsertDocument),
                Item(localizer.Get("Menu.Document.Edit"), Ctrl(KeyCode.E), EditDocument),
                Item(localizer.Get("Menu.Document.Delete"), Ctrl(KeyCode.D), EditDocument),
            ]),
            new MenuBarItem(localizer.Get("Menu.Collection"),
            [
                Item(localizer.Get("Menu.Collection.Indexes"), Ctrl(KeyCode.K), () => Safe(OpenIndexesAsync)),
                Item(localizer.Get("Menu.Collection.Export"), Key.F2, OpenExport),
                Item(localizer.Get("Menu.Collection.Import"), Key.F3, OpenImport),
            ]),
            new MenuBarItem(localizer.Get("Menu.Settings"),
            [
                new MenuItem
                {
                    Title = localizer.Get("Menu.Settings.Display"),
                    SubMenu = new Menu(
                    [
                        Choice(localizer.Get("Result.Table"), _resultViewMode == ResultViewMode.Table, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Table))),
                        Choice(localizer.Get("Result.Tree"), _resultViewMode == ResultViewMode.Tree, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Tree))),
                        Choice(localizer.Get("Result.Json"), _resultViewMode == ResultViewMode.Json, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Json))),
                        Choice(localizer.Get("Result.Raw"), _resultViewMode == ResultViewMode.Raw, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Raw))),
                    ]),
                },
                new MenuItem
                {
                    Title = localizer.Get("Menu.Settings.Language"),
                    SubMenu = new Menu(
                    [
                        Choice("English", CurrentLanguage == "en", () => Safe(() => SetLanguageAsync("en"))),
                        Choice("Русский", CurrentLanguage == "ru", () => Safe(() => SetLanguageAsync("ru"))),
                    ]),
                },
                Item(localizer.Get("Menu.Settings.Ai"), Key.Empty, () => Safe(OpenAiSettingsAsync)),
                Item(localizer.Get("Menu.Settings.Security"), Key.Empty, OpenSecuritySettings),
            ]),
            new MenuBarItem(localizer.Get("Menu.Help"),
            [
                Item(localizer.Get("Menu.Help.Shortcuts"), Key.Empty, OpenShortcuts),
                Item(localizer.Get("Menu.Help.About"), Key.Empty, OpenAbout),
            ]),
        ];
    }

    private string CurrentLanguage => _language ?? _localizer.CurrentLanguage;

    private async Task RefreshActiveAsync()
    {
        _queryTabs.InvalidateCompletions();
        if (_tree.Count == 0)
        {
            return;
        }

        foreach (var connection in _tree.Connections.ToList())
        {
            try
            {
                var databases = await _gateway.ListDatabasesAsync(connection.Target, CancellationToken.None);
                _tree.UpdateDatabases(
                    connection.Profile.Id,
                    databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase).ToList());
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message);
            }
        }

        RebuildTree();
    }

    private async Task DisconnectActiveAsync()
    {
        var connection = SelectedRow()?.Owner ?? _tree.Connections.FirstOrDefault();

        if (connection is null)
        {
            SetStatus(_localizer.Get("Tree.Empty"));
            return;
        }

        await DisconnectAsync(connection);
    }

    private async Task RunActiveQueryAsync()
    {
        var summary = await _queryTabs.RunActiveAsync();

        if (summary is { } result)
        {
            SetStatus(_localizer.Format("Status.QueryResult", result.DocumentCount, result.Elapsed.TotalMilliseconds));
        }
    }

    private void RebuildTree(int selectIndex = -1)
    {
        _rows.Clear();
        var lines = new List<string>();

        if (_tree.Count == 0)
        {
            lines.Add(_localizer.Get("Tree.Empty"));
        }
        else
        {
            foreach (var row in _tree.BuildRows())
            {
                _rows.Add(row);
                lines.Add(row.Text);
            }
        }

        _treeList.SetSource(new ObservableCollection<string>(lines));

        if (selectIndex >= 0 && selectIndex < _rows.Count)
        {
            _treeList.SelectedItem = selectIndex;
        }
    }

    private async Task OpenTreeItemAsync()
    {
        if (SelectedRow() is not { } row)
        {
            return;
        }

        var id = row.Owner.Profile.Id;
        var index = _treeList.SelectedItem ?? -1;

        switch (row.Kind)
        {
            case TreeRowKind.Connection:
                _tree.ToggleConnection(id);
                RebuildTree(index);
                break;
            case TreeRowKind.Database:
                await _tree.ToggleDatabaseAsync(id, row.Owner.Target, row.Database!, CancellationToken.None);
                RebuildTree(index);
                break;
            case TreeRowKind.Collection:
                OpenCollectionTab(row);
                break;
        }
    }

    private void OpenCollectionTab(TreeRow row)
    {
        var script = BuildCollectionScript(row.Collection!);
        _queryTabs.AddTab(row.Owner.Profile.Id, row.Owner.Target, row.Database, row.Collection, script);
        SetStatus($"{row.Database}.{row.Collection}");
    }

    private void OpenDatabaseQueryTab(TreeRow row)
    {
        _queryTabs.AddTab(row.Owner.Profile.Id, row.Owner.Target, row.Database, collection: null, EmptyQueryScript);
        SetStatus(row.Database!);
    }

    private static string BuildCollectionScript(string collection) =>
        $"db.getCollection(\"{collection}\").find({{}}).limit(50).toArray()";

    private void OnTreeMouseEvent(object? sender, Mouse mouse)
    {
        if (!mouse.Flags.HasFlag(MouseFlags.RightButtonClicked))
        {
            return;
        }

        if (mouse.Position is { } position && _rows.Count > 0)
        {
            _treeList.SelectedItem = Math.Clamp(position.Y, 0, _rows.Count - 1);
        }

        mouse.Handled = true;
        OpenTreeContextMenu(mouse.ScreenPosition);
    }

    private void OpenTreeContextMenu(Point position)
    {
        if (Application is null || SelectedRow() is not { } row)
        {
            return;
        }

        var actions = BuildContextActions(row);

        if (actions.Count == 0)
        {
            return;
        }

        var clamped = new Point(
            Math.Clamp(position.X, 0, Math.Max(0, Application.Screen.Width - 38)),
            Math.Clamp(position.Y, 0, Math.Max(0, Application.Screen.Height - (actions.Count + 4))));

        var dialog = new ContextMenuDialog(_localizer, Application, actions.Select(action => action.Title).ToList(), clamped);
        Application.Run(dialog);

        if (dialog.SelectedIndex >= 0 && dialog.SelectedIndex < actions.Count)
        {
            Safe(actions[dialog.SelectedIndex].Handler);
        }
    }

    private List<(string Title, Func<Task> Handler)> BuildContextActions(TreeRow row)
    {
        var connection = row.Owner;
        var actions = new List<(string Title, Func<Task> Handler)>();

        switch (row.Kind)
        {
            case TreeRowKind.Connection:
                actions.Add((_localizer.Get("Tree.CreateDatabase"), () => CreateDatabaseAsync(connection)));
                actions.Add((_localizer.Get("Tree.Refresh"), () => RefreshConnectionAsync(connection)));
                actions.Add((_localizer.Get("Tree.Disconnect"), () => DisconnectAsync(connection)));
                break;
            case TreeRowKind.Database:
                actions.Add((_localizer.Get("Tree.NewQuery"), () =>
                {
                    OpenDatabaseQueryTab(row);
                    return Task.CompletedTask;
                }));
                actions.Add((_localizer.Get("Tree.CreateCollection"), () => CreateCollectionAsync(connection, row.Database!)));
                actions.Add((_localizer.Get("Tree.Refresh"), () => RefreshDatabaseAsync(connection, row.Database!)));
                actions.Add((_localizer.Get("Tree.Statistics"), () => ShowDatabaseStatsAsync(connection, row.Database!)));
                actions.Add((_localizer.Get("Tree.DropDatabase"), () => DropDatabaseAsync(connection, row.Database!)));
                break;
            case TreeRowKind.Collection:
                actions.Add((_localizer.Get("Tree.NewQuery"), () =>
                {
                    OpenCollectionTab(row);
                    return Task.CompletedTask;
                }));
                actions.Add((_localizer.Get("Tree.Statistics"), () => ShowCollectionStatsAsync(connection, row.Database!, row.Collection!)));
                actions.Add((_localizer.Get("Tree.Indexes"), () => OpenCollectionIndexesAsync(connection, row.Database!, row.Collection!)));
                actions.Add((_localizer.Get("Tree.ClearCollection"), () => ClearCollectionAsync(connection, row.Database!, row.Collection!)));
                actions.Add((_localizer.Get("Tree.DropCollection"), () => DropCollectionAsync(connection, row.Database!, row.Collection!)));
                break;
        }

        return actions;
    }

    private async Task RefreshConnectionAsync(ActiveConnection connection)
    {
        var databases = await _gateway.ListDatabasesAsync(connection.Target, CancellationToken.None);

        _tree.UpdateDatabases(
            connection.Profile.Id,
            databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase).ToList());

        RebuildTree();
        SetStatus(_localizer.Format("Status.Connected", connection.Profile.Name, databases.Count));
    }

    private async Task RefreshDatabaseAsync(ActiveConnection connection, string database)
    {
        await _tree.ReloadCollectionsAsync(connection.Profile.Id, connection.Target, database, CancellationToken.None);
        RebuildTree();
        SetStatus(database);
    }

    private async Task ShowDatabaseStatsAsync(ActiveConnection connection, string database)
    {
        if (Application is null)
        {
            return;
        }

        var stats = await _databases.GetStatsAsync(connection.Target, DatabaseName.Create(database), CancellationToken.None);

        var text = string.Join(Environment.NewLine,
        [
            $"{_localizer.Get("Stats.Name")}: {stats.Name}",
            $"{_localizer.Get("Stats.Collections")}: {stats.CollectionCount}",
            $"{_localizer.Get("Stats.Documents")}: {stats.ObjectCount}",
            $"{_localizer.Get("Stats.DataSize")}: {FormatBytes(stats.DataSizeBytes)}",
            $"{_localizer.Get("Stats.StorageSize")}: {FormatBytes(stats.StorageSizeBytes)}",
            $"{_localizer.Get("Stats.Indexes")}: {stats.IndexCount}",
        ]);

        Application.Run(new ErrorDialog(_localizer, Application, _localizer.Get("Stats.DatabaseTitle"), text));
    }

    private async Task ShowCollectionStatsAsync(ActiveConnection connection, string database, string collection)
    {
        if (Application is null)
        {
            return;
        }

        var collectionRef = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        var stats = await _indexes.GetStatsAsync(connection.Target, collectionRef, CancellationToken.None);

        var text = string.Join(Environment.NewLine,
        [
            $"{_localizer.Get("Stats.Name")}: {stats.Name}",
            $"{_localizer.Get("Stats.Documents")}: {stats.DocumentCount}",
            $"{_localizer.Get("Stats.Indexes")}: {stats.IndexCount}",
        ]);

        Application.Run(new ErrorDialog(_localizer, Application, _localizer.Get("Stats.CollectionTitle"), text));
    }

    private async Task OpenCollectionIndexesAsync(ActiveConnection connection, string database, string collection)
    {
        if (Application is null)
        {
            return;
        }

        var collectionRef = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        var dialog = new IndexManagerDialog(_indexes, _localizer, Application, connection.Target, collectionRef);
        await dialog.LoadAsync();
        Application.Run(dialog);
    }

    private async Task DropDatabaseAsync(ActiveConnection connection, string database)
    {
        if (!Confirm("Confirm.DropDatabase", database))
        {
            return;
        }

        await _databases.DropDatabaseAsync(connection.Target, DatabaseName.Create(database), CancellationToken.None);
        _tree.RemoveDatabase(connection.Profile.Id, database);
        RebuildTree();
        SetStatus(_localizer.Format("Status.DatabaseDropped", database));
    }

    private async Task DropCollectionAsync(ActiveConnection connection, string database, string collection)
    {
        var name = $"{database}.{collection}";

        if (!Confirm("Confirm.DropCollection", name))
        {
            return;
        }

        var collectionRef = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        await _databases.DropCollectionAsync(connection.Target, collectionRef, CancellationToken.None);
        _tree.RemoveCollection(connection.Profile.Id, database, collection);
        RebuildTree();
        SetStatus(_localizer.Format("Status.CollectionDropped", name));
    }

    private async Task ClearCollectionAsync(ActiveConnection connection, string database, string collection)
    {
        var name = $"{database}.{collection}";

        if (!Confirm("Confirm.ClearCollection", name))
        {
            return;
        }

        var collectionRef = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        await _databases.ClearCollectionAsync(connection.Target, collectionRef, CancellationToken.None);
        SetStatus(_localizer.Format("Status.CollectionCleared", name));
    }

    private bool Confirm(string key, string target)
    {
        if (Application is null)
        {
            return false;
        }

        var dialog = new ConfirmDialog(
            _localizer,
            Application,
            _localizer.Get(key + ".Title"),
            _localizer.Format(key + ".Message", target));

        Application.Run(dialog);

        return dialog.Confirmed;
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is not { } value)
        {
            return "-";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = value;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private TreeRow? SelectedRow() =>
        _treeList.SelectedItem is int index && index >= 0 && index < _rows.Count ? _rows[index] : null;

    private void SetStatus(string? message = null)
    {
        var active = _localizer.Format("Status.ActiveConnections", _tree.Count);

        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = $"{active}  ·  {message}";
        }
        else if (_tree.Count == 0)
        {
            _status.Text = $"{active}  ·  {_localizer.Get("Status.Hint")}";
        }
        else
        {
            _status.Text = active;
        }
    }

    private async Task LoadSettingsAsync()
    {
        var language = await _settings.GetLanguageAsync(CancellationToken.None);
        _language = language is "en" or "ru" ? language : _localizer.CurrentLanguage;

        if (_language is "en" or "ru")
        {
            _localizer.SetLanguage(_language);
        }

        var mode = await _settings.GetDisplayModeAsync(CancellationToken.None);
        _resultViewMode = Enum.TryParse<ResultViewMode>(mode, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ResultViewMode.Table;

        _queryTabs.SetResultViewMode(_resultViewMode);
        ApplyLocalization();
    }

    // Re-applies every localized string that is not refreshed by a dedicated component.
    private void ApplyLocalization()
    {
        Title = _localizer.Get("App.Title");

        if (_treeFrame is not null)
        {
            _treeFrame.Title = _localizer.Get("Tree.Active");
        }

        _queryTabs.RefreshLocalization();
        RefreshMenuBar();
        SetStatus();
    }

    private async Task SetResultViewModeAsync(ResultViewMode mode)
    {
        _resultViewMode = mode;
        _queryTabs.SetResultViewMode(mode);
        await _settings.SetDisplayModeAsync(mode.ToString(), CancellationToken.None);

        RefreshMenuBar();
        SetStatus(_localizer.Format("Status.DisplayMode", _localizer.Get(ModeKey(mode))));
    }

    private async Task SetLanguageAsync(string language)
    {
        _language = language;
        _localizer.SetLanguage(language);
        await _settings.SetLanguageAsync(language, CancellationToken.None);

        ApplyLocalization();
        SetStatus(_localizer.Format("Status.Language", language == "ru" ? "Русский" : "English"));
    }

    // The menu bar is rebuilt on a timer so the item currently being activated is not disposed
    // underneath the command pipeline.
    private void RefreshMenuBar()
    {
        if (_menuBar is null || Application is null)
        {
            return;
        }

        Application.AddTimeout(
            TimeSpan.FromMilliseconds(50),
            () =>
            {
                _menuBar.Menus = BuildMenuItems(_localizer);

                return false;
            });
    }

    private static string ModeKey(ResultViewMode mode) => mode switch
    {
        ResultViewMode.Table => "Result.Table",
        ResultViewMode.Tree => "Result.Tree",
        ResultViewMode.Json => "Result.Json",
        _ => "Result.Raw",
    };

    private void NewTab()
    {
        if (SelectedRow() is not { } row)
        {
            SetStatus(_localizer.Get("Tree.Empty"));
            return;
        }

        _queryTabs.AddTab(row.Owner.Profile.Id, row.Owner.Target, row.Database, row.Collection);
        SetStatus(row.Owner.Profile.Name);
    }

    private void InsertDocument()
    {
        if (ResolveContext() is not { } context)
        {
            return;
        }

        var viewModel = new DocumentEditViewModel(_documents, context.Target, context.Collection);
        var dialog = new DocumentEditDialog(_localizer, Application!, viewModel);
        Application!.Run(dialog);

        SetStatus(dialog.Saved ? _localizer.Get("Documents.Saved") : null);
    }

    private void EditDocument()
    {
        if (ResolveResultContext() is not { } context)
        {
            return;
        }

        var documentJson = _queryTabs.SelectedDocumentJson;

        if (documentJson is null)
        {
            SetStatus(_localizer.Get("Documents.NoDocument"));
            return;
        }

        var viewModel = new DocumentEditViewModel(_documents, context.Target, context.Collection, documentJson);
        var dialog = new DocumentEditDialog(_localizer, Application!, viewModel);
        Application!.Run(dialog);

        if (dialog.Deleted)
        {
            SetStatus(_localizer.Get("Documents.Deleted"));
        }
        else if (dialog.Saved)
        {
            SetStatus(_localizer.Get("Documents.Saved"));
        }
    }

    private void DeleteDocument()
    {
        if (ResolveResultContext() is not { } context)
        {
            return;
        }

        if (_queryTabs.SelectedDocumentJson is not { } documentJson)
        {
            SetStatus(_localizer.Get("Documents.NoDocument"));
            return;
        }

        var filter = DocumentService.BuildIdFilter(documentJson);
        var confirm = new ConfirmDialog(
            _localizer,
            Application!,
            _localizer.Get("Menu.Document.Delete"),
            _localizer.Format("Documents.DeleteCondition", filter));
        Application!.Run(confirm);

        if (confirm.Confirmed)
        {
            Safe(() => DeleteDocumentAsync(context, filter));
        }
    }

    private async Task DeleteDocumentAsync((MongoConnectionTarget Target, CollectionRef Collection) context, string filter)
    {
        try
        {
            var deleted = await _documents.DeleteAsync(context.Target, context.Collection, filter, CancellationToken.None);
            SetStatus(_localizer.Get(deleted > 0 ? "Documents.Deleted" : "Documents.NotFound"));
        }
        catch (DomainException exception)
        {
            SetStatus(exception.Message);
        }
    }

    private (MongoConnectionTarget Target, CollectionRef Collection)? ResolveResultContext()
    {
        if (_queryTabs.ActiveTab is { IsExecuting: false, ResultCollection: { } collection } tab)
            return (tab.Target, collection);
        SetStatus(_localizer.Get("Documents.UnsafeResult"));
        return null;
    }

    private (MongoConnectionTarget Target, CollectionRef Collection)? ResolveContext()
    {
        var tab = _queryTabs.ActiveTab;
        if (tab?.Database is null || tab.Collection is null)
        {
            SetStatus(_localizer.Get("Documents.NoContext"));
            return null;
        }

        var collection = new CollectionRef(DatabaseName.Create(tab.Database), CollectionName.Create(tab.Collection));

        return (tab.Target, collection);
    }

    private async Task OpenIndexesAsync()
    {
        if (ResolveContext() is not { } context || Application is null)
        {
            return;
        }

        var dialog = new IndexManagerDialog(_indexes, _localizer, Application, context.Target, context.Collection);
        await dialog.LoadAsync();
        Application.Run(dialog);
    }

    private void OpenExport()
    {
        if (ResolveContext() is not { } context || Application is null)
        {
            return;
        }

        var dialog = new ExportWizardDialog(_export, _localizer, Application, context.Target, context.Collection);
        Application.Run(dialog);
    }

    private void OpenImport()
    {
        if (ResolveContext() is not { } context || Application is null)
        {
            return;
        }

        var dialog = new ImportWizardDialog(_import, _localizer, Application, context.Target, context.Collection);
        Application.Run(dialog);
    }

    private async Task OpenAiAssistantAsync()
    {
        if (!_aiConfigured)
        {
            SetStatus(_localizer.Get("Ai.NotConfigured"));
            return;
        }

        if (ResolveContext() is not { } context || Application is null)
        {
            return;
        }

        var schema = await DescribeSchemaAsync(context);
        var request = new QueryAssistantRequest { Schema = schema, Prompt = string.Empty };
        var dialog = new AiAssistantDialog(_assistant, _localizer, Application, request);
        Application.Run(dialog);
    }

    // The assistant reasons about the database, its collections and the sampled field types.
    private async Task<CollectionSchema> DescribeSchemaAsync((MongoConnectionTarget Target, CollectionRef Collection) context)
    {
        try
        {
            return await _schema.DescribeAsync(
                context.Target,
                context.Collection.Database.Value,
                context.Collection.Collection.Value,
                CancellationToken.None);
        }
        catch (DomainException exception)
        {
            SetStatus(exception.Message);

            return new CollectionSchema { Collection = context.Collection, Fields = [] };
        }
    }

    private void OpenTranslate()
    {
        if (!_aiConfigured)
        {
            SetStatus(_localizer.Get("Ai.NotConfigured"));
            return;
        }

        if (Application is null)
        {
            return;
        }

        var dialog = new TranslateDialog(_translate, _localizer, Application);
        Application.Run(dialog);
    }

    private async Task OpenHistoryAsync()
    {
        if (Application is null || SelectedRow() is not { } row)
        {
            SetStatus(_localizer.Get("Tree.Empty"));
            return;
        }

        var connectionId = row.Owner.Profile.Id;
        var dialog = new HistoryDialog(_history, _localizer, Application, connectionId);
        await dialog.LoadAsync();
        Application.Run(dialog);
    }

    private void UnlockSecrets()
    {
        if (Application is null)
        {
            return;
        }

        if (_security.CurrentMode != SecurityMode.Password)
        {
            SetStatus(_localizer.Get("Security.Status.Locked"));
            return;
        }

        var dialog = new MasterPasswordDialog(_security, _localizer, Application);
        Application.Run(dialog);

        if (dialog.Unlocked)
        {
            SetStatus(_localizer.Get("MasterPassword.Unlock"));
            Safe(RefreshAiAvailabilityAsync);
        }
    }

    private void OpenChangePassword()
    {
        if (Application is null || _security.CurrentMode != SecurityMode.Password)
        {
            return;
        }

        var dialog = new ChangeMasterPasswordDialog(_security, _localizer, Application);
        Application.Run(dialog);

        if (dialog.Changed)
        {
            SetStatus(_localizer.Get("ChangePassword.Changed"));
        }
    }

    private async Task<bool> EnsureSecurityReadyAsync()
    {
        if (!await EnsureSecurityConfiguredAsync())
        {
            return false;
        }

        RefreshMenuBar();
        return await AutoUnlockSecretsAsync();
    }

    private async Task<bool> EnsureSecurityConfiguredAsync()
    {
        var detection = await _security.DetectAsync(CancellationToken.None);
        if (detection.State == SecurityState.Invalid)
        {
            if (!await HandleInvalidSecurityAsync(detection.Problem ?? SecurityProblem.UnknownKeyMaterial))
            {
                return false;
            }

            detection = await _security.DetectAsync(CancellationToken.None);
        }

        if (detection.State == SecurityState.FirstRun)
        {
            if (!ShowSecurityModeDialog())
            {
                return false;
            }

            detection = await _security.DetectAsync(CancellationToken.None);
        }

        return detection.State == SecurityState.Ready;
    }

    private async Task<bool> AutoUnlockSecretsAsync()
    {
        var result = await _security.TryAutoUnlockAsync(CancellationToken.None);
        if (result == AutoUnlockResult.PasswordRequired)
        {
            UnlockSecrets();
            return true;
        }

        if (result != AutoUnlockResult.KeyMaterialUnavailable)
        {
            return true;
        }

        var problem = _security.CurrentMode == SecurityMode.Keyring
            ? SecurityProblem.KeyringKeyMissing
            : SecurityProblem.KekFileMissing;
        if (!await HandleInvalidSecurityAsync(problem))
        {
            SetStatus(_localizer.Get("Security.Error.NotReady"));
            return false;
        }

        return await EnsureSecurityReadyAsync();
    }

    private async Task<bool> HandleInvalidSecurityAsync(SecurityProblem problem)
    {
        if (Application is null)
        {
            return false;
        }

        Application.Run(new ErrorDialog(
            _localizer,
            Application,
            _localizer.Get("Security.Error.Title"),
            _localizer.Get(ProblemKey(problem))));

        var confirm = new ConfirmDialog(
            _localizer,
            Application,
            _localizer.Get("Security.Settings.ResetTitle"),
            _localizer.Get("Security.Error.ConfirmReset"));
        Application.Run(confirm);

        if (!confirm.Confirmed)
        {
            return false;
        }

        await _security.ResetAsync(CancellationToken.None);
        return true;
    }

    private bool ShowSecurityModeDialog()
    {
        if (Application is null)
        {
            return false;
        }

        var dialog = new SecurityModeDialog(_security, _localizer, Application);
        Application.Run(dialog);
        RefreshMenuBar();

        return dialog.Completed;
    }

    private void OpenSecuritySettings()
    {
        if (Application is null)
        {
            return;
        }

        var dialog = new SecuritySettingsDialog(_security, _localizer, Application);
        Application.Run(dialog);

        if (dialog.Reset)
        {
            SetStatus(_localizer.Get("Security.Settings.ResetDone"));
            if (ShowSecurityModeDialog())
            {
                Safe(RefreshAiAvailabilityAsync);
            }
            else
            {
                Application.RequestStop();
            }

            return;
        }

        if (dialog.Changed)
        {
            RefreshMenuBar();
            SetStatus(_localizer.Get("Security.Settings.Changed"));
            Safe(RefreshAiAvailabilityAsync);
        }
    }

    private static string ProblemKey(SecurityProblem problem) => problem switch
    {
        SecurityProblem.KeyringUnavailable => "Security.Error.KeyringUnavailable",
        SecurityProblem.KeyringKeyMissing => "Security.Error.KeyringKeyMissing",
        SecurityProblem.KekFileMissing => "Security.Error.KekFileMissing",
        SecurityProblem.PasswordMaterialMissing => "Security.Error.PasswordMaterialMissing",
        _ => "Security.Error.UnknownKeyMaterial",
    };

    private async Task OpenAiSettingsAsync()
    {
        if (Application is null)
        {
            return;
        }

        var dialog = new AiSettingsDialog(_aiSettings, _localizer, Application);
        await dialog.LoadAsync(CancellationToken.None);
        Application.Run(dialog);

        if (dialog.Saved)
        {
            await RefreshAiAvailabilityAsync();
            SetStatus(_localizer.Get("Ai.Settings.Saved"));
        }
    }

    private async Task RefreshAiAvailabilityAsync()
    {
        _aiConfigured = await _aiSettings.IsConfiguredAsync(CancellationToken.None);
        RefreshMenuBar();
    }
}
