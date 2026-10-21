using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Dialog that switches the security mode or changes the master password.</summary>
public sealed class SecuritySettingsDialog : ThemedDialog
{
    private readonly SecurityModeService _security;
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly IReadOnlyList<SecurityMode> _modes;
    private readonly Label _currentLabel = new();
    private readonly OptionSelector _modeList = new() { Orientation = Orientation.Vertical };
    private readonly Label _description = new();
    private readonly Label _warning = new();
    private readonly Label _hint = new();
    private readonly Label _passwordLabel = new();
    private readonly Label _confirmLabel = new();
    private readonly TextField _passwordField = new() { Secret = true };
    private readonly TextField _confirmField = new() { Secret = true };
    private readonly Label _error = new();

    public SecuritySettingsDialog(SecurityModeService security, ILocalizer localizer, IApplication application)
    {
        _security = security;
        _application = application;
        _localizer = localizer;
        _modes = security.AvailableModes;

        Title = localizer.Get("Security.Settings.Title");
        Width = 76;
        Height = 22;

        BuildModeSection();
        BuildPasswordSection();
        BuildFeedbackSection();
        BuildActions();
        UpdateFields();
    }

    public bool Changed { get; private set; }

    public bool Reset { get; private set; }

    private SecurityMode? CurrentMode => _security.CurrentMode;

    private SecurityMode SelectedMode => _modes[_modeList.Value ?? 0];

    private void BuildModeSection()
    {
        _currentLabel.X = 1;
        _currentLabel.Y = 1;
        _currentLabel.Width = Dim.Fill();
        Add(_currentLabel);

        Add(new Label { Text = _localizer.Get("Security.Settings.New"), X = 1, Y = 3 });
        _modeList.X = 1;
        _modeList.Y = 4;
        _modeList.Width = Dim.Fill();
        _modeList.Height = _modes.Count;
        _modeList.Labels = _modes.Select(ModeName).ToList();
        _modeList.Values = Enumerable.Range(0, _modes.Count).ToList();
        _modeList.Value = CurrentIndex();
        _modeList.ValueChanged += (_, _) => UpdateFields();
        Add(_modeList);

        _description.X = 1;
        _description.Y = 8;
        _description.Width = Dim.Fill();
        _description.Height = 2;
        Add(_description);
    }

    private void BuildPasswordSection()
    {
        _passwordLabel.Text = _localizer.Get("Security.Settings.Password");
        _passwordLabel.X = 1;
        _passwordLabel.Y = 11;
        Add(_passwordLabel);
        _passwordField.X = 1;
        _passwordField.Y = 12;
        _passwordField.Width = Dim.Fill();
        Add(_passwordField);

        _confirmLabel.Text = _localizer.Get("Security.Settings.PasswordConfirm");
        _confirmLabel.X = 1;
        _confirmLabel.Y = 13;
        Add(_confirmLabel);
        _confirmField.X = 1;
        _confirmField.Y = 14;
        _confirmField.Width = Dim.Fill();
        Add(_confirmField);
    }

    private void BuildFeedbackSection()
    {
        _hint.X = 1;
        _hint.Y = 15;
        _hint.Width = Dim.Fill();
        Add(_hint);

        _warning.X = 1;
        _warning.Y = 16;
        _warning.Width = Dim.Fill();
        _warning.Height = 2;
        Add(_warning);

        _error.X = 1;
        _error.Y = 18;
        _error.Width = Dim.Fill();
        Add(_error);
    }

