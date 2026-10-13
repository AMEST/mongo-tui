using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that lists, creates and drops collection indexes.</summary>
public sealed class IndexManagerDialog : ThemedDialog
{
    private readonly IndexService _indexes;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly MongoConnectionTarget _target;
    private readonly CollectionRef _collection;
    private readonly ListView _list = new();
    private readonly Label _stats = new();
    private readonly TextField _keysField = new();
    private readonly TextField _nameField = new();
    private readonly TextField _uniqueField = new();
    private readonly Label _error = new();

    private IReadOnlyList<IndexInfo> _indexInfos = [];

    public IndexManagerDialog(
        IndexService indexes,
        ILocalizer localizer,
        IApplication application,
        MongoConnectionTarget target,
        CollectionRef collection)
    {
        _indexes = indexes;
        _localizer = localizer;
        _application = application;
        _target = target;
        _collection = collection;

        Title = localizer.Get("Indexes.Title");
        Width = 72;
        Height = 22;

        _list.X = 1;
        _list.Y = 1;
        _list.Width = Dim.Fill();
        _list.Height = 6;
        Add(_list);

        _stats.X = 1;
        _stats.Y = 7;
        _stats.Width = Dim.Fill();
        Add(_stats);

        Add(FieldLabel("Indexes.Field.Keys", 9));
        _keysField.X = 20;
        _keysField.Y = 9;
        _keysField.Width = Dim.Fill();
        Add(_keysField);

        Add(FieldLabel("Indexes.Field.Name", 11));
        _nameField.X = 20;
        _nameField.Y = 11;
        _nameField.Width = Dim.Fill();
        Add(_nameField);

        Add(FieldLabel("Indexes.Field.Unique", 13));
        _uniqueField.X = 20;
        _uniqueField.Y = 13;
        _uniqueField.Width = 10;
        Add(_uniqueField);

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var refreshButton = new Button { Text = localizer.Get("Indexes.Refresh") };
        refreshButton.OnAcceptAsync(() => GuardedAsync(LoadAsync));
        var createButton = new Button { Text = localizer.Get("Indexes.Create") };
        createButton.OnAcceptAsync(() => GuardedAsync(CreateAsync));
        var dropButton = new Button { Text = localizer.Get("Indexes.Drop") };
        dropButton.OnAcceptAsync(() => GuardedAsync(DropAsync));
        var closeButton = new Button { Text = localizer.Get("Common.Ok"), IsDefault = true };
        closeButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(refreshButton, createButton, dropButton, closeButton);
    }

    public async Task LoadAsync()
    {
        _indexInfos = await _indexes.ListAsync(_target, _collection, CancellationToken.None);
        _list.SetSource(new ObservableCollection<string>(_indexInfos.Select(index => index.Name)));

        var stats = await _indexes.GetStatsAsync(_target, _collection, CancellationToken.None);
        _stats.Text =
            $"{_localizer.Get("Stats.Documents")}: {stats.DocumentCount}  " +
            $"{_localizer.Get("Stats.Indexes")}: {stats.IndexCount}";
    }

    private async Task CreateAsync()
    {
        var unique = _uniqueField.Text?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
        var name = string.IsNullOrWhiteSpace(_nameField.Text) ? null : _nameField.Text;

        await _indexes.CreateAsync(
            _target,
            _collection,
            _keysField.Text ?? "{}",
            name,
            unique,
            CancellationToken.None);

        await LoadAsync();
    }

    private async Task DropAsync()
    {
        if (_list.SelectedItem is not int index || index < 0 || index >= _indexInfos.Count)
        {
            return;
        }

        await _indexes.DropAsync(_target, _collection, _indexInfos[index].Name, CancellationToken.None);
        await LoadAsync();
    }

    private async Task GuardedAsync(Func<Task> action)
    {
        _error.Text = string.Empty;

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }

    private Label FieldLabel(string key, int y) => new() { Text = _localizer.Get(key), X = 1, Y = y };
}
