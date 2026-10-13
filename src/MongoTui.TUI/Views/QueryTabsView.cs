using MongoDB.Bson;
using Serilog;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M6.

namespace MongoTui.TUI.Views;

/// <summary>Query workspace: one tab per query, each with an editor and a result pane.</summary>
public sealed class QueryTabsView : View
{
    private readonly QueryService _queryService;
    private readonly ILocalizer _localizer;
    private readonly Tabs _tabs = new();
    private readonly Dictionary<View, TabPane> _panes = [];
    private int _counter;
    private ResultViewMode _resultViewMode = ResultViewMode.Table;

    public QueryTabsView(QueryService queryService, ILocalizer localizer)
    {
        _queryService = queryService;
        _localizer = localizer;

        CanFocus = true;

        _tabs.X = 0;
        _tabs.Y = 0;
        _tabs.Width = Dim.Fill();
        _tabs.Height = Dim.Fill();
        Add(_tabs);
    }

    /// <summary>Raised when the user requests running the active query from its editor context menu.</summary>
    public event EventHandler? RunRequested;

    /// <summary>Raised when the user requests exporting the active result from its context menu.</summary>
    public event EventHandler? ExportRequested;

    /// <summary>Raised when the user requests editing the document under the result cursor.</summary>
    public event EventHandler? EditDocumentRequested;

    /// <summary>Raised when the user requests deleting the document under the result cursor.</summary>
    public event EventHandler? DeleteDocumentRequested;

    public int TabCount => _panes.Count;

    public QueryTabViewModel? ActiveTab => CurrentPane()?.Model;

    /// <summary>Active tab's editor text view (exposed for tests).</summary>
    internal TextView? ActiveEditorView => CurrentPane()?.Editor;

    /// <summary>Active tab's result text view (exposed for tests).</summary>
    internal TextView? ActiveResultView => CurrentPane()?.ResultView;

    /// <summary>JSON of the result document at the cursor, or the first one when not applicable.</summary>
    public string? SelectedDocumentJson => CurrentPane()?.SelectedDocumentJson();

    public QueryTabViewModel AddTab(
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string? database = null,
        string? collection = null,
        string script = "")
    {
        _counter++;
        var model = new QueryTabViewModel(_queryService, connectionId, target, $"Query {_counter}", database, collection, script);
        model.Result.Mode = _resultViewMode;

        var pane = new TabPane(this, model, _localizer);
        _panes.Add(pane.Container, pane);

        _tabs.Add(pane.Container);
        _tabs.Value = pane.Container;
        pane.Editor.SetFocus();

        return model;
    }

    public void SetResultViewMode(ResultViewMode mode)
    {
        _resultViewMode = mode;

        foreach (var pane in _panes.Values)
        {
            pane.Model.Result.Mode = mode;
            pane.ShowResult();
        }
    }

    public void RefreshLocalization()
    {
        foreach (var pane in _panes.Values)
        {
            pane.RefreshLocalization(_localizer);
        }
    }

    public async Task<QueryRunSummary?> RunActiveAsync()
    {
        if (CurrentPane() is not { } pane)
        {
            Log.Debug("RunActive: no active query pane");
            return null;
        }

        Log.Debug("RunActive: executing tab {TabId} ({Title})", pane.Model.Id, pane.Model.Title);
        await pane.Model.ExecuteAsync(CancellationToken.None);
        pane.ShowResult();
        pane.FocusResult();

        var summary = new QueryRunSummary(pane.Model.Result.Documents.Count, pane.Model.Result.Elapsed);

        Log.Debug(
            "RunActive: tab {TabId} finished with {DocumentCount} documents, error={Error}",
            pane.Model.Id,
            pane.Model.Result.Documents.Count,
            pane.Model.Result.Error);

        return summary;
    }

    public async Task CloseForConnectionAsync(ConnectionId connectionId)
    {
        foreach (var pane in _panes.Values.Where(pane => pane.Model.ConnectionId == connectionId).ToList())
        {
            await pane.Model.CloseAsync(CancellationToken.None);
            _panes.Remove(pane.Container);
            _tabs.Remove(pane.Container);
            pane.Container.Dispose();
        }
    }

    public async Task CloseActiveAsync()
    {
        if (CurrentPane() is not { } pane)
        {
            return;
        }

        await pane.Model.CloseAsync(CancellationToken.None);
        _panes.Remove(pane.Container);
        _tabs.Remove(pane.Container);
        pane.Container.Dispose();
    }