    private void BuildActions()
    {
        var saveButton = new Button { Text = _localizer.Get("Security.Settings.Apply"), IsDefault = true };
        saveButton.OnAcceptAsync(ApplyAsync);
        var resetButton = new Button { Text = _localizer.Get("Security.Settings.Reset") };
        resetButton.OnAccept(ConfirmReset);
        var cancelButton = new Button { Text = _localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(saveButton, resetButton, cancelButton);
    }

    private void UpdateFields()
    {
        if (_modes.Count == 0 || CurrentMode is not { } current)
        {
            return;
        }

        var mode = SelectedMode;
        var password = mode == SecurityMode.Password;
        _currentLabel.Text = _localizer.Format("Security.Settings.Current", ModeName(current));
        _passwordLabel.Visible = password;
        _passwordField.Visible = password;
        _confirmLabel.Visible = password;
        _confirmField.Visible = password;
        _hint.Visible = password && current == SecurityMode.Password;
        _hint.Text = _localizer.Get("Security.Settings.PasswordHint");
        _description.Text = _localizer.Get(DescriptionKey(mode));
        _warning.Text = mode == SecurityMode.None ? _localizer.Get("Security.Mode.None.Warning") : string.Empty;
        _warning.Visible = mode == SecurityMode.None;
        _error.Text = string.Empty;
    }

    private async Task ApplyAsync()
    {
        _error.Text = string.Empty;
        if (CurrentMode is not { } current)
        {
            return;
        }

        var mode = SelectedMode;
        if (mode == current && mode != SecurityMode.Password)
        {
            _application.RequestStop(this);
            return;
        }

        if (!TryGetPassword(mode, current, out var password))
        {
            return;
        }

        if (current == SecurityMode.Password
            && mode != SecurityMode.Password
            && !Confirm("Security.Settings.SwitchTitle", "Security.Settings.SwitchConfirm"))
        {
            return;
        }

        await ApplyModeAsync(current, mode, password);
    }

    private bool TryGetPassword(SecurityMode mode, SecurityMode current, out string password)
    {
        password = _passwordField.Text ?? string.Empty;
        if (mode != SecurityMode.Password)
        {
            return true;
        }

        if (current == SecurityMode.Password && password.Length == 0)
        {
            _application.RequestStop(this);
            return false;
        }

        if (password.Length == 0)
        {
            _error.Text = _localizer.Get("Security.Mode.Error.PasswordEmpty");
            return false;
        }

        if (password != (_confirmField.Text ?? string.Empty))
        {
            _error.Text = _localizer.Get("Security.Mode.Error.PasswordMismatch");
            return false;
        }

        return true;
    }

    private async Task ApplyModeAsync(SecurityMode current, SecurityMode mode, string password)
    {
        try
        {
            if (mode == SecurityMode.Password && current == SecurityMode.Password)
            {
                await _security.ChangePasswordAsync(password, CancellationToken.None);
            }
            else
            {
                await _security.SwitchModeAsync(mode, mode == SecurityMode.Password ? password : null,
                    CancellationToken.None);
            }

            Changed = true;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }

    private void ConfirmReset()
    {
        if (!Confirm("Security.Settings.ResetTitle", "Security.Settings.ResetQuestion"))
        {
            return;
        }

        _ = ResetAsync();
    }

    private async Task ResetAsync()
    {
        try
        {
            await _security.ResetAsync(CancellationToken.None);
            Reset = true;
            Changed = true;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }

    private bool Confirm(string titleKey, string messageKey)
    {
        var confirm = new ConfirmDialog(
            _localizer,
            _application,
            _localizer.Get(titleKey),
            _localizer.Get(messageKey));
        _application.Run(confirm);
        return confirm.Confirmed;
    }

    private int CurrentIndex()
    {
        var current = CurrentMode;
        var index = current is null ? -1 : _modes.ToList().IndexOf(current.Value);
        return index < 0 ? 0 : index;
    }

    private string ModeName(SecurityMode mode) => _localizer.Get(mode switch
    {
        SecurityMode.Password => "Security.Mode.Password",
        SecurityMode.Keyring => "Security.Mode.Keyring",
        _ => "Security.Mode.None",
    });

    private static string DescriptionKey(SecurityMode mode) => mode switch
    {
        SecurityMode.Password => "Security.Mode.Password.Description",
        SecurityMode.Keyring => "Security.Mode.Keyring.Description",
        _ => "Security.Mode.None.Description",
    };
}
