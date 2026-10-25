using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed class LoggingSettingsDialog : ThemedDialog
{
    private readonly ILoggingSettings _settings;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly OptionSelector _level = new() { Orientation = Orientation.Vertical, Enabled = false };
    private readonly Label _override = new();
    private readonly Label _feedback = new();
    private readonly Button _save;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _loaded, _busy, _disposed;
    public bool Saved { get; private set; }

    public LoggingSettingsDialog(ILoggingSettings settings, ILocalizer localizer, IApplication application)
    {
        _settings = settings; _localizer = localizer; _application = application;
        Title = localizer.Get(LocalizationKeys.Logging.Title);
        Width = 82; Height = 18;
        BuildFields();
        _save = new Button { Text = localizer.Get(LocalizationKeys.Common.Save), IsDefault = true, Enabled = false };
        _save.OnAcceptAsync(SaveAsync);
        var cancel = new Button { Text = localizer.Get(LocalizationKeys.Common.Cancel) };
        cancel.OnAccept(() => application.RequestStop(this));
        this.AddButtons(_save, cancel);
        Disposing += (_, _) => { _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    private void BuildFields()
    {
        Add(new Label { Text = _localizer.Get(LocalizationKeys.Logging.Level), X = 1, Y = 1 });
        _level.X = 1; _level.Y = 2; _level.Width = Dim.Fill(1); _level.Height = 6;
        _level.Labels = Enum.GetNames<ApplicationLogLevel>().ToList();
        _level.Values = Enum.GetValues<ApplicationLogLevel>().Select(value => (int)value).ToList();
        _level.Value = (int)ApplicationLogLevel.Information;
        Add(_level);
        Add(new Label { Text = _localizer.Get(LocalizationKeys.Logging.Hint), X = 1, Y = 8, Width = Dim.Fill(1), Height = 2 });
        _override.X = 1; _override.Y = 10; _override.Width = Dim.Fill(1); _override.Height = 3;
        _feedback.X = 1; _feedback.Y = 13; _feedback.Width = Dim.Fill(1); _feedback.Height = 2;
        Add(_override, _feedback);
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_busy || _disposed) return;
        SetBusy(true);
        _feedback.Text = _localizer.Get(LocalizationKeys.Common.Loading);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        LoggingSettings? settings = null;
        try { settings = await _settings.GetAsync(operation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_disposed) { return; }
        catch { }
        await _application.InvokeAsync(() =>
        {
            if (_disposed) return;
            _loaded = settings is not null;
            if (settings is not null)
            {
                _level.Value = (int)settings.SavedLevel;
                ShowOverride(settings);
            }
            _feedback.Text = _loaded ? string.Empty : _localizer.Get(LocalizationKeys.Logging.LoadError);
            SetBusy(false);
        }).ConfigureAwait(false);
    }

    internal async Task SaveAsync()
    {
        if (_busy || !_loaded || _disposed) return;
        var level = (ApplicationLogLevel)(_level.Value ?? (int)ApplicationLogLevel.Information);
        SetBusy(true);
        _feedback.Text = _localizer.Get(LocalizationKeys.Common.Working);
        LoggingSettings? settings = null;
        try { settings = await _settings.SaveAsync(level, _lifetime.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_disposed) { return; }
        catch { }
        await _application.InvokeAsync(() =>
        {
            if (_disposed) return;
            SetBusy(false);
            Saved = settings is not null;
            _feedback.Text = Saved ? string.Empty : _localizer.Get(LocalizationKeys.Logging.SaveError);
            if (settings is not null) { ShowOverride(settings); _application.RequestStop(this); }
        }).ConfigureAwait(false);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _level.Enabled = _save.Enabled = _loaded && !busy;
    }

    private void ShowOverride(LoggingSettings settings) => _override.Text = settings.IsOverridden
        ? _localizer.Format(LocalizationKeys.Logging.Override, settings.EffectiveLevel) : string.Empty;
}
