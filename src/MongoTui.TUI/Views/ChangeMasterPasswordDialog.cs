using MongoTui.Application.Localization;
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
    private readonly SecurityModeService _security;
    private readonly IApplication _application;
    private readonly TextField _passwordField = new() { Secret = true };
    private readonly Label _error = new();

    public ChangeMasterPasswordDialog(SecurityModeService security, ILocalizer localizer, IApplication application)
    {
        _security = security;
        _application = application;

        Title = localizer.Get(LocalizationKeys.ChangePassword.Title);
        Width = 64;
        Height = 10;

        Add(new Label { Text = localizer.Get(LocalizationKeys.ChangePassword.Field), X = 1, Y = 1 });
        _passwordField.X = 1;
        _passwordField.Y = 3;
        _passwordField.Width = Dim.Fill();
        Add(_passwordField);

        _error.X = 1;
        _error.Y = 5;
        _error.Width = Dim.Fill();
        Add(_error);

        var changeButton = new Button { Text = localizer.Get(LocalizationKeys.ChangePassword.Change), IsDefault = true };
        changeButton.OnAcceptAsync(ChangeAsync);
        var cancelButton = new Button { Text = localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancelButton.OnAccept(() => application.RequestStop(this));

        this.AddButtons(changeButton, cancelButton);
    }

    public bool Changed { get; private set; }

    private async Task ChangeAsync()
    {
        _error.Text = string.Empty;

        try
        {
            await _security.ChangePasswordAsync(_passwordField.Text ?? string.Empty, CancellationToken.None);
            Changed = true;
            _application.RequestStop(this);
        }
        catch (DomainException exception)
        {
            _error.Text = exception.Message;
        }
    }
}
