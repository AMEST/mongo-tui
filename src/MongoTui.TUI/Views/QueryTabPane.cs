using System.Diagnostics;
using MongoTui.Application.Localization;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Views;

internal sealed class QueryTabPane
{
    private readonly QueryTabsView _owner;
    private readonly ILocalizer _localizer;
    private readonly ResultTextView _result;
    private readonly Button _openOutput = new() { X = 1, Y = Pos.AnchorEnd(1), Visible = false };
    private readonly Label _context = new() { X = 1, Y = 0, Width = Dim.Fill(1), Height = 1 };
    private readonly FrameView _editorFrame;
    private readonly FrameView _resultFrame;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<(MenuItem Item, string Key)> _menuLabels = [];
    private bool _menuBuilt;
    private ScriptOutput? _shownOutput;
    private long _shownLength;
    private long _lastOutputDraw;
    private bool _showingRaw;

    public QueryTabPane(QueryTabsView owner, QueryTabViewModel model, ILocalizer localizer, AutocompleteService autocomplete)
    {
        _owner = owner;
        _localizer = localizer;
        Model = model;
        Container = new View { Title = model.Title, Width = Dim.Fill(), Height = Dim.Fill() };
        Container.Disposing += (_, _) =>
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _ = Model.DisposeOutputAsync();
        };
        RefreshContext(localizer);

        _editorFrame = new FrameView
        {
            Title = localizer.Get(LocalizationKeys.Query.Pane),
            X = 0,
            Y = Pos.Bottom(_context),
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
        };

        Editor = new QueryEditorView(autocomplete, model) { Width = Dim.Fill(), Height = Dim.Fill() };
        _result = new ResultTextView { Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = true, Width = Dim.Fill(), Height = Dim.Fill() };

        Editor.TrackContents(() => Model.Script = Editor.Text ?? string.Empty);
        _editorFrame.Add(Editor);

        _resultFrame = new FrameView
        {
            Title = localizer.Get(LocalizationKeys.Query.Result),
            X = 0,
            Y = Pos.Bottom(_editorFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _openOutput.OnAccept(OpenOutput);
        _resultFrame.Add(_result, _openOutput);

        Container.Add(_context, _editorFrame, _resultFrame);

        Container.Initialized += (_, _) => BuildContextMenus(localizer);
    }

    public QueryTabViewModel Model { get; }

    public View Container { get; }

    public QueryEditorView Editor { get; }

    public CancellationToken CancellationToken => _lifetime.Token;

    public Task<QueryRunSummary?>? Operation { get; set; }

    public void Cancel() => _lifetime.Cancel();

    /// <summary>Result text view (exposed for tests).</summary>
    internal TextView ResultView => _result;

    public void ShowResult()
    {
        var raw = Model.Result.Mode == ResultViewMode.Raw;
        var showShortcut = !raw && Model.Result is { HasError: true, Output.Length: > 0 };
        if (_openOutput.Visible != showShortcut)
        {
            _openOutput.Visible = showShortcut;
            _result.Height = Dim.Fill(showShortcut ? 1 : 0);
        }
        if (raw) ShowRawOutput();
        else
        {
            if (_showingRaw) _resultFrame.Title = _localizer.Get(LocalizationKeys.Query.Result);
            _showingRaw = false;
            _shownOutput = null;
            _result.Text = Model.Result.Render();
        }
    }

    public void RefreshLiveOutput(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || Model.Result.Mode != ResultViewMode.Raw
            || Stopwatch.GetElapsedTime(_lastOutputDraw) < ScriptOutput.RefreshInterval) return;
        _lastOutputDraw = Stopwatch.GetTimestamp();
        ShowRawOutput();
    }

