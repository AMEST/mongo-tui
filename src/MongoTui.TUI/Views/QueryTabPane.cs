using MongoDB.Bson;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Views;

internal sealed class QueryTabPane
{
    private readonly QueryTabsView _owner;
    private readonly ResultTextView _result;
    private readonly FrameView _editorFrame;
    private readonly FrameView _resultFrame;
    private bool _menuBuilt;

    public QueryTabPane(QueryTabsView owner, QueryTabViewModel model, ILocalizer localizer, AutocompleteService autocomplete)
    {
        _owner = owner;
        Model = model;
        Container = new View { Title = model.Title, Width = Dim.Fill(), Height = Dim.Fill() };

        _editorFrame = new FrameView
        {
            Title = localizer.Get("Query.Pane"),
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
        };

        Editor = new QueryEditorView(autocomplete, model) { Width = Dim.Fill(), Height = Dim.Fill() };
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

    public QueryEditorView Editor { get; }

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
