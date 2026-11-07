using System.Data;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618
namespace MongoTui.TUI.Views;

public sealed class DiagnosticsDialog : DiagnosticDialogBase
{
    private readonly DiagnosticsServices _services;
    private readonly MongoConnectionTarget _target;
    private readonly IReadOnlyList<string> _databases;
    private readonly TextField _database = new(), _collection = new(), _minutes = new(), _minimum = new(), _limit = new();
    private QueryOperationKind? _operation;
    private readonly OptionSelector _source = new() { X = 1, Y = 5, Height = 1, Width = Dim.Fill(1), Orientation = Orientation.Horizontal };
    private readonly OptionSelector _page = new() { X = 1, Y = 1, Height = 1, Width = Dim.Fill(1), Orientation = Orientation.Horizontal };
    private IReadOnlyList<QueryObservation> _observations = [];
    private readonly TableView _table = new() { X = 1, Y = 7, Width = Dim.Fill(1), Height = Dim.Fill(5), Visible = false };
    private readonly TextView _output = new() { X = 1, Y = 7, Width = Dim.Fill(1), Height = Dim.Fill(5), ReadOnly = true };
    private readonly CheckBox _follow = new() { X = 1, Y = 6, Value = CheckState.Checked };
    private readonly CheckBox _slowOnly = new() { X = 38, Y = 6, Width = 34, Value = CheckState.Checked };
    private readonly object? _timer;
    public DiagnosticsDialog(DiagnosticsServices services, MongoConnectionTarget target, string database, IReadOnlyList<string> databases,
        ILocalizer localizer, IApplication application) : base(localizer, application, "Diagnostics.Title")
    {
        _services = services; _target = target; _databases = databases;
        _page.Labels = new[] { "Overview", "SlowQueries", "Database", "Server", "Profiler" }.Select(name => localizer.Get("Diagnostics." + name)).ToList();
        _page.Value = 0; _source.Labels = new[] { localizer.Get("Diagnostics.All") }.Concat(Enum.GetNames<QuerySource>().Select(name => localizer.Get("Diagnostics.Source." + name))).ToList(); _source.Value = 0;
        Field("Database", _database, database, 2, 1); Field("Collection", _collection, "", 2, 38);
        Field("Period", _minutes, "60", 3, 1); Field("Minimum", _minimum, "0", 4, 1);
        Field("RowLimit", _limit, "1000", 4, 38);
        var operation = new Button { X = 38, Y = 3, Width = 34, Text = localizer.Get("Diagnostics.Operation") + ": " + localizer.Get("Diagnostics.All") };
        operation.OnAccept(() =>
        {
            var labels = new[] { localizer.Get("Diagnostics.All") }.Concat(Enum.GetNames<QueryOperationKind>().Select(name => localizer.Get("Diagnostics.Operation." + name))).ToArray();
            using var picker = new ContextMenuDialog(localizer, application, labels, new System.Drawing.Point(1, 3));
            application.Run(picker); if (picker.SelectedIndex < 0) return;
            _operation = picker.SelectedIndex == 0 ? null : (QueryOperationKind)(picker.SelectedIndex - 1);
            operation.Text = localizer.Get("Diagnostics.Operation") + ": " + labels[picker.SelectedIndex];
        });
        Add(operation);
        _follow.Text = localizer.Get("Diagnostics.Follow"); _slowOnly.Text = localizer.Get("Diagnostics.SlowOnly");
        Add(_page, _source, _output, _follow, _slowOnly, _table);
        ActionButton("Diagnostics.Refresh", LoadAsync);
        ActionButton("Diagnostics.Settings", () => { using var dialog = new DiagnosticsSettingsDialog(services.Settings, localizer, application); application.Run(dialog); return Task.CompletedTask; });
        ActionButton("Diagnostics.Profiler", () =>
        {
            using var dialog = new ProfilerDialog(services, target, _database.Text, databases, localizer, application);
            RunLoaded(dialog, dialog.LoadAsync); return Task.CompletedTask;
        });
        var clearMemory = ActionButton("Diagnostics.ClearMemory", () => { services.Insights.ClearMemory(); return LoadAsync(); }, false);
        clearMemory.X = 1; clearMemory.Y = Pos.AnchorEnd(4);
        var clearHistory = ActionButton("Diagnostics.ClearHistory", ClearAsync, false);
        clearHistory.X = Pos.Right(clearMemory) + 1; clearHistory.Y = Pos.AnchorEnd(4);
        var groups = ActionButton("Diagnostics.Groups", () =>
        {
            using var dialog = new QueryInsightsDialog(services.Insights, _observations, localizer, application);
            application.Run(dialog); return Task.CompletedTask;
        }, false);
        groups.X = Pos.Right(clearHistory) + 1; groups.Y = Pos.AnchorEnd(4);
        _table.Activated += (_, _) =>
        {
            var index = _table.Value?.SelectedCell.Y ?? -1;
            if (index < 0 || index >= _observations.Count) return;
            using var dialog = new QueryObservationDialog(_observations[index], localizer, application); application.Run(dialog);
        };
        _timer = application.AddTimeout(TimeSpan.FromSeconds(2), () => { if (Closed) return false; if (_follow.Value == CheckState.Checked && !Busy) _ = LoadAsync(); return true; });
        Disposing += (_, _) => { if (_timer is not null) application.RemoveTimeout(_timer); };
    }
    private void Field(string key, TextField field, string value, int y, int x)
    {
        Add(new Label { X = x, Y = y, Text = Localizer.Get("Diagnostics." + key) });
        field.X = x + (x > 1 || key == "Database" ? 16 : 30); field.Y = y; field.Width = x > 1 ? 18 : 20; field.Text = value; Add(field);
    }
    private void RunLoaded(ThemedDialog dialog, Func<Task> load)
    {
        void Start(object? sender, Terminal.Gui.App.EventArgs<IApplication?> args) { UiApplication.Iteration -= Start; _ = load(); }
        UiApplication.Iteration += Start;
        try { UiApplication.Run(dialog); } finally { UiApplication.Iteration -= Start; }
    }
    public Task LoadAsync()
    {
        var database = _database.Text; var collection = _collection.Text; var page = _page.Value ?? 0;
        if (!int.TryParse(_minutes.Text, out var minutes) || minutes < 0 || minutes > 525600
            || !double.TryParse(_minimum.Text, out var minimum) || !double.IsFinite(minimum) || minimum < 0 || minimum > 86400000
            || !int.TryParse(_limit.Text, out var limit) || limit is < 1 or > 10000)
        { Feedback.Text = Localizer.Get("Diagnostics.Error.Command"); return Task.CompletedTask; }
        var filter = new QueryMetricsFilter { ConnectionId = _target.ConnectionId, Database = string.IsNullOrWhiteSpace(database) ? null : database,
            Collection = string.IsNullOrWhiteSpace(collection) ? null : collection, Source = _source.Value > 0 ? (QuerySource)(_source.Value.Value - 1) : null, Operation = _operation,
            Since = minutes == 0 ? null : DateTimeOffset.UtcNow.AddMinutes(-minutes), MinimumDuration = TimeSpan.FromMilliseconds(minimum),
            SlowOnly = page == 1 && _slowOnly.Value == CheckState.Checked };
        return RunAsync(async token =>
        {
            if (!string.IsNullOrWhiteSpace(database)) await _services.Server.RefreshProfilerStateAsync(_target, database, token).ConfigureAwait(false);
            if (page == 2) return DiagnosticsFormatter.Database(await _services.Server.GetDatabaseOverviewAsync(_target, database, token).ConfigureAwait(false), Localizer.Get);
            if (page == 3) return DiagnosticsFormatter.Server(await _services.Server.GetServerOverviewAsync(_target, token).ConfigureAwait(false), Localizer.Get);
            if (page == 4)
            {
                var state = await _services.Server.GetProfilerStateAsync(_target, database, token).ConfigureAwait(false);
                return $"{state.Database}: {state.Level} / {state.Options.SlowMs} ms / {state.Options.SampleRate}";
            }
            IReadOnlyList<QueryObservation> observations;
            if (filter.Source == QuerySource.ServerProfiler)
            {
                var rows = await _services.Server.ReadProfilerAsync(_target, database, filter.Since, filter.MinimumDuration ?? TimeSpan.Zero, limit, token).ConfigureAwait(false);
                observations = ProfilerObservations.Select(rows, _target.ConnectionId!, filter, _services.Settings.Current.SlowThresholdMs);
            }
            else observations = await _services.Insights.ReadAsync(filter, token).ConfigureAwait(false);
            _observations = observations.Take(limit).ToArray();
            return (page == 0 ? DiagnosticsFormatter.Overview(observations, Localizer.Get) : DiagnosticsFormatter.Observations(observations))
                + $"\n{Localizer.Get("Diagnostics.Lost")}: {_services.Queue.Lost}";
        }, text =>
        {
            _output.Text = text; _table.Visible = page == 1;
            _output.Visible = !_table.Visible;
            if (_table.Visible) FillTable();
        });
    }
    private void FillTable()
    {
        var table = new DataTable();
        foreach (var name in new[] { "Timestamp", "Database", "Collection", "Operation", "Source", "Duration", "Returned", "DocumentsExamined", "KeysExamined", "Plan" }) table.Columns.Add(Localizer.Get("Diagnostics." + name));
        foreach (var row in _observations) table.Rows.Add(row.StartedAt.ToString("HH:mm:ss.fff"), row.Database, row.Collection,
            Localizer.Get("Diagnostics.Operation." + row.Operation), Localizer.Get("Diagnostics.Source." + row.Source),
            row.Duration.TotalMilliseconds.ToString("0.0"), row.ReturnedCount?.ToString() ?? "—", row.DocumentsExamined?.ToString() ?? "—", row.KeysExamined?.ToString() ?? "—", row.PlanSummary ?? "—");
        _table.Table = new DataTableSource(table);
    }
    private Task ClearAsync()
    {
        using var confirmation = new ConfirmDialog(Localizer, UiApplication, Localizer.Get("Diagnostics.ClearHistory"), Localizer.Get("Diagnostics.ClearConfirm"));
        UiApplication.Run(confirmation); if (!confirmation.Confirmed) return Task.CompletedTask;
        return RunAsync(async token => { await _services.Insights.ClearHistoryAsync(_target.ConnectionId, token).ConfigureAwait(false); return true; }, _ => _output.Text = "");
    }
}
