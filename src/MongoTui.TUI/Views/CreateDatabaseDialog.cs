using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Localization;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Creates a database with its first empty collection, keeping errors and input in the dialog.</summary>
public sealed class CreateDatabaseDialog : ThemedDialog
{
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly Func<CollectionRef, CancellationToken, Task> _create;
    private readonly TextField _database = new() { X = 1, Y = 1, Width = Dim.Fill(1) };
    private readonly TextField _collection = new() { X = 1, Y = 4, Width = Dim.Fill(1) };
    private readonly Label _error = new() { X = 1, Y = 8, Width = Dim.Fill(1), Height = 3 };
    private readonly Button _submit;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _creating, _disposed;

    public CreateDatabaseDialog(ILocalizer localizer, IApplication application, Func<CollectionRef, CancellationToken, Task> create)
    {
        _application = application;
        _localizer = localizer;
        _create = create;
        Title = localizer.Get(LocalizationKeys.Create.DatabaseTitle);
        Width = 64;
        Height = 16;
        Add(new Label { Text = localizer.Get(LocalizationKeys.Create.DatabaseName), X = 1, Y = 0 }, _database);
        Add(new Label { Text = localizer.Get(LocalizationKeys.Create.FirstCollection), X = 1, Y = 3 }, _collection);
        Add(new Label { Text = localizer.Get(LocalizationKeys.Create.DatabaseHint), X = 1, Y = 6, Width = Dim.Fill(1), Height = 2 }, _error);
        _submit = new Button { Text = localizer.Get(LocalizationKeys.Create.Submit), IsDefault = true };
        _submit.OnAcceptAsync(CreateAsync);
        var cancel = new Button { Text = localizer.Get(LocalizationKeys.Common.Cancel) };
        cancel.OnAccept(() => { _lifetime.Cancel(); _application.RequestStop(this); });
        this.AddButtons(_submit, cancel);
        Disposing += (_, _) => { _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    public CollectionRef? CreatedCollection { get; private set; }

    private async Task CreateAsync()
    {
        if (_creating || _disposed) return;
        SetBusy(true);
        _error.Text = _localizer.Get(LocalizationKeys.Common.Working);
        CollectionRef? collection = null;
        Exception? error = null;
        try
        {
            collection = new CollectionRef(DatabaseName.Create((_database.Text ?? string.Empty).Trim()),
                CollectionName.Create((_collection.Text ?? string.Empty).Trim()));
            await _create(collection, _lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception exception) { error = exception; }
        if (_lifetime.IsCancellationRequested) return;
        await _application.InvokeAsync(() =>
        {
            if (_disposed || _lifetime.IsCancellationRequested) return;
            SetBusy(false);
            _error.Text = error?.Message ?? string.Empty;
            if (error is not null) return;
            CreatedCollection = collection;
            _application.RequestStop(this);
        }).ConfigureAwait(false);
    }

    private void SetBusy(bool busy)
    {
        _creating = busy;
        _database.Enabled = _collection.Enabled = _submit.Enabled = !busy;
    }
}
