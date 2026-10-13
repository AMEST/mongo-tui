using MongoTui.Application.Abstractions.Localization;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>
/// Modal dialog that inserts, edits or deletes a document. In edit mode a find filter selects the
/// document to load.
/// </summary>
public sealed class DocumentEditDialog : ThemedDialog
{
    private readonly DocumentEditViewModel _viewModel;
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly FrameView _filterFrame;
    private readonly FrameView _documentFrame;
    private readonly Button _loadButton;
    private readonly Button _deleteButton;
#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M7.
    private readonly TextView _filter = new() { Multiline = true };
    private readonly TextView _editor = new() { Multiline = true };
#pragma warning restore CS0618
    private readonly Label _error = new();

    public DocumentEditDialog(ILocalizer localizer, IApplication application, DocumentEditViewModel viewModel)
    {
        _viewModel = viewModel;
        _application = application;
        _localizer = localizer;

        Title = localizer.Get("Documents.Title");
        Width = 76;
        Height = 24;

        _filterFrame = new FrameView
        {
            Title = localizer.Get("Documents.Filter"),
            X = 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = 6,
        };
        _filter.X = 0;
        _filter.Y = 0;
        _filter.Width = Dim.Fill();
        _filter.Height = Dim.Fill();
        _filter.Text = _viewModel.Filter;
        _filterFrame.Add(_filter);
        Add(_filterFrame);

        _loadButton = new Button { Text = localizer.Get("Documents.Load"), X = 1, Y = Pos.Bottom(_filterFrame) };
        _loadButton.OnAcceptAsync(LoadAsync);
        Add(_loadButton);

        _documentFrame = new FrameView
        {
            Title = localizer.Get("Documents.Document"),
            X = 1,
            Y = 8,
            Width = Dim.Fill(),
            Height = Dim.Fill(4),
        };
        _editor.X = 0;
        _editor.Y = 0;
        _editor.Width = Dim.Fill();
        _editor.Height = Dim.Fill();
        _editor.Text = _viewModel.Json;
        _documentFrame.Add(_editor);
        Add(_documentFrame);

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var saveButton = new Button { Text = localizer.Get("Connections.Save"), IsDefault = true };
        saveButton.OnAcceptAsync(SaveAsync);
        _deleteButton = new Button { Text = localizer.Get("Connections.Delete") };
        _deleteButton.OnAcceptAsync(DeleteAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        AddButton(saveButton);
        AddButton(_deleteButton);
        AddButton(cancelButton);

        if (_viewModel.IsNew)
        {
            _filterFrame.Visible = false;
            _loadButton.Visible = false;
            _deleteButton.Enabled = false;
            _documentFrame.Y = 1;
        }
    }

    public bool Saved { get; private set; }

    public bool Deleted { get; private set; }

    private async Task LoadAsync()
    {
        _viewModel.Filter = _filter.Text ?? string.Empty;

        if (await _viewModel.LoadAsync(CancellationToken.None))
        {
            _editor.Text = _viewModel.Json;
            _error.Text = string.Empty;
        }
        else
        {
            _error.Text = _viewModel.ValidationError ?? _localizer.Get("Documents.NotFound");
        }
    }

    private async Task SaveAsync()
    {
        _viewModel.Json = _editor.Text ?? string.Empty;

        if (await _viewModel.SaveAsync(CancellationToken.None))
        {
            Saved = true;
            _application.RequestStop(this);
        }
        else
        {
            _error.Text = _viewModel.ValidationError ?? string.Empty;
        }
    }

    private async Task DeleteAsync()
    {
        _viewModel.Json = _editor.Text ?? string.Empty;

        if (await _viewModel.DeleteAsync(CancellationToken.None))
        {
            Deleted = true;
            _application.RequestStop(this);
        }
        else
        {
            _error.Text = _viewModel.ValidationError ?? string.Empty;
        }
    }
}
