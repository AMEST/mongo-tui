using MongoTui.Application.Localization;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<MainWindow> _logger;
    private readonly DocumentService _documents;
    private readonly IndexService _indexes;
    private readonly DatabaseService _databases;
    private readonly UserManagementService _users;
    private readonly ExportService _export;
    private readonly ImportService _import;
    private readonly IAiAssistant _assistant;
    private readonly IQueryHistoryRepository _history;
    private readonly CollectionSchemaService _schema;
    private readonly TranslateQueryService _translate;
    private readonly SettingsService _settings;
    private readonly ILoggingSettings _loggingSettings;
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
    private bool _openingDocument;
    private readonly CancellationTokenSource _documentLifetime = new();

    public MainWindow(
        ILocalizer localizer,
        ConnectionService connections,
        QueryTabsView queryTabs,
        DocumentService documents,
        IndexService indexes,
        DatabaseService databases,
        UserManagementService users,
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
        ConnectionTunnelService tunnels,
        ILogger<MainWindow> logger,
        ILoggingSettings loggingSettings)
    {
        _localizer = localizer;
        _connections = connections;
        _documents = documents;
        Disposing += (_, _) => { _documentLifetime.Cancel(); _documentLifetime.Dispose(); };
        _indexes = indexes;
        _databases = databases;
        _users = users;
        _export = export;
        _import = import;
        _assistant = assistant;
        _history = history;
        _schema = schema;
        _translate = translate;
        _settings = settings;
        _loggingSettings = loggingSettings;
        _aiSettings = aiSettings;
        _security = security;
        _gateway = gateway;
        _tunnels = tunnels;
        _logger = logger;
        _tree = new TreeViewModel(gateway, localizer);
        _queryTabs = queryTabs;
        _queryTabs.RunRequested += (_, _) => Safe(RunActiveQueryAsync);
        _queryTabs.ExportRequested += (_, _) => OpenExport();
        _queryTabs.EditDocumentRequested += (_, _) => EditDocument();
        _queryTabs.DeleteDocumentRequested += (_, _) => DeleteDocument();

        Title = localizer.Get(LocalizationKeys.App.Title);
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var menuBar = BuildMenuBar(localizer);
        _menuBar = menuBar;
        Add(menuBar);

        var treeFrame = new FrameView
        {
            Title = localizer.Get(LocalizationKeys.Tree.Explorer),
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
            Text = localizer.Get(LocalizationKeys.Status.Hint),
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
            _logger.LogDebug("Shortcut {Key} invoked", key);
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
            _logger.LogError("Startup failed ({ExceptionType})", exception.GetType().Name);
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
            _logger.LogError("Command failed ({ExceptionType})", exception.GetType().Name);
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
        await ReloadProfilesAsync();

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
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.File),
            [
                Item(localizer.Get(LocalizationKeys.Menu.FileExit), Ctrl(KeyCode.Q), () => Application?.RequestStop()),
            ]),
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.Connection),
            [
                Item(localizer.Get(LocalizationKeys.Menu.ConnectionManage), Ctrl(KeyCode.O), () => Safe(ShowManagerAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.ConnectionDisconnect), Key.Empty, () => Safe(DisconnectActiveAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.ConnectionRefresh), Key.F9, () => Safe(RefreshActiveAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.ConnectionUnlock), Key.F8, UnlockSecrets,
                    _security.CurrentMode == SecurityMode.Password),
                Item(localizer.Get(LocalizationKeys.Menu.ConnectionChangePassword), Ctrl(KeyCode.P), OpenChangePassword,
                    _security.CurrentMode == SecurityMode.Password),
            ]),
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.Query),
            [
                Item(localizer.Get(LocalizationKeys.Menu.QueryNew), Ctrl(KeyCode.T), NewTab),
                Item(localizer.Get(LocalizationKeys.Menu.QueryRun), Key.F5, runQuery),
                Item(localizer.Get(LocalizationKeys.Menu.QueryAutocomplete), Key.Empty, () => Safe(CompleteActiveQueryAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.QueryClose), Ctrl(KeyCode.W), () => Safe(_queryTabs.CloseActiveAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.QueryAi), Key.F6, () => Safe(OpenAiAssistantAsync), _aiConfigured),
                Item(localizer.Get(LocalizationKeys.Menu.QueryTranslate), Key.F7, OpenTranslate, _aiConfigured),
                Item(localizer.Get(LocalizationKeys.Menu.QueryHistory), Ctrl(KeyCode.H), () => Safe(OpenHistoryAsync)),
            ]),
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.Document),
            [
                Item(localizer.Get(LocalizationKeys.Menu.DocumentInsert), Ctrl(KeyCode.I), InsertDocument),
                Item(localizer.Get(LocalizationKeys.Menu.DocumentEdit), Ctrl(KeyCode.E), EditDocument),
                Item(localizer.Get(LocalizationKeys.Menu.DocumentDelete), Ctrl(KeyCode.D), EditDocument),
            ]),
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.Collection),
            [
                Item(localizer.Get(LocalizationKeys.Menu.CollectionIndexes), Ctrl(KeyCode.K), () => Safe(OpenIndexesAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.CollectionExport), Key.F2, OpenExport),
                Item(localizer.Get(LocalizationKeys.Menu.CollectionImport), Key.F3, OpenImport),
            ]),
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.Settings),
            [
                new MenuItem
                {
                    Title = localizer.Get(LocalizationKeys.Menu.SettingsDisplay),
                    SubMenu = new Menu(
                    [
                        Choice(localizer.Get(LocalizationKeys.Result.Table), _resultViewMode == ResultViewMode.Table, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Table))),
                        Choice(localizer.Get(LocalizationKeys.Result.Tree), _resultViewMode == ResultViewMode.Tree, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Tree))),
                        Choice(localizer.Get(LocalizationKeys.Result.Json), _resultViewMode == ResultViewMode.Json, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Json))),
                        Choice(localizer.Get(LocalizationKeys.Result.Raw), _resultViewMode == ResultViewMode.Raw, () => Safe(() => SetResultViewModeAsync(ResultViewMode.Raw))),
                    ]),
                },
                new MenuItem
                {
                    Title = localizer.Get(LocalizationKeys.Menu.SettingsLanguage),
                    SubMenu = new Menu(
                    [
                        Choice(UiLanguages.EnglishName, CurrentLanguage == UiLanguages.English, () => Safe(() => SetLanguageAsync(UiLanguages.English))),
                        Choice(UiLanguages.RussianName, CurrentLanguage == UiLanguages.Russian, () => Safe(() => SetLanguageAsync(UiLanguages.Russian))),
                    ]),
                },
                Item(localizer.Get(LocalizationKeys.Menu.SettingsQueries), Key.Empty, () => Safe(OpenQuerySettingsAsync)),
                Item(localizer.Get(LocalizationKeys.Logging.Title), Key.Empty, () => Safe(OpenLoggingSettingsAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.SettingsAi), Key.Empty, () => Safe(OpenAiSettingsAsync)),
                Item(localizer.Get(LocalizationKeys.Menu.SettingsSecurity), Key.Empty, OpenSecuritySettings),
            ]),
            new MenuBarItem(localizer.Get(LocalizationKeys.Menu.Help),
            [
                Item(localizer.Get(LocalizationKeys.Menu.HelpShortcuts), Key.Empty, OpenShortcuts),
                Item(localizer.Get(LocalizationKeys.Menu.HelpAbout), Key.Empty, OpenAbout),
            ]),
        ];
    }

    private string CurrentLanguage => _language ?? _localizer.CurrentLanguage;

    private async Task RefreshActiveAsync()
    {
        _queryTabs.InvalidateCompletions();
        await ReloadProfilesAsync();

        foreach (var connection in _tree.Connections.ToList())
        {
            try
            {
                await RefreshConnectionAsync(connection);
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
        var selected = SelectedRow();
        var connection = selected is null ? _tree.Connections.FirstOrDefault() : selected.Owner;

        if (connection is null)
        {
            SetStatus(_localizer.Get(LocalizationKeys.Tree.Empty));
            return;
        }

        await DisconnectAsync(connection);
    }

    private async Task RunActiveQueryAsync()
    {
        if (Application is not { } application || _queryTabs.ActiveTab is not { IsExecuting: false } tab) return;
        SetStatus(_localizer.Get(LocalizationKeys.Query.Running));
        var summary = await _queryTabs.RunActiveAsync(application).ConfigureAwait(false);

        if (summary is { } result)
        {
            await application.InvokeAsync(() =>
            {
                if (_queryTabs.ActiveTab == tab)
                    SetStatus(_localizer.Format(LocalizationKeys.Status.QueryResult, result.DocumentCount, result.Elapsed.TotalMilliseconds));
            }).ConfigureAwait(false);
        }
    }

    private void RebuildTree(int selectIndex = -1)
    {
        var selected = SelectedRow();
        _rows.Clear();
        var lines = new List<string>();

        var rows = _tree.BuildRows();
        if (rows.Count == 0)
        {
            lines.Add(_localizer.Get(LocalizationKeys.Connection.Empty));
        }
        else
        {
            foreach (var row in rows)
            {
                _rows.Add(row);
                lines.Add(row.Text);
            }
        }

        _treeList.SetSource(new ObservableCollection<string>(lines));
        if (selectIndex < 0 && selected is not null)
        {
            selectIndex = _rows.FindIndex(row => row.Profile.Id == selected.Profile.Id && row.Kind == selected.Kind
                && row.Database == selected.Database && row.Collection == selected.Collection);
            if (selectIndex < 0) selectIndex = _rows.FindIndex(row => row.Profile.Id == selected.Profile.Id);
        }
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

        var id = row.Profile.Id;
        var index = _treeList.SelectedItem ?? -1;

        switch (row.Kind)
        {
            case TreeRowKind.Connection:
                if (row.Owner is null) await ConnectProfileAsync(row.Profile);
                else
                {
                    _tree.ToggleConnection(id);
                    RebuildTree(index);
                }
                break;
            case TreeRowKind.Database:
                await _tree.ToggleDatabaseAsync(id, row.Owner!.Target, row.Database!, CancellationToken.None);
                RebuildTree(index);
                break;
            case TreeRowKind.Collection:
                await OpenCollectionTabAsync(row).ConfigureAwait(false);
                break;
        }
    }

    private async Task OpenCollectionTabAsync(TreeRow row)
    {
        var script = BuildCollectionScript(row.Collection!);
        _queryTabs.AddTab(row.Profile.Id, row.Owner!.Target, row.Database, row.Collection, script,
            connectionName: row.Profile.Name);
        SetStatus($"{row.Database}.{row.Collection}");
        await RunActiveQueryAsync().ConfigureAwait(false);
    }

    private void OpenDatabaseQueryTab(TreeRow row)
    {
        _queryTabs.AddTab(row.Profile.Id, row.Owner!.Target, row.Database, collection: null, EmptyQueryScript,
            connectionName: row.Profile.Name);
        SetStatus(row.Database!);
    }

    private static string BuildCollectionScript(string collection) =>
        $"db.getCollection({JsonSerializer.Serialize(collection)}).find({{}}).limit(50).toArray()";

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
        if (row.Kind == TreeRowKind.Connection) return BuildConnectionActions(row);
        var connection = row.Owner;
        var actions = new List<(string Title, Func<Task> Handler)>();
        if (connection is null) return actions;

        switch (row.Kind)
        {
            case TreeRowKind.Database:
                actions.Add((_localizer.Get(LocalizationKeys.Tree.NewQuery), () =>
                {
                    OpenDatabaseQueryTab(row);
                    return Task.CompletedTask;
                }));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.CreateCollection), () => CreateCollectionAsync(connection, row.Database!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.Refresh), () => RefreshDatabaseAsync(connection, row.Database!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.Statistics), () => ShowDatabaseStatsAsync(connection, row.Database!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.ManageUsers), () => OpenUserManagementAsync(connection, row.Database!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.DropDatabase), () => DropDatabaseAsync(connection, row.Database!)));
                break;
            case TreeRowKind.Collection:
                actions.Add((_localizer.Get(LocalizationKeys.Tree.NewQuery), () => OpenCollectionTabAsync(row)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.Statistics), () => ShowCollectionStatsAsync(connection, row.Database!, row.Collection!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.Indexes), () => OpenCollectionIndexesAsync(connection, row.Database!, row.Collection!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.ClearCollection), () => ClearCollectionAsync(connection, row.Database!, row.Collection!)));
                actions.Add((_localizer.Get(LocalizationKeys.Tree.DropCollection), () => DropCollectionAsync(connection, row.Database!, row.Collection!)));
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
        foreach (var database in databases.Where(database => _tree.IsExpanded(connection.Profile.Id, database.Name)))
        {
            await _tree.ReloadCollectionsAsync(connection.Profile.Id, connection.Target, database.Name, CancellationToken.None);
        }

        RebuildTree();
        SetStatus(_localizer.Format(LocalizationKeys.Status.Connected, connection.Profile.Name, databases.Count));
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
            $"{_localizer.Get(LocalizationKeys.Stats.Name)}: {stats.Name}",
            $"{_localizer.Get(LocalizationKeys.Stats.Collections)}: {stats.CollectionCount}",
            $"{_localizer.Get(LocalizationKeys.Stats.Documents)}: {stats.ObjectCount}",
            $"{_localizer.Get(LocalizationKeys.Stats.DataSize)}: {FormatBytes(stats.DataSizeBytes)}",
            $"{_localizer.Get(LocalizationKeys.Stats.StorageSize)}: {FormatBytes(stats.StorageSizeBytes)}",
            $"{_localizer.Get(LocalizationKeys.Stats.Indexes)}: {stats.IndexCount}",
        ]);

        Application.Run(new ErrorDialog(_localizer, Application, _localizer.Get(LocalizationKeys.Stats.DatabaseTitle), text));
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
            $"{_localizer.Get(LocalizationKeys.Stats.Name)}: {stats.Name}",
            $"{_localizer.Get(LocalizationKeys.Stats.Documents)}: {stats.DocumentCount}",
            $"{_localizer.Get(LocalizationKeys.Stats.Indexes)}: {stats.IndexCount}",
        ]);

        Application.Run(new ErrorDialog(_localizer, Application, _localizer.Get(LocalizationKeys.Stats.CollectionTitle), text));
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
        SetStatus(_localizer.Format(LocalizationKeys.Status.DatabaseDropped, database));
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
        SetStatus(_localizer.Format(LocalizationKeys.Status.CollectionDropped, name));
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
        SetStatus(_localizer.Format(LocalizationKeys.Status.CollectionCleared, name));
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
        var active = _localizer.Format(LocalizationKeys.Status.ActiveConnections, _tree.Count);

        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = $"{active}  ·  {message}";
        }
        else if (_tree.Count == 0)
        {
            _status.Text = $"{active}  ·  {_localizer.Get(LocalizationKeys.Status.Hint)}";
        }
        else
        {
            _status.Text = active;
        }
    }

    private async Task LoadSettingsAsync()
    {
        var language = await _settings.GetLanguageAsync(CancellationToken.None);
        _language = language is UiLanguages.English or UiLanguages.Russian ? language : _localizer.CurrentLanguage;

        if (_language is UiLanguages.English or UiLanguages.Russian)
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
        Title = _localizer.Get(LocalizationKeys.App.Title);

        if (_treeFrame is not null)
        {
            _treeFrame.Title = _localizer.Get(LocalizationKeys.Tree.Explorer);
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
        SetStatus(_localizer.Format(LocalizationKeys.Status.DisplayMode, _localizer.Get(ModeKey(mode))));
    }

    private async Task SetLanguageAsync(string language)
    {
        _language = language;
        _localizer.SetLanguage(language);
        await _settings.SetLanguageAsync(language, CancellationToken.None);

        ApplyLocalization();
        SetStatus(_localizer.Format(LocalizationKeys.Status.Language, language == UiLanguages.Russian ? UiLanguages.RussianName : UiLanguages.EnglishName));
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
        ResultViewMode.Table => LocalizationKeys.Result.Table,
        ResultViewMode.Tree => LocalizationKeys.Result.Tree,
        ResultViewMode.Json => LocalizationKeys.Result.Json,
        _ => LocalizationKeys.Result.Raw,
    };

    private void NewTab()
    {
        if (SelectedRow() is not { Owner: { } connection } row)
        {
            SetStatus(_localizer.Get(LocalizationKeys.Tree.Empty));
            return;
        }

        _queryTabs.AddTab(row.Profile.Id, connection.Target, row.Database, row.Collection, connectionName: row.Profile.Name);
        SetStatus(row.Profile.Name);
    }

    private Task OpenIndexesAsync()
    {
        if (ResolveContext() is not { } context || Application is null)
        {
            return Task.CompletedTask;
        }

        using var dialog = new IndexManagerDialog(_indexes, _localizer, Application, context.Target, context.Collection);
        _ = dialog.LoadAsync();
        Application.Run(dialog);
        return Task.CompletedTask;
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
            SetStatus(_localizer.Get(LocalizationKeys.Ai.NotConfigured));
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
            SetStatus(_localizer.Get(LocalizationKeys.Ai.NotConfigured));
            return;
        }

        if (Application is null)
        {
            return;
        }

        var dialog = new TranslateDialog(_translate, _localizer, Application);
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
            SetStatus(_localizer.Get(LocalizationKeys.Security.StatusLocked));
            return;
        }

        var dialog = new MasterPasswordDialog(_security, _localizer, Application);
        Application.Run(dialog);

        if (dialog.Unlocked)
        {
            SetStatus(_localizer.Get(LocalizationKeys.MasterPassword.Unlock));
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
            SetStatus(_localizer.Get(LocalizationKeys.ChangePassword.Changed));
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
            SetStatus(_localizer.Get(LocalizationKeys.Security.ErrorNotReady));
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
            _localizer.Get(LocalizationKeys.Security.ErrorTitle),
            _localizer.Get(ProblemKey(problem))));

        var confirm = new ConfirmDialog(
            _localizer,
            Application,
            _localizer.Get(LocalizationKeys.Security.SettingsResetTitle),
            _localizer.Get(LocalizationKeys.Security.ErrorConfirmReset));
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
            SetStatus(_localizer.Get(LocalizationKeys.Security.SettingsResetDone));
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
            SetStatus(_localizer.Get(LocalizationKeys.Security.SettingsChanged));
            Safe(RefreshAiAvailabilityAsync);
        }
    }

    private static string ProblemKey(SecurityProblem problem) => problem switch
    {
        SecurityProblem.KeyringUnavailable => LocalizationKeys.Security.ErrorKeyringUnavailable,
        SecurityProblem.KeyringKeyMissing => LocalizationKeys.Security.ErrorKeyringKeyMissing,
        SecurityProblem.KekFileMissing => LocalizationKeys.Security.ErrorKekFileMissing,
        SecurityProblem.PasswordMaterialMissing => LocalizationKeys.Security.ErrorPasswordMaterialMissing,
        _ => LocalizationKeys.Security.ErrorUnknownKeyMaterial,
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
            SetStatus(_localizer.Get(LocalizationKeys.Ai.SettingsSaved));
        }
    }

    private async Task RefreshAiAvailabilityAsync()
    {
        _aiConfigured = await _aiSettings.IsConfiguredAsync(CancellationToken.None);
        RefreshMenuBar();
    }
}
