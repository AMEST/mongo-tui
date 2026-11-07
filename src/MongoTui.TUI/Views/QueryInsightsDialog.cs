using System.Data;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed class QueryInsightsDialog : DiagnosticDialogBase
{
    private readonly TableView _table = new() { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(3), FullRowSelect = true };
    private readonly IReadOnlyList<QueryObservation> _observations;
    private readonly IReadOnlyList<QueryMetricGroup> _groups;
    private IReadOnlyList<QueryObservation>? _selectedRows;

    public QueryInsightsDialog(QueryInsightsService service, IReadOnlyList<QueryObservation> observations,
        ILocalizer localizer, IApplication application) : base(localizer, application, "Diagnostics.Groups")
    {
        _observations = observations; _groups = service.Group(observations);
        Add(_table); ShowGroups();
        _table.Activated += (_, _) => OpenSelected();
        ActionButton("Diagnostics.Details", () => { OpenSelected(); return Task.CompletedTask; });
        ActionButton("Diagnostics.Groups", () => { ShowGroups(); return Task.CompletedTask; });
    }

    private void ShowGroups()
    {
        _selectedRows = null;
        var table = Table("Database", "Collection", "Operation", "Source", "Count", "ErrorPercent", "Average", "P95", "Slow");
        foreach (var group in _groups) table.Rows.Add(group.Database, group.Collection, Localizer.Get("Diagnostics.Operation." + group.Operation),
            Localizer.Get("Diagnostics.Source." + group.Source), group.Count, (100d * group.Failed / group.Count).ToString("0.0"),
            group.AverageDurationMs.ToString("0.0"), group.P95Ms.ToString("0.0"), group.Slow);
        _table.Table = new DataTableSource(table);
    }

    private DataTable Table(params string[] columns)
    {
        var table = new DataTable();
        foreach (var column in columns) table.Columns.Add(Localizer.Get("Diagnostics." + column));
        return table;
    }

    private void OpenSelected()
    {
        var index = _table.Value?.SelectedCell.Y ?? -1;
        if (_selectedRows is { } rows)
        {
            if (index < 0 || index >= rows.Count) return;
            using var dialog = new QueryObservationDialog(rows[index], Localizer, UiApplication);
            UiApplication.Run(dialog); return;
        }
        if (index < 0 || index >= _groups.Count) return;
        var group = _groups[index];
        _selectedRows = _observations.Where(row => row.Database == group.Database && row.Collection == group.Collection
            && row.Operation == group.Operation && row.Source == group.Source).OrderByDescending(row => row.StartedAt).ToArray();
        var table = Table("Timestamp", "Duration", "Returned", "DocumentsExamined", "KeysExamined", "Plan", "Source");
        foreach (var row in _selectedRows) table.Rows.Add(row.StartedAt.ToString("HH:mm:ss.fff"), row.Duration.TotalMilliseconds.ToString("0.0"),
            row.ReturnedCount, row.DocumentsExamined, row.KeysExamined, row.PlanSummary, Localizer.Get("Diagnostics.Source." + row.Source));
        _table.Table = new DataTableSource(table);
    }
}
