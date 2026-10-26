using MongoTui.Application.Localization;
using System.Globalization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Edits the query deadline shared by the terminal and web interfaces.</summary>
public sealed class QuerySettingsDialog : ThemedDialog
{
    private readonly SettingsService _settings;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly TextField _timeout = new();
    private readonly Label _error = new();
    private readonly Button _save = new();
    private readonly CancellationTokenSource _lifetime = new();
    private bool _busy, _loaded, _disposed;

    public QuerySettingsDialog(SettingsService settings, ILocalizer localizer, IApplication application)
    {
        _settings = settings;
        _localizer = localizer;
        _application = application;
        Title = localizer.Get(LocalizationKeys.Query.SettingsTitle);
        Width = 72;
        Height = 13;
        BuildFields();
        AddActions();
        SetBusy(false);
        Disposing += (_, _) => { _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    public bool Saved { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_busy || _disposed) return;
        SetBusy(true);
        _error.Text = _localizer.Get(LocalizationKeys.Common.Loading);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        int? seconds = null;
        try { seconds = await _settings.GetQueryTimeoutSecondsAsync(operation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch { }
        if (_lifetime.IsCancellationRequested) return;
        await _application.InvokeAsync(() =>
        {
            if (_disposed || _lifetime.IsCancellationRequested) return;
            _loaded = seconds is not null;
            if (seconds is { } value) _timeout.Text = value.ToString(CultureInfo.InvariantCulture);
            _error.Text = _loaded ? string.Empty : _localizer.Get(LocalizationKeys.Query.SettingsLoadError);
            SetBusy(false);
        }).ConfigureAwait(false);
    }

    private void BuildFields()
    {
        Add(new Label { Text = _localizer.Get(LocalizationKeys.Query.Timeout), X = 1, Y = 1 });
        _timeout.X = 32;
        _timeout.Y = 1;
        _timeout.Width = 12;
        Add(_timeout);
        Add(new Label { Text = _localizer.Get(LocalizationKeys.Query.TimeoutHint), X = 1, Y = 3, Width = Dim.Fill(), Height = 2 });
        _error.X = 1;
        _error.Y = 5;
        _error.Width = Dim.Fill();
        _error.Height = 1;
        Add(_error);
    }

    private void AddActions()
    {
        _save.Text = _localizer.Get(LocalizationKeys.Connections.Save);
        _save.IsDefault = true;
        _save.OnAcceptAsync(SaveAsync);
        var cancel = new Button { Text = _localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancel.OnAccept(() => { _lifetime.Cancel(); _application.RequestStop(this); });
        this.AddButtons(_save, cancel);
    }

    internal async Task SaveAsync()
    {
        if (_busy || !_loaded || _disposed) return;
        _error.Text = string.Empty;
        if (!int.TryParse(_timeout.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds is < 0 or > SettingsService.MaximumQueryTimeoutSeconds)
        {
            _error.Text = _localizer.Format(LocalizationKeys.Query.TimeoutInvalid, SettingsService.MaximumQueryTimeoutSeconds);
            return;
        }
        SetBusy(true);
        _error.Text = _localizer.Get(LocalizationKeys.Common.Working);
        var saved = false;
        try { await _settings.SetQueryTimeoutSecondsAsync(seconds, _lifetime.Token).ConfigureAwait(false); saved = true; }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch { }
        if (_lifetime.IsCancellationRequested) return;
        await _application.InvokeAsync(() =>
        {
            if (_disposed || _lifetime.IsCancellationRequested) return;
            SetBusy(false);
            Saved = saved;
            _error.Text = saved ? string.Empty : _localizer.Get(LocalizationKeys.Query.SettingsSaveError);
            if (saved) _application.RequestStop(this);
        }).ConfigureAwait(false);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _timeout.Enabled = _save.Enabled = _loaded && !busy;
        SetNeedsDraw();
    }
}