    private void ShowRawOutput()
    {
        var output = Model.Result.Output;
        var length = output?.Length ?? 0;
        var reset = !_showingRaw || output != _shownOutput;
        if (!reset && length == _shownLength && length > 0) return;
        _shownOutput = output;
        _shownLength = length;
        _showingRaw = true;
        var (text, hasEarlierOutput) = output?.ReadPreview() ?? (string.Empty, false);
        var title = _localizer.Get(LocalizationKeys.Query.Result);
        _resultFrame.Title = hasEarlierOutput ? $"{title} · {_localizer.Get(LocalizationKeys.Result.OutputPreview)}" : title;
        if (text.Length == 0) text = Model.Result.Error ?? string.Empty;
        _result.ShowOutput(text, reset);
    }

    public void FocusResult() => _result.SetFocus();

    // In table mode documents start below the header (column names + separator).
    public BsonDocument? SelectedDocument()
    {
        if (Model.Result.DocumentIndexAtRow(_result.DocumentRow) is not { } index)
        {
            return null;
        }

        return Model.Result.Documents[index];
    }

    public void RefreshLocalization(ILocalizer localizer)
    {
        RefreshContext(localizer);
        _editorFrame.Title = localizer.Get(LocalizationKeys.Query.Pane);
        _resultFrame.Title = localizer.Get(LocalizationKeys.Query.Result);
        _openOutput.Text = localizer.Get(LocalizationKeys.Result.OpenOutput);
        if (_showingRaw) { _shownLength = -1; ShowRawOutput(); }
        foreach (var (item, key) in _menuLabels)
        {
            item.Title = localizer.Get(key);
            if (TextContextMenuLabels.GetHelpKey(item.Command) is { } help) item.HelpText = localizer.Get(help);
        }
    }

    private void RefreshContext(ILocalizer localizer) => _context.Text =
        localizer.Format(LocalizationKeys.Query.Context, Model.ConnectionName, Model.ContextDatabase);

    private void BuildContextMenus(ILocalizer localizer)
    {
        if (_menuBuilt)
        {
            return;
        }

        _menuBuilt = true;
        BuildEditorContextMenu(localizer);
        BuildResultContextMenu(localizer);
        foreach (var view in new TextView[] { Editor, _result })
            foreach (var item in view.ContextMenu?.Root?.SubViews.OfType<MenuItem>() ?? [])
                if (TextContextMenuLabels.GetKey(item.Command) is { } key) _menuLabels.Add((item, key));
        RefreshLocalization(localizer);
    }

    private void BuildEditorContextMenu(ILocalizer localizer)
    {
        if (Editor.ContextMenu?.Root is not { } root)
        {
            return;
        }

        root.Add(new Line());
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Menu.QueryRun, _owner.RaiseRunRequested));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Menu.QueryOpenFile, _owner.RaiseOpenQueryFileRequested));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Menu.QuerySaveFile, _owner.RaiseSaveQueryFileRequested));
    }

    private void BuildResultContextMenu(ILocalizer localizer)
    {
        if (_result.ContextMenu?.Root is not { } root)
        {
            return;
        }

        root.Add(new Line());
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Result.CopyAll, CopyAll));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Result.CopyDocument, CopySelectedDocument));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Result.OpenOutput, OpenOutput));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Result.SaveOutput, _owner.RaiseSaveOutputRequested));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Result.Export, _owner.RaiseExportRequested));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Menu.DocumentEdit, _owner.RaiseEditDocumentRequested));
        root.Add(CreateMenuItem(localizer, LocalizationKeys.Menu.DocumentDelete, _owner.RaiseDeleteDocumentRequested));
    }

    private MenuItem CreateMenuItem(ILocalizer localizer, string key, Action action)
    {
        var item = new MenuItem(localizer.Get(key), string.Empty, action);
        _menuLabels.Add((item, key));
        return item;
    }

    private void CopyAll() => _result.App?.Clipboard?.SetClipboardData(_result.Text ?? string.Empty);

    private void OpenOutput()
    {
        Model.Result.Mode = ResultViewMode.Raw;
        ShowResult();
        FocusResult();
    }

    private void CopySelectedDocument()
    {
        if (SelectedDocument() is { } document)
        {
            _result.App?.Clipboard?.SetClipboardData(BsonTextCodec.Format(document));
        }
    }
}
