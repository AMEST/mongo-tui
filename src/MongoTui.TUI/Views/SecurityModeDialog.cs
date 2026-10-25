using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>First-run dialog that chooses how the encryption key is protected.</summary>
public sealed class SecurityModeDialog : ThemedDialog
{
    private readonly SecurityModeService _security;
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly IReadOnlyList<SecurityMode> _modes;
    private readonly OptionSelector _modeList = new() { Orientation = Orientation.Vertical };
    private readonly Label _description = new();
    private readonly Label _warning = new();
    private readonly Label _passwordLabel = new();
    private readonly Label _confirmLabel = new();
    private readonly TextField _passwordField = new() { Secret = true };
    private readonly TextField _confirmField = new() { Secret = true };
    private readonly Label _error = new();

    public SecurityModeDialog(SecurityModeService security, ILocalizer localizer, IApplication application)
    {
        _security = security;
        _application = application;
        _localizer = localizer;
        _modes = security.AvailableModes;

        Title = localizer.Get(LocalizationKeys.Security.ModeTitle);
        Width = 72;
        Height = 20;

        BuildModeSection();
        BuildPasswordSection();
        BuildFeedbackSection();
        BuildActions();
        UpdateFields();
    }

    public bool Completed { get; private set; }

    private SecurityMode SelectedMode => _modes[_modeList.Value ?? 0];

    private void BuildModeSection()
    {
        Add(new Label { Text = _localizer.Get(LocalizationKeys.Security.ModeField), X = 1, Y = 1 });
        _modeList.X = 1;
        _modeList.Y = 2;
        _modeList.Width = Dim.Fill();
        _modeList.Height = _modes.Count;
        _modeList.Labels = _modes.Select(ModeName).ToList();
        _modeList.Values = Enumerable.Range(0, _modes.Count).ToList();
        _modeList.Value = 0;
        _modeList.ValueChanged += (_, _) => UpdateFields();
        Add(_modeList);

        _description.X = 1;
        _description.Y = 6;
        _description.Width = Dim.Fill();
        _description.Height = 2;
        Add(_description);
    }

    private void BuildPasswordSection()
    {
        _passwordLabel.Text = _localizer.Get(LocalizationKeys.Security.ModePasswordField);
        _passwordLabel.X = 1;
        _passwordLabel.Y = 9;
        Add(_passwordLabel);
        _passwordField.X = 1;
        _passwordField.Y = 10;
        _passwordField.Width = Dim.Fill();
        Add(_passwordField);

        _confirmLabel.Text = _localizer.Get(LocalizationKeys.Security.ModePasswordConfirm);
        _confirmLabel.X = 1;
        _confirmLabel.Y = 11;
        Add(_confirmLabel);
        _confirmField.X = 1;
        _confirmField.Y = 12;
        _confirmField.Width = Dim.Fill();
        Add(_confirmField);
    }

    private void BuildFeedbackSection()
    {
        _warning.X = 1;
        _warning.Y = 13;
        _warning.Width = Dim.Fill();
        _warning.Height = 2;
        Add(_warning);

        _error.X = 1;
        _error.Y = 15;
        _error.Width = Dim.Fill();
        Add(_error);
    }

    private void BuildActions()
    {
        var enableButton = new Button { Text = _localizer.Get(LocalizationKeys.Security.ModeInitialize), IsDefault = true };
        enableButton.OnAcceptAsync(InitializeAsync);
        var cancelButton = new Button { Text = _localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(enableButton, cancelButton);
    }

    private void UpdateFields()
    {
        if (_modes.Count == 0)
        {
            return;
        }

        var mode = SelectedMode;
        var password = mode == SecurityMode.Password;
        _passwordLabel.Visible = password;
        _passwordField.Visible = password;
        _confirmLabel.Visible = password;
        _confirmField.Visible = password;
        _description.Text = _localizer.Get(DescriptionKey(mode));
        _warning.Text = mode == SecurityMode.None ? _localizer.Get(LocalizationKeys.Security.ModeNoneWarning) : string.Empty;
        _warning.Visible = mode == SecurityMode.None;
        _error.Text = string.Empty;
    }

    private async Task InitializeAsync()
    {
        _error.Text = string.Empty;
        var mode = SelectedMode;

        if (mode == SecurityMode.Password)
        {
            var password = _passwordField.Text ?? string.Empty;
            if (password.Length == 0)
            {
                _error.Text = _localizer.Get(LocalizationKeys.Security.ModeErrorPasswordEmpty);
                return;
            }

            if (password != (_confirmField.Text ?? string.Empty))
            {
                _error.Text = _localizer.Get(LocalizationKeys.Security.ModeErrorPasswordMismatch);
                return;
            }
        }

        try
        {
            await _security.InitializeAsync(mode, _passwordField.Text, CancellationToken.None);
            Completed = true;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }

    private string ModeName(SecurityMode mode) => _localizer.Get(mode switch
    {
        SecurityMode.Password => LocalizationKeys.Security.ModePassword,
        SecurityMode.Keyring => LocalizationKeys.Security.ModeKeyring,
        _ => LocalizationKeys.Security.ModeNone,
    });

    private static string DescriptionKey(SecurityMode mode) => mode switch
    {
        SecurityMode.Password => LocalizationKeys.Security.ModePasswordDescription,
        SecurityMode.Keyring => LocalizationKeys.Security.ModeKeyringDescription,
        _ => LocalizationKeys.Security.ModeNoneDescription,
    };
}
