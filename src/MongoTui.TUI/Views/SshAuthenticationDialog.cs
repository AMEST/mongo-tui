using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed class SshAuthenticationDialog : ThemedDialog
{
    private readonly TextField _response = new() { Secret = true };
    private readonly IApplication _application;

    public SshAuthenticationDialog(ILocalizer localizer, IApplication application, string prompt)
    {
        _application = application;
        Title = localizer.Get("Ssh.Authentication.Title");
        Width = 76;
        Height = 15;
        Add(new Label { Text = localizer.Get("Ssh.Authentication.Hint"), X = 1, Y = 1 });
#pragma warning disable CS0618 // Existing Terminal.Gui 2.5 views use TextView for scrollable text.
        Add(new TextView
        {
            Text = prompt,
            ReadOnly = true,
            Multiline = true,
            WordWrap = true,
            X = 1,
            Y = 3,
            Width = Dim.Fill(),
            Height = 5
        });
#pragma warning restore CS0618
        _response.X = 1;
        _response.Y = 9;
        _response.Width = Dim.Fill();
        _response.KeyDown += OnResponseKeyDown;
        Add(_response);

        var submit = new Button { Text = localizer.Get("Common.Ok"), IsDefault = true };
        submit.OnAccept(Submit);
        var cancel = new Button { Text = localizer.Get("Connections.Cancel") };
        cancel.OnAccept(() => application.RequestStop(this));
        this.AddButtons(submit, cancel);
        _response.SetFocus();
    }

    public string? Response { get; private set; }

    private void OnResponseKeyDown(object? sender, Key key)
    {
        if (key == Key.Enter)
        {
            key.Handled = true;
            Submit();
        }
    }

    private void Submit()
    {
        Response = _response.Text ?? string.Empty;
        _response.Text = string.Empty;
        _application.RequestStop(this);
    }
}
