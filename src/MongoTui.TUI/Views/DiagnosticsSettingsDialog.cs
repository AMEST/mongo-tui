using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed class DiagnosticsSettingsDialog : DiagnosticDialogBase
{
    private readonly DiagnosticsSettingsService _settings;
    private readonly Dictionary<string, TextField> _numbers = new();
    private readonly Dictionary<string, CheckBox> _checks = new();
    public DiagnosticsSettingsDialog(DiagnosticsSettingsService settings, ILocalizer localizer, IApplication application)
        : base(localizer, application, "Diagnostics.Settings")
    {
        _settings = settings; var value = settings.Current; var y = 1;
        Check("MetricsEnabled", value.MetricsEnabled, ref y); Check("LogSlow", value.LogSlowQueries, ref y);
        Check("Persist", value.PersistMetrics, ref y); Check("Mirror", value.MirrorServerSlowToApplicationLog, ref y);
        Number("Threshold", value.SlowThresholdMs, ref y); Number("MemoryRetention", value.MemoryRetentionMinutes, ref y);
        Number("Retention", value.PersistentRetentionDays, ref y); Number("MaximumRows", value.MaximumPersistentRows, ref y);
        ActionButton("Common.Save", SaveAsync);
    }
    private void Check(string name, bool value, ref int y)
    {
        var control = new CheckBox { Text = Localizer.Get("Diagnostics." + name), X = 1, Y = y++, Value = value ? CheckState.Checked : CheckState.UnChecked };
        _checks[name] = control; Add(control);
    }
    private void Number(string name, int value, ref int y)
    {
        Add(new Label { X = 1, Y = y, Text = Localizer.Get("Diagnostics." + name) });
        var control = new TextField { X = 42, Y = y++, Width = 15, Text = value.ToString() };
        _numbers[name] = control; Add(control);
    }
    private Task SaveAsync()
    {
        if (_numbers.Values.Any(control => !int.TryParse(control.Text, out _))) { Feedback.Text = Localizer.Get("Diagnostics.Error.Command"); return Task.CompletedTask; }
        bool Check(string name) => _checks[name].Value == CheckState.Checked;
        int Number(string name) => int.Parse(_numbers[name].Text);
        var value = new ClientDiagnosticsSettings { MetricsEnabled = Check("MetricsEnabled"), LogSlowQueries = Check("LogSlow"),
            PersistMetrics = Check("Persist"), MirrorServerSlowToApplicationLog = Check("Mirror"), SlowThresholdMs = Number("Threshold"),
            MemoryRetentionMinutes = Number("MemoryRetention"), PersistentRetentionDays = Number("Retention"), MaximumPersistentRows = Number("MaximumRows") };
        return RunAsync(async token => { await _settings.SaveAsync(value, token).ConfigureAwait(false); return true; }, _ => UiApplication.RequestStop(this));
    }
}
