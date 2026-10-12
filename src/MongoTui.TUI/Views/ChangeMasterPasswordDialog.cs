using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that changes the master password and re-encrypts secrets.</summary>
public sealed class ChangeMasterPasswordDialog : ThemedDialog
{
    private readonly ConnectionService _connections;
    private readonly IApplication _application;
    private readonly TextField _passwordField = new() { Secret = true };
    private readonly Label _error = new();

    public ChangeMasterPasswordDialog(ConnectionService connections, ILocalizer localizer, IApplication application)
    {
        _connections = connections;
        _application = application;

        Title = localizer.Get("ChangePassword.Title");
        Width = 64;
        Height = 10;

        Add(new Label { Text = localizer.Get("ChangePassword.Field"), X = 1, Y = 1 });
        _passwordField.X = 1;
        _passwordField.Y = 3;
        _passwordField.Width = Dim.Fill();
        Add(_passwordField);

        _error.X = 1;
        _error.Y = 5;
        _error.Width = Dim.Fill();
        Add(_error);

        var changeButton = new Button { Text = localizer.Get("ChangePassword.Change"), IsDefault = true };
        changeButton.OnAcceptAsync(ChangeAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => application.RequestStop(this));

        AddButton(changeButton);
        AddButton(cancelButton);
    }

    public bool Changed { get; private set; }

    private async Task ChangeAsync()
    {
        _error.Text = string.Empty;

        try
        {
            await _connections.ChangeMasterPasswordAsync(_passwordField.Text ?? string.Empty, CancellationToken.None);
            Changed = true;
            _application.RequestStop(this);
        }
        catch (DomainException exception)
        {
            _error.Text = exception.Message;
        }
    }
}
