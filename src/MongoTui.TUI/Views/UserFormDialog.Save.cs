using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;
using Serilog;
using Terminal.Gui.Drawing;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace MongoTui.TUI.Views;

public sealed partial class UserFormDialog
{
    private bool _saving;

    private async Task SaveAsync()
    {
        if (_saving) return;
        var username = (_username.Text ?? string.Empty).Trim();
        var password = _password.Text ?? string.Empty;
        if (ValidationError(username, password) is { } error)
        {
            SetStatus(_localizer.Get(error), ColorName16.BrightRed);
            return;
        }

        _saving = true;
        _save.Enabled = false;
        SetStatus(_localizer.Get("Users.Saving"), ColorName16.BrightYellow);
        Exception? saveError = null;
        try
        {
            await SaveUserAsync(username, password).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            saveError = exception;
        }

        await _application.InvokeAsync(() => CompleteSave(saveError)).ConfigureAwait(false);
    }

    private void CompleteSave(Exception? error)
    {
        _saving = false;
        _save.Enabled = true;
        if (error is not null)
        {
            ShowSaveError(error);
            return;
        }

        Saved = true;
        _error.Text = string.Empty;
        Log.Information("MongoDB user save completed");
        _application.RequestStop(this);
    }

    private async Task SaveUserAsync(string username, string password)
    {
        var access = new UserAccessSelection(_accesses.ToList(), _clusterRoles.Selected());
        if (_isEdit && _existing is not null)
        {
            await _users.UpdateAsync(_connection.Target, _scope, _existing, password, access, CancellationToken.None).ConfigureAwait(false);
            PasswordChanged = password.Length > 0;
        }
        else
        {
            await _users.CreateAsync(_connection.Target, _scope, username, password, access, CancellationToken.None).ConfigureAwait(false);
        }

        SavedPassword = !_isEdit || PasswordChanged ? password : null;
        Username = username;
    }

    private string? ValidationError(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Users.UsernameRequired";
        if (!_isEdit && string.IsNullOrWhiteSpace(password)) return "Users.PasswordRequired";
        return !_isEdit && _accesses.Count == 0 && _clusterRoles.Selected().Count == 0
            ? "Users.Access.Required"
            : null;
    }

    private void ShowSaveError(Exception exception)
    {
        var message = exception is DomainException domain ? _localizer.GetErrorMessage(domain) : exception.Message;
        SetStatus(message, ColorName16.BrightRed);
        if (exception is DomainException)
        {
            Log.Warning("MongoDB user save failed ({ExceptionType})", exception.GetType().Name);
        }
        else
        {
            Log.Error("MongoDB user save failed ({ExceptionType})", exception.GetType().Name);
        }
    }

    private void SetStatus(string message, ColorName16 color)
    {
        var attribute = new TuiAttribute(color, GetScheme().Normal.Background);
        _error.SetScheme(new Scheme { Normal = attribute, HotNormal = attribute });
        _error.Text = message;
        SetNeedsDraw();
    }
}
