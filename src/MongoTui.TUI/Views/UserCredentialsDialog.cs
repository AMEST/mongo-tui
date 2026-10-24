using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Shows a user's password and connection string after creation or a password change.</summary>
public sealed class UserCredentialsDialog : ThemedDialog
{
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly Label _status;

    public UserCredentialsDialog(
        ILocalizer localizer,
        IApplication application,
        string username,
        string password,
        string connectionString,
        bool sshEnabled,
        bool passwordChanged)
    {
        _application = application;
        _localizer = localizer;

        Title = localizer.Get(passwordChanged ? "Users.PasswordChanged.Title" : "Users.Created.Title");
        Width = Dim.Fill(1);
        Height = 22;
        AddCredentials(username, password, connectionString, sshEnabled);

        _status = new Label { X = 1, Y = Pos.AnchorEnd(3), Width = Dim.Fill(1) };
        Add(_status);

        var copy = new Button { Text = localizer.Get("Users.Created.CopyConnectionString") };
        copy.OnAccept(() => Copy(connectionString, "Users.Created.Copied", "Users.Created.CopyFailed"));
        var copyPassword = new Button { Text = localizer.Get("Users.Credentials.CopyPassword") };
        copyPassword.OnAccept(() => Copy(password, "Users.Credentials.PasswordCopied", "Users.Credentials.PasswordCopyFailed"));
        var close = new Button { Text = localizer.Get("Common.Ok"), IsDefault = true };
        close.OnAccept(() => application.RequestStop(this));
        this.AddButtons(copyPassword, copy, close);
    }

    private void AddCredentials(string username, string password, string connectionString, bool sshEnabled)
    {
        var text = string.Join(Environment.NewLine,
        [
            $"{_localizer.Get("Users.Field.Username")}: {username}",
            $"{_localizer.Get("Users.Field.Password")}: {password}",
            string.Empty,
            connectionString,
            string.Empty,
            _localizer.Get("Users.Created.PasswordShownOnce"),
            sshEnabled ? _localizer.Get("Users.Created.SshWarning") : string.Empty,
        ]);

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.
        var view = new TextView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(3), ReadOnly = true, Multiline = true, Text = text };
#pragma warning restore CS0618
        Add(view);
    }

    private void Copy(string value, string successKey, string failureKey)
    {
        var copied = _application.Clipboard?.TrySetClipboardData(value) == true;
        _status.Text = _localizer.Get(copied ? successKey : failureKey);
    }
}
