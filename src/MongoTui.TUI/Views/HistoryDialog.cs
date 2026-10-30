using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Browses recent queries across saved connections and selects a query to reopen.</summary>
public sealed class HistoryDialog : ThemedDialog
{
    private readonly IQueryHistoryRepository _history;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly IReadOnlyList<ConnectionProfile> _profiles;
    private readonly string[] _profileLabels;
    private readonly DropDownList _connection = new() { X = 1, Y = 2, Width = Dim.Fill(1), ReadOnly = true };
    private readonly ListView _list = new() { X = 1, Y = 4, Width = Dim.Fill(1), Height = Dim.Percent(40) };
    private readonly SyntaxTextView _details = new() { ReadOnly = true, Multiline = true, Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly Label _status = new() { X = 1, Y = Pos.AnchorEnd(3), Width = Dim.Fill(1) };
    private readonly Button _open = new();
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _loadCancellation;
    private IReadOnlyList<QueryHistoryEntry> _entries = [];
    private bool _disposed, _loading;

    public HistoryDialog(IQueryHistoryRepository history, ILocalizer localizer, IApplication application,
        IReadOnlyList<ConnectionProfile> profiles, ConnectionId? selectedConnection = null)
    {
        _history = history; _localizer = localizer; _application = application; _profiles = profiles;
        _profileLabels = profiles.Select((profile, index) => $"{index + 1}. {profile.Name}").ToArray();
        Title = localizer.Get(LocalizationKeys.History.Title);
        Width = Dim.Percent(90);
        Height = Dim.Percent(90);
        Add(new Label { Text = localizer.Get(LocalizationKeys.History.Select), X = 1, Y = 0 });
        _connection.Source = new ListWrapper<string>(new ObservableCollection<string>(_profileLabels));
        var selected = profiles.ToList().FindIndex(profile => profile.Id == selectedConnection);
        _connection.Value = _profileLabels.ElementAtOrDefault(Math.Max(0, selected)) ?? string.Empty;
        _connection.Enabled = profiles.Count > 0;
        _connection.ValueChanged += (_, _) => _ = LoadAsync();
        _list.ValueChanged += (_, _) => ShowSelection();
        _list.Accepting += (_, args) => { args.Handled = true; Open(); };
        var frame = new FrameView { Title = localizer.Get(LocalizationKeys.Query.Script), X = 1,
            Y = Pos.Bottom(_list), Width = Dim.Fill(1), Height = Dim.Fill(1) };
        frame.Add(_details);
        Add(_connection, _list, frame, _status);
        AddActions();
        Disposing += (_, _) => { _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    public QueryHistoryEntry? SelectedEntry { get; private set; }

    public async Task LoadAsync()
    {
        if (_disposed || _lifetime.IsCancellationRequested) return;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        var index = Array.IndexOf(_profileLabels, _connection.Value);
        if (index < 0) { _loading = false; ApplyEntries([]); return; }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(30));
        _loadCancellation = operation;
        _loading = true;
        ApplyEntries([]);
        _status.Text = _localizer.Get(LocalizationKeys.Common.Loading);
        IReadOnlyList<QueryHistoryEntry>? entries = null;
        try { entries = await _history.GetRecentAsync(_profiles[index].Id, 100, operation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || operation != _loadCancellation) { return; }
        catch { }
        if (_disposed || _lifetime.IsCancellationRequested || operation != _loadCancellation) return;
        await _application.InvokeAsync(() =>
        {
            if (_disposed || _lifetime.IsCancellationRequested || operation != _loadCancellation) return;
            _loadCancellation = null;
            _loading = false;
            ApplyEntries(entries ?? []);
            _status.Text = entries is null ? _localizer.Get(LocalizationKeys.History.LoadError)
                : entries.Count == 0 ? _localizer.Get(LocalizationKeys.History.Empty) : string.Empty;
        }).ConfigureAwait(false);
    }

    private void AddActions()
    {
        _open.Text = _localizer.Get(LocalizationKeys.History.Open);
        _open.IsDefault = true;
        _open.Enabled = false;
        _open.OnAccept(Open);
        var refresh = new Button { Text = _localizer.Get(LocalizationKeys.Common.Refresh) };
        refresh.OnAcceptAsync(LoadAsync);
        var close = new Button { Text = _localizer.Get(LocalizationKeys.Common.Close) };
        close.OnAccept(() => { _lifetime.Cancel(); _application.RequestStop(this); });
        this.AddButtons(_open, refresh, close);
    }

    private void ApplyEntries(IReadOnlyList<QueryHistoryEntry> entries)
    {
        _entries = entries;
        _list.SetSource(new ObservableCollection<string>(entries.Select(entry =>
            _localizer.Format(LocalizationKeys.History.Entry, entry.RanAt.ToLocalTime(),
                _localizer.Get(entry.Success ? LocalizationKeys.History.Success : LocalizationKeys.History.Failed),
                entry.Database ?? MongoSystemNames.DefaultDatabase,
                entry.Collection is null ? string.Empty : $" / {entry.Collection}", entry.ElapsedMs ?? 0)
            + $" | {entry.Script.ReplaceLineEndings(" ")}")));
        _list.SelectedItem = entries.Count > 0 ? 0 : null;
        ShowSelection();
    }

    private QueryHistoryEntry? CurrentEntry() => _list.SelectedItem is int index && index >= 0 && index < _entries.Count
        ? _entries[index] : null;

    private void ShowSelection()
    {
        _details.Text = CurrentEntry()?.Script ?? string.Empty;
        _open.Enabled = !_loading && CurrentEntry() is not null;
    }

    private void Open()
    {
        if (_loading || CurrentEntry() is not { } entry) return;
        SelectedEntry = entry;
        _lifetime.Cancel();
        _application.RequestStop(this);
    }
}
