using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that unlocks the connection secrets with the master password.</summary>
public sealed class MasterPasswordDialog : ThemedDialog
{
    private readonly ConnectionService _connections;
    private readonly IApplication _application;
    private readonly TextField _passwordField = new() { Secret = true };
    private readonly Label _error = new();

    public MasterPasswordDialog(ConnectionService connections, ILocalizer localizer, IApplication application)
    {
        _connections = connections;
        _application = application;

        Title = localizer.Get("MasterPassword.Title");
        Width = 60;
        Height = 10;

        Add(new Label { Text = localizer.Get("MasterPassword.Field"), X = 1, Y = 1 });
        _passwordField.X = 1;
        _passwordField.Y = 3;
        _passwordField.Width = Dim.Fill();
        Add(_passwordField);

        _error.X = 1;
        _error.Y = 5;
        _error.Width = Dim.Fill();
        Add(_error);

        var unlockButton = new Button { Text = localizer.Get("MasterPassword.Unlock"), IsDefault = true };
        unlockButton.OnAcceptAsync(UnlockAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        AddButton(unlockButton);
        AddButton(cancelButton);
    }

    public bool Unlocked { get; private set; }

    private async Task UnlockAsync()
    {
        _error.Text = string.Empty;

        try
        {
            await _connections.UnlockAsync(_passwordField.Text ?? string.Empty, CancellationToken.None);
            Unlocked = true;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }
}
