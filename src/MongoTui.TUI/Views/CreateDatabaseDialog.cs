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
    private readonly Func<CollectionRef, Task> _create;
    private readonly TextField _database = new() { X = 1, Y = 1, Width = Dim.Fill(1) };
    private readonly TextField _collection = new() { X = 1, Y = 4, Width = Dim.Fill(1) };
    private readonly Label _error = new() { X = 1, Y = 8, Width = Dim.Fill(1), Height = 3 };
    private bool _creating;

    public CreateDatabaseDialog(ILocalizer localizer, IApplication application, Func<CollectionRef, Task> create)
    {
        _application = application;
        _create = create;
        Title = localizer.Get(LocalizationKeys.Create.DatabaseTitle);
        Width = 64;
        Height = 16;
        Add(new Label { Text = localizer.Get(LocalizationKeys.Create.DatabaseName), X = 1, Y = 0 }, _database);
        Add(new Label { Text = localizer.Get(LocalizationKeys.Create.FirstCollection), X = 1, Y = 3 }, _collection);
        Add(new Label { Text = localizer.Get(LocalizationKeys.Create.DatabaseHint), X = 1, Y = 6, Width = Dim.Fill(1), Height = 2 }, _error);
        var submit = new Button { Text = localizer.Get(LocalizationKeys.Create.Submit), IsDefault = true };
        submit.OnAcceptAsync(CreateAsync);
        var cancel = new Button { Text = localizer.Get(LocalizationKeys.Common.Cancel) };
        cancel.OnAccept(() => { if (!_creating) _application.RequestStop(this); });
        this.AddButtons(submit, cancel);
    }

    public CollectionRef? CreatedCollection { get; private set; }

    private async Task CreateAsync()
    {
        if (_creating) return;
        _creating = true;
        _error.Text = string.Empty;
        try
        {
            var collection = new CollectionRef(DatabaseName.Create((_database.Text ?? string.Empty).Trim()),
                CollectionName.Create((_collection.Text ?? string.Empty).Trim()));
            await _create(collection);
            CreatedCollection = collection;
            _application.RequestStop(this);
        }
        catch (Exception exception) { _error.Text = exception.Message; }
        finally { _creating = false; }
    }
}
