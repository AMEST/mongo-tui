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

    public QuerySettingsDialog(SettingsService settings, ILocalizer localizer, IApplication application)
    {
        _settings = settings;
        _localizer = localizer;
        _application = application;
        Title = localizer.Get(LocalizationKeys.Query.SettingsTitle);
        Width = 72;
        Height = 11;
        BuildFields();
        AddActions();
    }

    public bool Saved { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken) =>
        _timeout.Text = (await _settings.GetQueryTimeoutSecondsAsync(cancellationToken))
            .ToString(CultureInfo.InvariantCulture);

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
        Add(_error);
    }

    private void AddActions()
    {
        var save = new Button { Text = _localizer.Get(LocalizationKeys.Connections.Save), IsDefault = true };
        save.OnAcceptAsync(SaveAsync);
        var cancel = new Button { Text = _localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancel.OnAccept(() => _application.RequestStop(this));
        this.AddButtons(save, cancel);
    }

    internal async Task SaveAsync()
    {
        _error.Text = string.Empty;
        if (!int.TryParse(_timeout.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds is < 0 or > SettingsService.MaximumQueryTimeoutSeconds)
        {
            _error.Text = _localizer.Format(LocalizationKeys.Query.TimeoutInvalid, SettingsService.MaximumQueryTimeoutSeconds);
            return;
        }
        try
        {
            await _settings.SetQueryTimeoutSecondsAsync(seconds, CancellationToken.None);
            Saved = true;
            _application.RequestStop(this);
        }
        catch { _error.Text = _localizer.Get(LocalizationKeys.Query.SettingsSaveError); }
    }
}
