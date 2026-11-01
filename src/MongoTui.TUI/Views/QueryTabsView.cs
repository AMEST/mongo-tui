using Serilog;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M6.

namespace MongoTui.TUI.Views;

/// <summary>Query workspace: one tab per query, each with an editor and a result pane.</summary>
public sealed class QueryTabsView : View
{
    private readonly QueryService _queryService;
    private readonly AutocompleteService _autocomplete;
    private readonly ILocalizer _localizer;
    private readonly Tabs _tabs = new();
    private readonly Dictionary<View, QueryTabPane> _panes = [];
    private int _counter;
    private ResultViewMode _resultViewMode = ResultViewMode.Table;

    public QueryTabsView(QueryService queryService, ILocalizer localizer, AutocompleteService autocomplete)
    {
        _queryService = queryService;
        _autocomplete = autocomplete;
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
    public string? SelectedDocumentJson => SelectedDocument is { } document ? BsonTextCodec.Format(document) : null;

    public MongoDB.Bson.BsonDocument? SelectedDocument => CurrentPane()?.SelectedDocument();

    public QueryTabViewModel AddTab(
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string? database = null,
        string? collection = null,
        string script = "",
        string? connectionName = null)
    {
        _counter++;
        var model = new QueryTabViewModel(_queryService, _localizer, connectionId, target,
            $"Query {_counter}", database, collection, script, connectionName);
        model.Result.Mode = _resultViewMode;

        var pane = new QueryTabPane(this, model, _localizer, _autocomplete);
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

    public Task<QueryRunSummary?> RunActiveAsync(IApplication application)
    {
        var pane = CurrentPane();
        if (pane is null || pane.Model.IsExecuting || pane.Operation is { IsCompleted: false })
            return Task.FromResult<QueryRunSummary?>(null);
        return pane.Operation = RunAsync(application, pane);
    }

    /// <summary>Reloads the tab's last verified read without changing its editor or the selected tab.</summary>
    public Task<QueryRunSummary?> RefreshAsync(IApplication application, QueryTabViewModel tab)
    {
        var pane = _panes.Values.FirstOrDefault(candidate => candidate.Model == tab);
        if (pane is null || tab.IsExecuting || pane.Operation is { IsCompleted: false } || tab.ResultCollection is null)
            return Task.FromResult<QueryRunSummary?>(null);
        return pane.Operation = RunAsync(application, pane, refresh: true);
    }

    private async Task<QueryRunSummary?> RunAsync(IApplication application, QueryTabPane pane, bool refresh = false)
    {
        var cancellationToken = pane.CancellationToken;
        var script = pane.Editor.SelectedLength > 0 ? pane.Editor.SelectedText : pane.Model.Script;
        Log.Debug("RunActive: executing tab {TabId} ({Title})", pane.Model.Id, pane.Model.Title);
        pane.Editor.CancelCompletion();
        QueryRunSummary? summary = null;
        Task UpdateAsync(Action update) => application.InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested || !_panes.ContainsKey(pane.Container)) return;
            update();
            InvalidateCompletions();
            pane.ShowResult();
            if (!refresh && CurrentPane() == pane) pane.FocusResult();
            summary = new QueryRunSummary(pane.Model.Result.Documents.Count, pane.Model.Result.Elapsed);
            Log.Debug("RunActive: tab {TabId} finished with {DocumentCount} documents, error={Error}",
                pane.Model.Id, pane.Model.Result.Documents.Count, pane.Model.Result.HasError);
        });
        await (refresh ? pane.Model.RefreshAsync(UpdateAsync, cancellationToken)
            : pane.Model.ExecuteAsync(script, UpdateAsync, cancellationToken)).ConfigureAwait(false);

        return summary;
    }

    internal Task<string?> CompleteActiveAsync(CancellationToken cancellationToken) =>
        CurrentPane()?.Editor.CompleteAsync(cancellationToken) ?? Task.FromResult<string?>(null);

    internal void InvalidateCompletions()
    {
        _autocomplete.Invalidate();
        foreach (var pane in _panes.Values)
            pane.Editor.CancelCompletion();
    }

    public async Task CloseForConnectionAsync(ConnectionId connectionId)
    {
        InvalidateCompletions();
        foreach (var pane in _panes.Values.Where(pane => pane.Model.ConnectionId == connectionId).ToList())
        {
            pane.Cancel();
            pane.Editor.CancelCompletion();
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

        pane.Cancel();
        pane.Editor.CancelCompletion();
        await pane.Model.CloseAsync(CancellationToken.None);
        _panes.Remove(pane.Container);
        _tabs.Remove(pane.Container);
        pane.Container.Dispose();
    }

    internal void RaiseRunRequested() => RunRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseExportRequested() => ExportRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseEditDocumentRequested() => EditDocumentRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseDeleteDocumentRequested() => DeleteDocumentRequested?.Invoke(this, EventArgs.Empty);

    private QueryTabPane? CurrentPane()
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

}
