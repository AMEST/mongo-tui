using MongoTui.Application.Abstractions.Localization;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that inserts, edits or deletes a document.</summary>
public sealed class DocumentEditDialog : ThemedDialog
{
    private readonly DocumentEditViewModel _viewModel;
    private readonly IApplication _application;
#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M7.
    private readonly TextView _editor = new() { Multiline = true };
#pragma warning restore CS0618
    private readonly Label _error = new();

    public DocumentEditDialog(ILocalizer localizer, IApplication application, DocumentEditViewModel viewModel)
    {
        _viewModel = viewModel;
        _application = application;

        Title = localizer.Get("Documents.Title");
        Width = 72;
        Height = 22;

        _editor.X = 0;
        _editor.Y = 0;
        _editor.Width = Dim.Fill();
        _editor.Height = Dim.Fill();
        _editor.Text = _viewModel.Json;
        Add(_editor);

        _error.X = 0;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var saveButton = new Button { Text = localizer.Get("Connections.Save"), IsDefault = true };
        saveButton.OnAcceptAsync(SaveAsync);
        var deleteButton = new Button { Text = localizer.Get("Connections.Delete") };
        deleteButton.OnAcceptAsync(DeleteAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        AddButton(saveButton);
        AddButton(deleteButton);
        AddButton(cancelButton);

        if (_viewModel.IsNew)
        {
            deleteButton.Enabled = false;
        }
    }

    public bool Saved { get; private set; }

    public bool Deleted { get; private set; }

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
