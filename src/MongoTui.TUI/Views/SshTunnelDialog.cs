using System.Globalization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed class SshTunnelDialog : ThemedDialog
{
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly CheckBox _enabled = new();
    private readonly TextField _host = new();
    private readonly TextField _port = new();
    private readonly TextField _username = new();
    private readonly TextField _localPort = new();
    private readonly TextField _keyFile = new();
    private readonly Label _error = new();

    public SshTunnelDialog(ILocalizer localizer, IApplication application, SshTunnelOptions options)
    {
        _localizer = localizer;
        _application = application;
        Title = localizer.Get("Ssh.Title");
        Width = 76;
        Height = 22;
        ComposeFields(options);

        var save = new Button { Text = localizer.Get("Connections.Save"), IsDefault = true };
        save.OnAccept(Save);
        var cancel = new Button { Text = localizer.Get("Connections.Cancel") };
        cancel.OnAccept(() => application.RequestStop(this));
        this.AddButtons(save, cancel);
    }

    public SshTunnelOptions? Options { get; private set; }

    private void ComposeFields(SshTunnelOptions options)
    {
        _enabled.Text = _localizer.Get("Ssh.Enabled");
        _enabled.X = 1;
        _enabled.Y = 1;
        _enabled.Value = options.Enabled ? CheckState.Checked : CheckState.UnChecked;
        Add(_enabled);
        AddField(_host, "Ssh.Host", options.Host, 3);
        AddField(_port, "Ssh.Port", options.Port.ToString(CultureInfo.InvariantCulture), 5);
        AddField(_username, "Ssh.Username", options.Username, 7);
        AddField(_localPort, "Ssh.LocalPort", options.LocalPort.ToString(CultureInfo.InvariantCulture), 9);
        AddField(_keyFile, "Ssh.KeyFile", options.PrivateKeyFile, 11);
        Add(new Label { Text = _localizer.Get("Ssh.Hint"), X = 1, Y = 13, Width = Dim.Fill(), Height = 3 });
        _error.X = 1;
        _error.Y = 16;
        _error.Width = Dim.Fill();
        Add(_error);
    }

    private void AddField(TextField field, string key, string? value, int y)
    {
        Add(new Label { Text = _localizer.Get(key), X = 1, Y = y });
        field.X = 28;
        field.Y = y;
        field.Width = Dim.Fill();
        field.Text = value ?? string.Empty;
        Add(field);
    }

    private void Save()
    {
        if (!int.TryParse(_port.Text, out var port) || !int.TryParse(_localPort.Text, out var localPort))
        {
            _error.Text = _localizer.Get("Ssh.InvalidPorts");
            return;
        }

        var options = new SshTunnelOptions
        {
            Enabled = _enabled.Value == CheckState.Checked,
            Host = NullIfEmpty(_host.Text),
            Port = port,
            Username = NullIfEmpty(_username.Text),
            LocalPort = localPort,
            PrivateKeyFile = NullIfEmpty(_keyFile.Text),
        };
        try
        {
            options.Validate();
            Options = options;
            _application.RequestStop(this);
        }
        catch (DomainException)
        {
            _error.Text = _localizer.Get("Ssh.InvalidOptions");
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
