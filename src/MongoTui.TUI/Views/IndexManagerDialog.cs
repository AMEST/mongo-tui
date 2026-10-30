using MongoTui.Application.Localization;
using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
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
    private readonly Label _details = new() { X = 1, Y = 7, Width = Dim.Fill(1), Height = 2 };
    private readonly TextField _keysField = new();
    private readonly TextField _nameField = new();
    private readonly CheckBox _uniqueCheck = new();
    private readonly CheckBox _backgroundCheck = new();
    private readonly Label _error = new();
    private readonly Button _dropButton = new();
    private readonly Button _createButton = new();
    private readonly Button _refreshButton = new();
    private readonly CancellationTokenSource _lifetime = new();
    private bool _busy, _disposed;

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

        Title = localizer.Get(LocalizationKeys.Indexes.Title);
        Width = Dim.Percent(90);
        Height = 22;

        BuildList();
        BuildFields();
        AddActions();
        ShowSelection();
        Disposing += (_, _) => { _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    private void BuildList()
    {
        _list.X = 1;
        _list.Y = 1;
        _list.Width = Dim.Fill();
        _list.Height = 5;
        _list.ValueChanged += (_, _) => ShowSelection();
        Add(_list);

        _stats.X = 1;
        _stats.Y = 6;
        _stats.Width = Dim.Fill();
        Add(_stats, _details);

    }

    private void BuildFields()
    {
        AddField(_keysField, LocalizationKeys.Indexes.FieldKeys, 9);
        AddField(_nameField, LocalizationKeys.Indexes.FieldName, 11);
        _uniqueCheck.Text = _localizer.Get(LocalizationKeys.Indexes.FieldUnique);
        _uniqueCheck.X = 1;
        _uniqueCheck.Y = 13;
        _backgroundCheck.Text = _localizer.Get(LocalizationKeys.Indexes.FieldBackground);
        _backgroundCheck.X = 1;
        _backgroundCheck.Y = 15;
        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_uniqueCheck, _backgroundCheck, _error);
    }

    private void AddField(TextField field, string key, int y)
    {
        Add(FieldLabel(key, y));
        field.X = 20;
        field.Y = y;
        field.Width = Dim.Fill();
        Add(field);
    }

    private void AddActions()
    {
        _refreshButton.Text = _localizer.Get(LocalizationKeys.Indexes.Refresh);
        _refreshButton.OnAcceptAsync(LoadAsync);
        _createButton.Text = _localizer.Get(LocalizationKeys.Indexes.Create);
        _createButton.OnAcceptAsync(CreateAsync);
        _dropButton.Text = _localizer.Get(LocalizationKeys.Indexes.Drop);
        _dropButton.OnAcceptAsync(DropAsync);
        var closeButton = new Button { Text = _localizer.Get(LocalizationKeys.Common.Ok), IsDefault = true };
        closeButton.OnAccept(() => { _lifetime.Cancel(); _application.RequestStop(this); });

        this.AddButtons(_refreshButton, _createButton, _dropButton, closeButton);
    }

    public Task LoadAsync() => RunAsync(_ => Task.CompletedTask);

    private Task CreateAsync()
    {
        if (_busy) return Task.CompletedTask;
        var name = string.IsNullOrWhiteSpace(_nameField.Text) ? null : _nameField.Text;
        var keys = _keysField.Text ?? "{}";
        var unique = _uniqueCheck.Value == CheckState.Checked;
        var background = _backgroundCheck.Value == CheckState.Checked;
        return RunAsync(token => _indexes.CreateAsync(_target, _collection, keys, name, unique, background, token));
    }

    private Task DropAsync()
    {
        if (_busy || SelectedIndex() is not { } index || index.Name == MongoSystemNames.IdIndex)
            return Task.CompletedTask;
        using var confirm = new ConfirmDialog(_localizer, _application,
            _localizer.Get(LocalizationKeys.Indexes.Drop), _localizer.Format(LocalizationKeys.Index.DeleteHint, index.Name));
        _application.Run(confirm);
        return confirm.Confirmed ? RunAsync(token => _indexes.DropAsync(_target, _collection, index.Name, token))
            : Task.CompletedTask;
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_busy || _disposed || _lifetime.IsCancellationRequested) return;
        SetBusy(true);
        _error.Text = _localizer.Get(LocalizationKeys.Common.Working);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(30));
        IReadOnlyList<IndexInfo>? indexes = null;
        CollectionStats? stats = null;
        Exception? error = null;
        try
        {
            await action(operation.Token).ConfigureAwait(false);
            indexes = await _indexes.ListAsync(_target, _collection, operation.Token).ConfigureAwait(false);
            stats = await _indexes.GetStatsAsync(_target, _collection, operation.Token).ConfigureAwait(false);
        }
        catch (Exception exception) { error = exception; }
        if (_disposed || _lifetime.IsCancellationRequested) return;
        await _application.InvokeAsync(() =>
        {
            if (_disposed || _lifetime.IsCancellationRequested) return;
            if (error is null)
            {
                var selected = SelectedIndex()?.Name;
                _indexInfos = indexes!;
                _list.SetSource(new ObservableCollection<string>(_indexInfos.Select(Describe)));
                var position = _indexInfos.ToList().FindIndex(index => index.Name == selected);
                _list.SelectedItem = _indexInfos.Count > 0 ? Math.Max(0, position) : null;
                _stats.Text = $"{_localizer.Get(LocalizationKeys.Stats.Documents)}: {stats!.DocumentCount}  " +
                    $"{_localizer.Get(LocalizationKeys.Stats.Indexes)}: {stats.IndexCount}";
            }
            _error.Text = error?.Message ?? string.Empty;
            SetBusy(false);
        }).ConfigureAwait(false);
    }

    private IndexInfo? SelectedIndex() => _list.SelectedItem is int index && index >= 0 && index < _indexInfos.Count
        ? _indexInfos[index] : null;

    private string Describe(IndexInfo index) => $"{index.Name}  [{string.Join(", ", index.Fields.Select(field =>
        $"{field.Path}: {Direction(field.Direction)}"))}]" + (index.Unique ? $"  {_localizer.Get(LocalizationKeys.Index.UniqueBadge)}" : "");

    private static string Direction(IndexDirection direction) => direction switch
    {
        IndexDirection.Ascending => "1", IndexDirection.Descending => "-1", IndexDirection.Hashed => "hashed",
        IndexDirection.Text => "text", IndexDirection.Geo2d => "2d", IndexDirection.Geo2dSphere => "2dsphere",
        _ => direction.ToString(),
    };

    private void ShowSelection()
    {
        _details.Text = SelectedIndex() is { } index ? Describe(index) : string.Empty;
        _dropButton.Enabled = !_busy && SelectedIndex() is { Name: not MongoSystemNames.IdIndex };
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _list.Enabled = _refreshButton.Enabled = _createButton.Enabled = _keysField.Enabled = _nameField.Enabled = !busy;
        _uniqueCheck.Enabled = _backgroundCheck.Enabled = !busy;
        ShowSelection();
    }

    private Label FieldLabel(string key, int y) => new() { Text = _localizer.Get(key), X = 1, Y = y };
}