    private void RaiseRunRequested() => RunRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseExportRequested() => ExportRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseEditDocumentRequested() => EditDocumentRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseDeleteDocumentRequested() => DeleteDocumentRequested?.Invoke(this, EventArgs.Empty);

    private TabPane? CurrentPane()
    {
        if (_tabs.Value is View view && _panes.TryGetValue(view, out var pane))
        {
            return pane;
        }

        foreach (var (container, candidate) in _panes)
        {
            if (container.Focused is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed class TabPane
    {
        private readonly QueryTabsView _owner;
        private readonly ResultTextView _result;
        private readonly FrameView _editorFrame;
        private readonly FrameView _resultFrame;
        private bool _menuBuilt;

        public TabPane(QueryTabsView owner, QueryTabViewModel model, ILocalizer localizer)
        {
            _owner = owner;
            Model = model;
            Container = new View
            {
                Title = model.Title,
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            _editorFrame = new FrameView
            {
                Title = localizer.Get("Query.Pane"),
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(50),
            };

            Editor = new TextView { Multiline = true, Width = Dim.Fill(), Height = Dim.Fill(), Text = model.Script };
            _result = new ResultTextView { Multiline = true, ReadOnly = true, WordWrap = false, Width = Dim.Fill(), Height = Dim.Fill() };

            Editor.TrackContents(() => Model.Script = Editor.Text ?? string.Empty);
            _editorFrame.Add(Editor);

            _resultFrame = new FrameView
            {
                Title = localizer.Get("Query.Result"),
                X = 0,
                Y = Pos.Bottom(_editorFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            _resultFrame.Add(_result);

            Container.Add(_editorFrame);
            Container.Add(_resultFrame);

            Container.Initialized += (_, _) => BuildContextMenus(localizer);
        }

        public QueryTabViewModel Model { get; }

        public View Container { get; }

        public TextView Editor { get; }

        /// <summary>Result text view (exposed for tests).</summary>
        internal TextView ResultView => _result;

        public void ShowResult() => _result.Text = Model.Result.Render();

        public void FocusResult() => _result.SetFocus();

        // In table mode documents start below the header (column names + separator).
        public string? SelectedDocumentJson()
        {
            if (Model.Result.DocumentIndexAtRow(_result.DocumentRow) is not { } index)
            {
                return null;
            }

            return Model.Result.Documents[index].ToJson();
        }

        public void RefreshLocalization(ILocalizer localizer)
        {
            _editorFrame.Title = localizer.Get("Query.Pane");
            _resultFrame.Title = localizer.Get("Query.Result");
        }

        private void BuildContextMenus(ILocalizer localizer)
        {
            if (_menuBuilt)
            {
                return;
            }

            _menuBuilt = true;
            BuildEditorContextMenu(localizer);
            BuildResultContextMenu(localizer);
        }

        private void BuildEditorContextMenu(ILocalizer localizer)
        {
            if (Editor.ContextMenu?.Root is not { } root)
            {
                return;
            }

            root.Add(new Line());
            root.Add(new MenuItem(localizer.Get("Menu.Query.Run"), string.Empty, _owner.RaiseRunRequested));
        }

        private void BuildResultContextMenu(ILocalizer localizer)
        {
            if (_result.ContextMenu?.Root is not { } root)
            {
                return;
            }

            root.Add(new Line());
            root.Add(new MenuItem(localizer.Get("Result.CopyAll"), string.Empty, CopyAll));
            root.Add(new MenuItem(localizer.Get("Result.CopyDocument"), string.Empty, CopySelectedDocument));
            root.Add(new MenuItem(localizer.Get("Result.Export"), string.Empty, _owner.RaiseExportRequested));
            root.Add(new MenuItem(localizer.Get("Menu.Document.Edit"), string.Empty, _owner.RaiseEditDocumentRequested));
            root.Add(new MenuItem(localizer.Get("Menu.Document.Delete"), string.Empty, _owner.RaiseDeleteDocumentRequested));
        }

        private void CopyAll() => _result.App?.Clipboard?.SetClipboardData(_result.Text ?? string.Empty);

        private void CopySelectedDocument()
        {
            if (SelectedDocumentJson() is { } json)
            {
                _result.App?.Clipboard?.SetClipboardData(json);
            }
        }
    }
}
