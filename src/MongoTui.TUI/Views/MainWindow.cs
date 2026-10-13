using System.Collections.ObjectModel;
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
public sealed class MainWindow : Window
{
    private readonly ILocalizer _localizer;
    private readonly ConnectionService _connections;
    private readonly DocumentService _documents;
    private readonly IndexService _indexes;
    private readonly ExportService _export;
    private readonly ImportService _import;
    private readonly IAiAssistant _assistant;
    private readonly IQueryHistoryRepository _history;
    private readonly TranslateQueryService _translate;
    private readonly SettingsService _settings;
    private readonly IMongoGateway _gateway;
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

    public MainWindow(
        ILocalizer localizer,
        ConnectionService connections,
        QueryService queryService,
        DocumentService documents,
        IndexService indexes,
        ExportService export,
        ImportService import,
        IAiAssistant assistant,
        IQueryHistoryRepository history,
        TranslateQueryService translate,
        SettingsService settings,
        IMongoGateway gateway)
    {
        _localizer = localizer;
        _connections = connections;
        _documents = documents;
        _indexes = indexes;
        _export = export;
        _import = import;
        _assistant = assistant;
        _history = history;
        _translate = translate;
        _settings = settings;
        _gateway = gateway;
        _tree = new TreeViewModel(gateway, localizer);
        _queryTabs = new QueryTabsView(queryService, localizer);

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

        if (_shortcuts.TryGetValue(key, out var action))
        {
            Log.Debug("Shortcut {Key} invoked", key);
            key.Handled = true;
            action();
        }
    }

    /// <summary>Loads the initial state into the shell.</summary>
    public void Compose()
    {
    }

    /// <summary>
    /// Startup flow: unlock secrets (keyring automatically, otherwise via the master
    /// password dialog), then open the connection manager.
    /// </summary>
    public async Task OpenStartupAsync()
    {
        try
        {
            await LoadSettingsAsync();

            if (!await _connections.TryAutoUnlockAsync(CancellationToken.None))
            {
                UnlockSecrets();
            }

            await ShowManagerAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Startup failed");
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
            Log.Error(exception, "Command failed");
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

        // MenuBar menu items only receive their shortcut while their popover is open
        // (Terminal.Gui bails out of Command.HotKey for non-visible items). Register the
        // action here as an application-level shortcut handled by KeyDownNotHandled instead.
        MenuItem Item(string text, Key key, Action action)
        {
            if (key != Key.Empty)
            {
                _shortcuts[key] = action;
            }

            return new MenuItem(text, string.Empty, action, key);
        }

        static MenuItem Choice(string text, bool selected, Action action) =>
            new((selected ? "\u2022 " : "   ") + text, string.Empty, action);

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
                Item(localizer.Get("Menu.Connection.Unlock"), Key.F8, UnlockSecrets),
                Item(localizer.Get("Menu.Connection.ChangePassword"), Ctrl(KeyCode.P), OpenChangePassword),
            ]),
            new MenuBarItem(localizer.Get("Menu.Query"),
            [
                Item(localizer.Get("Menu.Query.New"), Ctrl(KeyCode.T), NewTab),
                Item(localizer.Get("Menu.Query.Run"), Key.F5, () => Safe(_queryTabs.RunActiveAsync)),
                Item(localizer.Get("Menu.Query.Close"), Ctrl(KeyCode.W), () => Safe(_queryTabs.CloseActiveAsync)),
                Item(localizer.Get("Menu.Query.Ai"), Key.F6, OpenAiAssistant),
                Item(localizer.Get("Menu.Query.Translate"), Key.F7, OpenTranslate),
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
            ]),
        ];
    }

    private string CurrentLanguage => _language ?? _localizer.CurrentLanguage;

    private async Task ConnectAsync(ConnectionProfile profile, MongoConnectionTarget target)
    {
        try
        {
            var databases = await _gateway.ListDatabasesAsync(target, CancellationToken.None);
            Log.Debug("Connected to {Profile}: {Count} databases returned", profile.Name, databases.Count);

            _tree.AddOrReplace(new ActiveConnection(
                profile,
                target,
                databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase).ToList()));

            RebuildTree();
            SetStatus(_localizer.Format("Status.Connected", profile.Name, databases.Count));
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Connect failed");
            SetStatus(exception.Message);

            if (Application is not null)
            {
                Application.Run(new ErrorDialog(_localizer, Application, _localizer.Get("Common.Error"), exception.Message));
            }
        }
    }

    private async Task RefreshActiveAsync()
    {
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

        _tree.Remove(connection.Profile.Id);
        await _queryTabs.CloseForConnectionAsync(connection.Profile.Id);
        RebuildTree();

        Log.Information("Disconnected from {Profile}", connection.Profile.Name);
        SetStatus(_localizer.Format("Status.Disconnected", connection.Profile.Name));
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
        var script = BuildCollectionScript(row.Database!, row.Collection!);
        _queryTabs.AddTab(row.Owner.Profile.Id, row.Owner.Target, row.Database, row.Collection, script);
        SetStatus($"{row.Database}.{row.Collection}");
    }

    private static string BuildCollectionScript(string database, string collection) =>
        $"db.getSiblingDB(\"{database}\").getCollection(\"{collection}\").find({{}}).limit(50).toArray()";

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
        RefreshMenuBar();
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

        if (_treeFrame is not null)
        {
            _treeFrame.Title = _localizer.Get("Tree.Active");
        }

        _queryTabs.RefreshLocalization();
        RefreshMenuBar();
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
        _ => "Result.Json",
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
        if (ResolveContext() is not { } context)
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

    private void OpenAiAssistant()
    {
        if (ResolveContext() is not { } context || Application is null)
        {
            return;
        }

        var schema = new CollectionSchema { Collection = context.Collection, Fields = [] };
        var request = new QueryAssistantRequest { Schema = schema, Prompt = string.Empty };
        var dialog = new AiAssistantDialog(_assistant, _localizer, Application, request);
        Application.Run(dialog);
    }

    private void OpenTranslate()
    {
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

        var dialog = new MasterPasswordDialog(_connections, _localizer, Application);
        Application.Run(dialog);

        if (dialog.Unlocked)
        {
            SetStatus(_localizer.Get("MasterPassword.Unlock"));
        }
    }

    private void OpenChangePassword()
    {
        if (Application is null)
        {
            return;
        }

        var dialog = new ChangeMasterPasswordDialog(_connections, _localizer, Application);
        Application.Run(dialog);

        if (dialog.Changed)
        {
            SetStatus(_localizer.Get("ChangePassword.Changed"));
        }
    }
}
