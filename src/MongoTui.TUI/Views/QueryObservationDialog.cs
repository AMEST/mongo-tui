using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618
namespace MongoTui.TUI.Views;

public sealed class QueryObservationDialog : DiagnosticDialogBase
{
    public QueryObservationDialog(QueryObservation row, ILocalizer localizer, IApplication application)
        : base(localizer, application, "Diagnostics.Details")
    {
        var lines = new[]
        {
            $"{localizer.Get("Diagnostics.Timestamp")}: {row.StartedAt:O}",
            $"{localizer.Get("Diagnostics.Database")}: {row.Database}",
            $"{localizer.Get("Diagnostics.Collection")}: {row.Collection}",
            $"{localizer.Get("Diagnostics.Operation")}: {localizer.Get("Diagnostics.Operation." + row.Operation)}",
            $"{localizer.Get("Diagnostics.Source")}: {localizer.Get("Diagnostics.Source." + row.Source)}",
            $"{localizer.Get("Diagnostics.Process")}: {row.ProcessId}",
            $"{localizer.Get("Diagnostics.Duration")}: {row.Duration.TotalMilliseconds:0.0}",
            $"{localizer.Get("Diagnostics.Returned")}: {row.ReturnedCount?.ToString() ?? "—"}",
            $"{localizer.Get("Diagnostics.DocumentsExamined")}: {row.DocumentsExamined?.ToString() ?? "—"}",
            $"{localizer.Get("Diagnostics.KeysExamined")}: {row.KeysExamined?.ToString() ?? "—"}",
            $"{localizer.Get("Diagnostics.Plan")}: {row.PlanSummary ?? "—"}",
            $"{localizer.Get("Diagnostics.Failed")}: {!row.Success}; {localizer.Get("Diagnostics.ErrorCode")}: {row.ErrorCode?.ToString() ?? "—"}",
            $"{localizer.Get("Diagnostics.Timeouts")}: {row.TimedOut}; {localizer.Get("Diagnostics.Cancelled")}: {row.Cancelled}",
        };
        Add(new TextView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(3), ReadOnly = true, Text = string.Join('\n', lines) });
    }
}
