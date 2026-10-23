using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog listing and managing users of a database.</summary>
public sealed class UserManagementDialog : ThemedDialog
{
    private readonly UserManagementService _users;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly ActiveConnection _connection;
    private readonly DatabaseName _scope;
    private readonly string? _currentUsername;
    private readonly string? _currentAuthDatabase;
    private readonly ListView _list = new();
    private readonly Label _status = new();
    private readonly Button _edit;
    private readonly Button _delete;

    private IReadOnlyList<MongoUser> _userList = [];

    public UserManagementDialog(
        UserManagementService users,
        ILocalizer localizer,
        IApplication application,
        ActiveConnection connection,
        DatabaseName scope,
        string? currentUsername,
        string? currentAuthDatabase)
    {
        _users = users;
        _localizer = localizer;
        _application = application;
        _connection = connection;
        _scope = scope;
        _currentUsername = currentUsername;
        _currentAuthDatabase = currentAuthDatabase;

        Title = localizer.Format("Users.Title", scope.Value);
        Width = 88;
        Height = 20;

        _list.X = 1;
        _list.Y = 1;
        _list.Width = Dim.Fill();
        _list.Height = Dim.Fill(5);
        _list.Accepted += (_, _) => _ = GuardedAsync(EditAsync);
        _list.KeyDown += OnListKeyDown;
        _list.ValueChanged += (_, _) => UpdateActions();
        Add(_list);

        _status.X = 1;
        _status.Y = Pos.AnchorEnd(3);
        _status.Width = Dim.Fill();
        Add(_status);

        var create = new Button { Text = localizer.Get("Users.Create") };
        create.OnAcceptAsync(() => GuardedAsync(CreateAsync));
        _edit = new Button { Text = localizer.Get("Users.Edit"), Enabled = false };
        _edit.OnAcceptAsync(() => GuardedAsync(EditAsync));
        _delete = new Button { Text = localizer.Get("Users.Delete"), Enabled = false };
        _delete.OnAcceptAsync(() => GuardedAsync(DeleteAsync));
        var refresh = new Button { Text = localizer.Get("Users.Refresh") };
        refresh.OnAcceptAsync(() => GuardedAsync(LoadAsync));
        var close = new Button { Text = localizer.Get("Common.Ok"), IsDefault = true };
        close.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(create, _edit, _delete, refresh, close);
    }

    public async Task LoadAsync()
    {
        try
        {
            _userList = await _users.ListAsync(_connection.Target, _scope, CancellationToken.None);
            var items = _userList.Count == 0
                ? new List<string> { _localizer.Get("Users.Empty") }
                : _userList.Select(FormatUser).ToList();
            _list.SetSource(new ObservableCollection<string>(items));
            _list.SelectedItem = _userList.Count > 0 ? 0 : null;
            _status.Text = string.Empty;
            UpdateActions();
        }
        catch (Exception exception)
        {
            _status.Text = $"{_localizer.Get("Users.LoadFailed")} {ErrorText(exception)}";
        }
    }

    private void OnListKeyDown(object? sender, Key key)
    {
        if (key != Key.Delete || _userList.Count == 0)
        {
            return;
        }

        key.Handled = true;
        _ = GuardedAsync(DeleteAsync);
    }

    private async Task CreateAsync()
    {
        using var form = new UserFormDialog(_users, _localizer, _application, _connection, _scope, existingUser: null);
        _application.Run(form);
        if (!form.Saved)
        {
            return;
        }

        if (form.CreatedPassword is { } password && form.Username is { } username)
        {
            var connectionString = _users.BuildConnectionString(
                _connection.Profile,
                username,
                password,
                _scope.Value);

            using var result = new UserCreatedDialog(
                _localizer,
                _application,
                username,
                password,
                connectionString,
                _connection.Profile.Ssh.Enabled);
            _application.Run(result);
        }

        await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedUser() is not { } user)
        {
            return;
        }

        using var form = new UserFormDialog(_users, _localizer, _application, _connection, _scope, user);
        _application.Run(form);
        if (form.Saved)
        {
            await LoadAsync();
            if (form.PasswordChanged && IsCurrentUser(user))
            {
                _status.Text = _localizer.Get("Users.PasswordChangedCurrent");
            }
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedUser() is not { } user)
        {
            return;
        }

        if (IsCurrentUser(user))
        {
            _status.Text = _localizer.Get("Users.DeleteCurrentForbidden");
            return;
        }

        var confirmed = new ConfirmDialog(
            _localizer,
            _application,
            _localizer.Get("Users.DeleteConfirmTitle"),
            _localizer.Format("Users.DeleteConfirmMessage", user.Username, user.Database));
        _application.Run(confirmed);
        if (!confirmed.Confirmed)
        {
            return;
        }

        try
        {
            await _users.DeleteAsync(_connection.Target, _scope, user.Username, _currentUsername, _currentAuthDatabase, CancellationToken.None);
            await LoadAsync();
        }
        catch (DomainException exception)
        {
            _status.Text = _localizer.GetErrorMessage(exception);
        }
    }

    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _status.Text = ErrorText(exception);
        }
    }

    private string ErrorText(Exception exception) =>
        exception is DomainException domain ? _localizer.GetErrorMessage(domain) : exception.Message;

    private MongoUser? SelectedUser() =>
        _list.SelectedItem is int index && index >= 0 && index < _userList.Count ? _userList[index] : null;

    private void UpdateActions() =>
        _edit.Enabled = _delete.Enabled = SelectedUser() is not null;

    private bool IsCurrentUser(MongoUser user) =>
        string.Equals(user.Username, _currentUsername, StringComparison.Ordinal) &&
        string.Equals(user.Database, _currentAuthDatabase ?? _scope.Value, StringComparison.Ordinal);

    private string FormatUser(MongoUser user)
    {
        var databaseParts = new List<string>();
        var clusterParts = new List<string>();
        var additional = 0;
        foreach (var role in user.Roles)
        {
            if (MongoRoleCatalog.TryGetClusterRole(role, out _))
            {
                clusterParts.Add(role.Name == "root" ? "Administrator" : role.Name);
            }
            else if (MongoRoleCatalog.TryGetDatabaseAccess(role, out var access))
            {
                databaseParts.Add($"{access.Database.Value}: {access.Level switch
                {
                    DatabaseAccessLevel.Read => "r",
                    DatabaseAccessLevel.ReadWrite => "rw",
                    _ => "dbOwner",
                }}");
            }
            else
            {
                additional++;
            }
        }

        var groups = new List<string>();
        if (databaseParts.Count > 0)
        {
            groups.Add($"[{string.Join(", ", databaseParts)}]");
        }
        groups.AddRange(clusterParts.Select(role => $"[{role}]"));
        var suffix = additional > 0 ? $" {_localizer.Format("Users.MoreRoles", additional)}" : string.Empty;
        var details = string.Join(" ", groups);
        return details.Length == 0 ? $"{user.Username}{suffix}" : $"{user.Username}  {details}{suffix}";
    }
}
