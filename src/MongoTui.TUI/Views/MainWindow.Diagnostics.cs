#pragma warning disable CS0618
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private readonly DiagnosticsServices? _diagnostics;
    private void InitializeProfilerIndicator()
    {
        var indicator = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Visible = false };
        Add(indicator);
        Initialized += (_, _) =>
        {
            if (Application is not { } app || _diagnostics is null) return;
            var timer = app.AddTimeout(TimeSpan.FromSeconds(1), () =>
            {
                var states = _tree.Connections.SelectMany(connection => _diagnostics.Server.States(connection.Target))
                    .Where(state => state.Level != MongoTui.Domain.Diagnostics.ProfilingLevel.Off).ToArray();
                indicator.Visible = states.Length > 0;
                indicator.Text = _localizer.Get("Diagnostics.ProfilerOn") + " " + string.Join(", ", states.Select(state => $"{state.Database}: {state.Level}, {state.Options.SlowMs} ms"));
                return true;
            });
            Disposing += (_, _) => { if (timer is not null) app.RemoveTimeout(timer); };
        };
    }
    private Task OpenDiagnosticsAsync()
    {
        if (_queryTabs.ActiveTab is { } tab)
            return OpenDiagnosticsAsync(tab.Target, tab.ContextDatabase, new[] { tab.ContextDatabase });
        if (SelectedRow() is { Owner: { } connection } row)
            return OpenDiagnosticsAsync(connection.Target, row.Database ?? "admin", connection.Databases.Select(db => db.Name).ToArray());
        return Task.CompletedTask;
    }
    private Task OpenDiagnosticsAsync(MongoConnectionTarget target, string database, IReadOnlyList<string> databases)
    {
        if (_diagnostics is null || Application is not { } app) return Task.CompletedTask;
        using var dialog = new DiagnosticsDialog(_diagnostics, target, database, databases, _localizer, app);
        RunDiagnosticsDialog(dialog, dialog.LoadAsync);
        var active = _diagnostics.Server.States(target).Where(state => state.Level != MongoTui.Domain.Diagnostics.ProfilingLevel.Off);
        SetStatus(string.Join(", ", active.Select(state => _localizer.Get("Diagnostics.ProfilerOn") + $" {state.Database}: {state.Level}")));
        return Task.CompletedTask;
    }
    private Task OpenProfilerAsync(MongoConnectionTarget target, string database, IReadOnlyList<string> databases)
    {
        if (_diagnostics is null || Application is not { } app) return Task.CompletedTask;
        using var dialog = new ProfilerDialog(_diagnostics, target, database, databases, _localizer, app);
        RunDiagnosticsDialog(dialog, dialog.LoadAsync); return Task.CompletedTask;
    }
    private Task OpenSampleExplainAsync(MongoTui.TUI.ViewModels.ActiveConnection connection, string database, string collection)
    {
        if (_diagnostics is null || Application is not { } app) return Task.CompletedTask;
        var context = new QueryExecutionContext { TabId = Guid.NewGuid(), ConnectionId = connection.Profile.Id, Target = connection.Target,
            Database = database, Collection = collection,
            Script = "db.getCollection(" + System.Text.Json.JsonSerializer.Serialize(collection) + ").find({}).limit(50)" };
        using var dialog = new ExplainDialog(_diagnostics.Explain, context, _localizer, app);
        RunDiagnosticsDialog(dialog, dialog.LoadAsync); return Task.CompletedTask;
    }
    private Task OpenExplainAsync()
    {
        if (_diagnostics is null || Application is not { } app || _queryTabs.ActiveTab is not { } tab) return Task.CompletedTask;
        var editor = _queryTabs.ActiveEditorView;
        var script = editor is { SelectedLength: > 0 } ? editor.SelectedText : editor?.Text ?? tab.Script;
        var context = new QueryExecutionContext { TabId = tab.Id, ConnectionId = tab.ConnectionId, Target = tab.Target,
            Database = tab.ContextDatabase, Collection = tab.Collection, Script = script };
        using var dialog = new ExplainDialog(_diagnostics.Explain, context, _localizer, app);
        RunDiagnosticsDialog(dialog, dialog.LoadAsync); return Task.CompletedTask;
    }
    private void RunDiagnosticsDialog(ThemedDialog dialog, Func<Task> load)
    {
        if (Application is not { } app) return;
        void Start(object? sender, Terminal.Gui.App.EventArgs<IApplication?> args) { app.Iteration -= Start; _ = load(); }
        app.Iteration += Start;
        try { app.Run(dialog); } finally { app.Iteration -= Start; }
    }
}
