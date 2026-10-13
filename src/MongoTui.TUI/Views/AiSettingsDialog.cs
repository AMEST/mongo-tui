using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that configures the OpenAI-compatible AI assistant.</summary>
public sealed class AiSettingsDialog : ThemedDialog
{
    private readonly AiSettingsService _settings;
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly TextField _urlField = new();
    private readonly TextField _modelField = new();
    private readonly TextField _apiKeyField = new() { Secret = true };
    private readonly Label _error = new();
    private string? _loadedApiKey;

    public AiSettingsDialog(AiSettingsService settings, ILocalizer localizer, IApplication application)
    {
        _settings = settings;
        _application = application;
        _localizer = localizer;

        Title = localizer.Get("Ai.Settings.Title");
        Width = 72;
        Height = 15;

        Add(new Label { Text = localizer.Get("Ai.Settings.Url"), X = 1, Y = 1 });
        _urlField.X = 20;
        _urlField.Y = 1;
        _urlField.Width = Dim.Fill();
        Add(_urlField);

        Add(new Label { Text = localizer.Get("Ai.Settings.Model"), X = 1, Y = 3 });
        _modelField.X = 20;
        _modelField.Y = 3;
        _modelField.Width = Dim.Fill();
        Add(_modelField);

        Add(new Label { Text = localizer.Get("Ai.Settings.ApiKey"), X = 1, Y = 5 });
        _apiKeyField.X = 20;
        _apiKeyField.Y = 5;
        _apiKeyField.Width = Dim.Fill();
        Add(_apiKeyField);

        _error.X = 1;
        _error.Y = 7;
        _error.Width = Dim.Fill();
        Add(_error);

        var saveButton = new Button { Text = localizer.Get("Connections.Save"), IsDefault = true };
        saveButton.OnAcceptAsync(SaveAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(saveButton, cancelButton);
    }

    public bool Saved { get; private set; }

    /// <summary>Loads the current configuration into the fields.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var configuration = await _settings.GetAsync(cancellationToken);

        _urlField.Text = configuration.BaseUrl;
        _modelField.Text = configuration.Model;
        _loadedApiKey = configuration.ApiKey;
        _apiKeyField.Text = configuration.ApiKey ?? string.Empty;
    }

    private async Task SaveAsync()
    {
        _error.Text = string.Empty;

        try
        {
            var enteredApiKey = _apiKeyField.Text ?? string.Empty;
            var apiKey = enteredApiKey == (_loadedApiKey ?? string.Empty) ? null : enteredApiKey;

            await _settings.SaveAsync(
                _urlField.Text ?? string.Empty,
                _modelField.Text ?? string.Empty,
                apiKey,
                CancellationToken.None);

            Saved = true;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }
}
