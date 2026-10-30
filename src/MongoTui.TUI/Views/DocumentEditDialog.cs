using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Exceptions;
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
    private readonly Button _saveButton;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _busy;
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
        Disposing += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); };

        _filter.TrackContents();
        _editor.TrackContents();

        Title = localizer.Get(LocalizationKeys.Documents.Title);
        Width = 76;
        Height = Dim.Func(_ => Math.Min(_application.Screen.Height,
            Math.Max(24, _application.Screen.Height * 9 / 10)), this);

        _filterFrame = new FrameView
        {
            Title = localizer.Get(LocalizationKeys.Documents.Filter),
            X = 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = 4,
        };
        _filter.X = 0;
        _filter.Y = 0;
        _filter.Width = Dim.Fill();
        _filter.Height = Dim.Fill();
        _filter.Text = _viewModel.Filter;
        _filterFrame.Add(_filter);
        Add(_filterFrame);

        _loadButton = new Button { Text = localizer.Get(LocalizationKeys.Documents.Load), X = 1, Y = Pos.Bottom(_filterFrame) };
        _loadButton.OnAcceptAsync(LoadAsync);
        Add(_loadButton);

        _documentFrame = new FrameView
        {
            Title = localizer.Get(LocalizationKeys.Documents.Document),
            X = 1,
            Y = Pos.Bottom(_loadButton),
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

        _saveButton = new Button { Text = localizer.Get(LocalizationKeys.Connections.Save), IsDefault = true, Enabled = viewModel.CanWrite };
        _saveButton.OnAcceptAsync(SaveAsync);
        _deleteButton = new Button { Text = localizer.Get(LocalizationKeys.Connections.Delete) };
        _deleteButton.OnAcceptAsync(DeleteAsync);
        var cancelButton = new Button { Text = localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(_saveButton, _deleteButton, cancelButton);

        if (_viewModel.IsNew)
        {
            _filterFrame.Visible = false;
            _loadButton.Visible = false;
            _deleteButton.Enabled = false;
            _documentFrame.Y = 0;
        }
    }

    public bool Saved { get; private set; }

    public bool Deleted { get; private set; }

    private Task LoadAsync()
    {
        if (_busy) return Task.CompletedTask;
        _viewModel.Filter = _filter.Text ?? string.Empty;
        return RunAsync(_viewModel.LoadAsync, () =>
        {
            _editor.Text = _viewModel.Json;
            _error.Text = string.Empty;
        });
    }

    private Task SaveAsync()
    {
        if (_busy) return Task.CompletedTask;
        _viewModel.Json = _editor.Text ?? string.Empty;
        return RunAsync(_viewModel.SaveAsync, () =>
        {
            Saved = true;
            _application.RequestStop(this);
        });
    }

    private Task DeleteAsync()
    {
        if (_busy) return Task.CompletedTask;
        _viewModel.Json = _editor.Text ?? string.Empty;
        return RunAsync(_viewModel.DeleteAsync, () =>
        {
            Deleted = true;
            _application.RequestStop(this);
        });
    }

    private async Task RunAsync(Func<CancellationToken, Task<bool>> action, Action onSuccess)
    {
        if (_busy) return;
        SetBusy(true);
        _error.Text = _localizer.Get(LocalizationKeys.Documents.Working);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(30));
        var succeeded = false;
        Exception? error = null;
        try { succeeded = await action(operation.Token).ConfigureAwait(false); }
        catch (Exception exception) { error = exception; }
        if (_lifetime.IsCancellationRequested) return;
        await _application.InvokeAsync(() =>
        {
            if (_lifetime.IsCancellationRequested) return;
            SetBusy(false);
            if (succeeded) onSuccess();
            else _error.Text = error is not null && error is not DomainException
                ? _localizer.Get(LocalizationKeys.Documents.OperationFailed)
                : error?.Message ?? ValidationMessage() ?? _localizer.Get(LocalizationKeys.Documents.NotFound);
        }).ConfigureAwait(false);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _loadButton.Enabled = _editor.Enabled = _filter.Enabled = !busy;
        _saveButton.Enabled = !busy && _viewModel.CanWrite;
        _deleteButton.Enabled = !busy && !_viewModel.IsNew && _viewModel.CanWrite;
    }
    private string? ValidationMessage() => _viewModel.ValidationErrorKey is { } key
        ? _localizer.Get(key) : _viewModel.ValidationError;
}
