using System.Globalization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed class ProfilerDialog : DiagnosticDialogBase
{
    private readonly DiagnosticsServices _services;
    private readonly MongoConnectionTarget _target;
    private readonly OptionSelector _mode = new() { X = 1, Y = 1, Height = 1, Width = Dim.Fill(1), Orientation = Orientation.Horizontal };
    private readonly OptionSelector _filterAction = new() { X = 1, Y = 6, Height = 1, Width = Dim.Fill(1), Orientation = Orientation.Horizontal };
    private readonly TextField _scope = new(), _threshold = new(), _sample = new(), _filter = new(), _minutes = new();
    private readonly CheckBox _restore = new() { X = 1, Y = 8 };
    private readonly CheckBox _mirror = new() { X = 1, Y = 9, Width = Dim.Fill(1) };
    private readonly Label _actual = new() { X = 1, Y = 12, Height = Dim.Fill(4), Width = Dim.Fill(1) };
    private ProfilerPreferences? _preferences;
    private bool _initialized, _hydrating;
    public ProfilerDialog(DiagnosticsServices services, MongoConnectionTarget target, string database, IReadOnlyList<string> databases,
        ILocalizer localizer, IApplication application) : base(localizer, application, "Diagnostics.Profiler")
    {
        _services = services; _target = target;
        _mode.Labels = new[] { "Off", "Client", "ServerSlow", "ServerAll" }.Select(name => localizer.Get("Diagnostics." + name)).ToList();
        _filterAction.Labels = new[] { "FilterPreserve", "FilterReplace", "FilterClear" }.Select(name => localizer.Get("Diagnostics." + name)).ToList();
        _mode.Value = 0; _filterAction.Value = 0; Add(_mode, _filterAction);
        Field("Scope", _scope, database, 2); Field("Threshold", _threshold, "100", 3); Field("SampleRate", _sample, "1", 4);
        Field("Filter", _filter, "", 5); Field("AutoDisable", _minutes, "0", 7);
        _mode.ValueChanged += (_, _) => ModeChanged();
        _restore.Text = localizer.Get("Diagnostics.Restore"); _mirror.Text = localizer.Get("Diagnostics.Mirror");
        _mirror.Value = services.Settings.Current.MirrorServerSlowToApplicationLog ? CheckState.Checked : CheckState.UnChecked;
        Add(_restore, _mirror, _actual);
        Add(new Label { X = 1, Y = 10, Height = 2, Width = Dim.Fill(1), Text = localizer.Get("Diagnostics.ProfilerWarningPersistent") });
        ActionButton("Diagnostics.ScopeAll", () => { _scope.Text = string.Join(',', databases.Where(name => name is not ("admin" or "local" or "config"))); return Task.CompletedTask; });
        ActionButton("Diagnostics.Refresh", LoadAsync); var apply = ActionButton("Diagnostics.Apply", ApplyAsync);
        foreach (var button in Buttons) button.IsDefault = false;
        apply.IsDefault = true; DefaultAcceptView = apply;
    }
    private void Field(string key, TextField control, string value, int y)
    {
        Add(new Label { X = 1, Y = y, Text = Localizer.Get("Diagnostics." + key) });
        control.X = 43; control.Y = y; control.Width = Dim.Fill(1); control.Text = value; Add(control);
    }
    public Task LoadAsync()
    {
        if (Busy || Closed) return Task.CompletedTask;
        var names = _scope.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var restoreScope = !_initialized;
        SetEditingEnabled(false);
        return RunAsync(async token =>
        {
            ProfilerPreferences? preferences = null; var preferencesFailed = false;
            try { preferences = await _services.Settings.GetProfilerPreferencesAsync(_target.ConnectionId, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { preferencesFailed = true; }
            var scope = restoreScope && names.Length == 1 && preferences?.Databases.Contains(names[0]) == true ? preferences.Databases : names;
            var states = new List<ProfilerState>();
            foreach (var name in scope)
            {
                try { states.Add(await _services.Server.GetProfilerStateAsync(_target, name, token).ConfigureAwait(false)); }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception) { states.Add(new(name, ProfilingLevel.Off, new(), null, false,
                    exception is MongoTui.Domain.Exceptions.DiagnosticsException diagnostic ? diagnostic.Key : "Diagnostics.Error.Command")); }
            }
            return (Scope: scope, States: states, Preferences: preferences, PreferencesFailed: preferencesFailed);
        }, loaded =>
        {
            _initialized = true; _preferences = loaded.Preferences; _scope.Text = string.Join(',', loaded.Scope);
            _mirror.Value = _services.Settings.Current.MirrorServerSlowToApplicationLog ? CheckState.Checked : CheckState.UnChecked;
            if (loaded.States.FirstOrDefault(state => state.IsAvailable) is { } actual) FillFields(actual);
            var lines = loaded.States.Select(state => state.IsAvailable
                ? $"{state.Database}: {state.Level}, {state.Options.SlowMs} ms, {state.Options.SampleRate}"
                : state.Database + ": " + Localizer.Get(state.ErrorCode ?? "Diagnostics.Error.Command"));
            _actual.Text = (ProfilerFormState.IsMixed(loaded.States.Where(state => state.IsAvailable).ToArray())
                ? Localizer.Get("Diagnostics.ProfilerMixed") + "\n" : "") + string.Join('\n', lines);
            if (loaded.PreferencesFailed) Feedback.Text = Localizer.Get("Diagnostics.ProfilerPreferencesLoadFailed");
        }, () => SetEditingEnabled(true));
    }

    private void FillFields(ProfilerState actual)
    {
        var lease = _services.Server.Leases.FirstOrDefault(item => item.Target == _target && item.Database == actual.Database);
        var form = ProfilerFormState.From(actual, _services.Settings.Current, _preferences, lease);
        _hydrating = true;
        try
        {
            _mode.Value = (int)form.Mode;
            _threshold.Text = form.Options.SlowMs.ToString(CultureInfo.InvariantCulture);
            _sample.Text = form.Options.SampleRate.ToString("R", CultureInfo.InvariantCulture);
            _filter.Text = form.Options.FilterJson ?? ""; _filterAction.Value = 0;
            _restore.Value = form.RestoreOnExit ? CheckState.Checked : CheckState.UnChecked;
            _minutes.Text = form.AutoDisableMinutes.ToString(CultureInfo.InvariantCulture);
        }
        finally { _hydrating = false; }
    }

    private void ModeChanged()
    {
        if (_hydrating) return;
        var mode = (SlowQueryMode)(_mode.Value ?? 0);
        var policy = _preferences?.Mode == mode ? _preferences : ProfilerPreferences.Defaults(mode);
        _restore.Value = policy.RestoreOnExit ? CheckState.Checked : CheckState.UnChecked;
        _minutes.Text = policy.AutoDisableMinutes.ToString(CultureInfo.InvariantCulture);
        if (_preferences?.Mode == mode)
        {
            _threshold.Text = _preferences.SlowMs.ToString(CultureInfo.InvariantCulture);
            _sample.Text = _preferences.SampleRate.ToString("R", CultureInfo.InvariantCulture);
        }
        else if (mode == SlowQueryMode.Client) _threshold.Text = _services.Settings.Current.SlowThresholdMs.ToString(CultureInfo.InvariantCulture);
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var control in new View[] { _mode, _scope, _threshold, _sample, _filter, _filterAction, _minutes, _restore, _mirror }) control.Enabled = enabled;
    }
    private Task ApplyAsync()
    {
        if (Busy || Closed) return Task.CompletedTask;
        var mode = _mode.Value ?? 0;
        var mirror = _mirror.Value == CheckState.Checked;
        if (!int.TryParse(_threshold.Text, out var threshold) || !double.TryParse(_sample.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var sample)
            || !int.TryParse(_minutes.Text, out var minutes) || threshold is < 0 or > 600000 || !double.IsFinite(sample)
            || sample is < 0 or > 1 || minutes is < 0 or > 1440) { Feedback.Text = Localizer.Get("Diagnostics.Error.Command"); return Task.CompletedTask; }
        if (mode == 3)
        {
            using var confirmation = new ConfirmDialog(Localizer, UiApplication, Localizer.Get("Diagnostics.Profiler"), Localizer.Get("Diagnostics.ProfilerWarningAll"));
            UiApplication.Run(confirmation); if (!confirmation.Confirmed) return Task.CompletedTask;
        }
        var request = new ProfilerApplyRequest(_scope.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            mode == 3 ? ProfilingLevel.All : mode == 2 ? ProfilingLevel.Slow : ProfilingLevel.Off,
            new ProfilerOptions { SlowMs = threshold, SampleRate = sample, FilterJson = _filter.Text }, mode == 3,
            _restore.Value == CheckState.Checked, minutes, (ProfilerFilterAction)(_filterAction.Value ?? 0));
        var preferences = new ProfilerPreferences { Mode = (SlowQueryMode)mode, Databases = request.Databases,
            SlowMs = threshold, SampleRate = sample, RestoreOnExit = request.RestoreOnExit, AutoDisableMinutes = minutes };
        SetEditingEnabled(false);
        return RunAsync(async token =>
        {
            IReadOnlyList<ProfilerApplyResult> results = mode == 1 ? [] : await _services.Server.ApplyProfilerAsync(_target, request, token).ConfigureAwait(false);
            var applied = mode == 1 || results.Any(result => result.Actual is not null && result.Error is null);
            var saved = !applied || await SavePreferencesAsync(preferences, mirror, token).ConfigureAwait(false);
            return (Results: results, Applied: applied, Saved: saved);
        }, outcome =>
        {
            if (outcome.Applied) { _initialized = true; _preferences = preferences; }
            if (outcome.Results.FirstOrDefault(result => result.Actual is not null)?.Actual is { } actual) FillFields(actual);
            _actual.Text = mode == 1 ? Localizer.Get("Diagnostics.Client") + $": {threshold} ms" : string.Join('\n', outcome.Results.Select(result =>
                $"{result.Database}: {result.Requested} → {result.Actual?.Level} {(result.Error is { } error ? Localizer.Get(error) : "✓")}"));
            if (!outcome.Saved) Feedback.Text = Localizer.Get("Diagnostics.ProfilerPreferencesSaveFailed");
        }, () => SetEditingEnabled(true));
    }

    private async Task<bool> SavePreferencesAsync(ProfilerPreferences preferences, bool mirror, CancellationToken cancellationToken)
    {
        try
        {
            await _services.Settings.SaveProfilerFormAsync(_target.ConnectionId, preferences, mirror, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (preferences.Mode != SlowQueryMode.Client) { return false; }
    }
}
