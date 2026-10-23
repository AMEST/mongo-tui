using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Shows a newly created user's password and connection string once.</summary>
public sealed class UserCreatedDialog : ThemedDialog
{
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly string _connectionString;
    private readonly Label _status;
    private readonly bool _sshEnabled;

    public UserCreatedDialog(
        ILocalizer localizer,
        IApplication application,
        string username,
        string password,
        string connectionString,
        bool sshEnabled)
    {
        _application = application;
        _localizer = localizer;
        _connectionString = connectionString;
        _sshEnabled = sshEnabled;

        Title = localizer.Get("Users.Created.Title");
        Width = 84;
        Height = 22;

        var text = string.Join(Environment.NewLine,
        [
            $"{localizer.Get("Users.Field.Username")}: {username}",
            $"{localizer.Get("Users.Field.Password")}: {password}",
            string.Empty,
            connectionString,
            string.Empty,
            localizer.Get("Users.Created.PasswordShownOnce"),
            sshEnabled ? localizer.Get("Users.Created.SshWarning") : string.Empty,
        ]);

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.
        var view = new TextView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(3), ReadOnly = true, Multiline = true, Text = text };
#pragma warning restore CS0618
        Add(view);

        _status = new Label { X = 1, Y = Pos.AnchorEnd(3), Width = Dim.Fill(1) };
        Add(_status);

        var copy = new Button { Text = localizer.Get("Users.Created.CopyConnectionString") };
        copy.OnAccept(() => Copy());
        var close = new Button { Text = localizer.Get("Common.Ok"), IsDefault = true };
        close.OnAccept(() => application.RequestStop(this));
        this.AddButtons(copy, close);
    }

    private void Copy()
    {
        var copied = _application.Clipboard?.TrySetClipboardData(_connectionString) == true;
        _status.Text = _localizer.Get(copied ? "Users.Created.Copied" : "Users.Created.CopyFailed");
    }
}
