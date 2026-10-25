using MongoTui.Application.Localization;
using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Domain.Primitives;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog listing recent queries for a connection.</summary>
public sealed class HistoryDialog : ThemedDialog
{
    private readonly IQueryHistoryRepository _history;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly ConnectionId _connectionId;
    private readonly ListView _list = new();

    public HistoryDialog(
        IQueryHistoryRepository history,
        ILocalizer localizer,
        IApplication application,
        ConnectionId connectionId)
    {
        _history = history;
        _localizer = localizer;
        _application = application;
        _connectionId = connectionId;

        Title = localizer.Get(LocalizationKeys.History.Title);
        Width = 76;
        Height = 22;

        _list.X = 1;
        _list.Y = 1;
        _list.Width = Dim.Fill();
        _list.Height = Dim.Fill();
        Add(_list);

        var closeButton = new Button { Text = localizer.Get(LocalizationKeys.Common.Ok), IsDefault = true };
        closeButton.OnAccept(() => _application.RequestStop(this));
        this.AddButtons(closeButton);
    }

    public async Task LoadAsync()
    {
        var entries = await _history.GetRecentAsync(_connectionId, 100, CancellationToken.None);

        var items = entries.Count == 0
            ? new List<string> { _localizer.Get(LocalizationKeys.History.Empty) }
            : entries
                .Select(entry => $"{entry.RanAt:HH:mm:ss} {(entry.Success ? "+" : "!")} {entry.Script}")
                .ToList();

        _list.SetSource(new ObservableCollection<string>(items));
    }
}
