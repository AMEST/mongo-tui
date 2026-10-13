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

    public int TabCount => _panes.Count;

    public QueryTabViewModel? ActiveTab => CurrentPane()?.Model;

    public QueryTabViewModel AddTab(
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string? database = null,
        string? collection = null,
        string script = "")
    {
        _counter++;
        var model = new QueryTabViewModel(_queryService, connectionId, target, $"Query {_counter}", database, collection, script);

        var pane = new TabPane(model, _localizer);
        _panes.Add(pane.Container, pane);

        _tabs.Add(pane.Container);
        _tabs.Value = pane.Container;
        pane.Editor.SetFocus();

        return model;
    }

    public async Task RunActiveAsync()
    {
        if (CurrentPane() is not { } pane)
        {
            Log.Debug("RunActive: no active query pane");
            return;
        }

        Log.Debug("RunActive: executing tab {TabId} ({Title})", pane.Model.Id, pane.Model.Title);
        await pane.Model.ExecuteAsync(CancellationToken.None);
        pane.ShowResult();
        pane.FocusResult();

        Log.Debug(
            "RunActive: tab {TabId} finished with {DocumentCount} documents, error={Error}",
            pane.Model.Id,
            pane.Model.Result.Documents.Count,
            pane.Model.Result.Error);
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
        private readonly TextView _result;

        public TabPane(QueryTabViewModel model, ILocalizer localizer)
        {
            Model = model;
            Container = new View
            {
                Title = model.Title,
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            var editorFrame = new FrameView
            {
                Title = localizer.Get("Query.Pane"),
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(50),
            };

            Editor = new TextView { Multiline = true, Width = Dim.Fill(), Height = Dim.Fill(), Text = model.Script };
            _result = new TextView { Multiline = true, ReadOnly = true, Width = Dim.Fill(), Height = Dim.Fill() };

            Editor.ContentsChanged += (_, _) => Model.Script = Editor.Text ?? string.Empty;
            editorFrame.Add(Editor);

            var resultFrame = new FrameView
            {
                Title = localizer.Get("Query.Result"),
                X = 0,
                Y = Pos.Bottom(editorFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            resultFrame.Add(_result);

            Container.Add(editorFrame);
            Container.Add(resultFrame);
        }

        public QueryTabViewModel Model { get; }

        public View Container { get; }

        public TextView Editor { get; }

        public void ShowResult() => _result.Text = Model.Result.Render();

        public void FocusResult() => _result.SetFocus();
    }
}
