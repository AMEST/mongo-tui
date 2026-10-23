using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Dialog for creating or editing a MongoDB user.</summary>
public sealed class UserFormDialog : ThemedDialog
{
    private readonly UserManagementService _users;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly ActiveConnection _connection;
    private readonly DatabaseName _scope;
    private readonly bool _isEdit;
    private readonly MongoUser? _existing;
    private readonly List<DatabaseAccess> _accesses = new();

    private readonly TextField _username = new() { X = 20, Y = 0, Width = Dim.Fill(1) };
    private readonly TextField _password = new() { X = 20, Y = 2, Width = Dim.Fill(8), Secret = true };
    private readonly Button _generate = new() { X = Pos.AnchorEnd(7), Y = 2 };
    private readonly Label _hint = new() { X = 1, Y = 3, Width = Dim.Fill(1), Height = 1, Visible = false };
    private readonly ListView _accessList = new() { X = 1, Y = 9, Width = Dim.Fill(1), Height = 5 };
    private readonly Button _addAccess = new() { X = 1 };
    private readonly Button _removeAccess = new() { X = 18 };
    private readonly ClusterRoleCheckboxes _clusterRoles;
    private readonly Label _additionalLabel = new() { X = 1, Y = 18, Width = Dim.Fill(1), Visible = false };
    private readonly ListView _additionalList = new() { X = 1, Y = 19, Width = Dim.Fill(1), Height = 3, Visible = false };
    private readonly Label _error = new() { X = 1, Y = Pos.AnchorEnd(2), Width = Dim.Fill(1), Height = 1 };

    public UserFormDialog(
        UserManagementService users,
        ILocalizer localizer,
        IApplication application,
        ActiveConnection connection,
        DatabaseName scope,
        MongoUser? existingUser)
    {
        _users = users;
        _localizer = localizer;
        _application = application;
        _connection = connection;
        _scope = scope;
        _existing = existingUser;
        _isEdit = existingUser is not null;

        Title = _isEdit ? localizer.Get("Users.Form.EditTitle") : localizer.Get("Users.Form.CreateTitle");
        Width = 72;
        Height = _isEdit ? 24 : 22;

        Add(new Label { Text = localizer.Get("Users.Field.Username"), X = 1, Y = 0 });
        Add(_username);
        if (_isEdit)
        {
            _username.ReadOnly = true;
            _username.Text = existingUser!.Username;
        }

        Add(new Label { Text = localizer.Get("Users.Field.Password"), X = 1, Y = 2 });
        Add(_password);
        _generate.Text = localizer.Get("Users.Field.Generate");
        _generate.OnAccept(() => _password.Text = _users.GeneratePassword());
        Add(_generate);

        if (_isEdit)
        {
            _hint.Text = localizer.Get("Users.Form.PasswordKeepHint");
            _hint.Visible = true;
            Add(_hint);
        }

        var accessLabelY = _isEdit ? 5 : 4;
        var accessListY = accessLabelY + 1;
        var accessButtonsY = accessListY + 3;
        var clusterLabelY = accessButtonsY + 2;

        Add(new Label { Text = localizer.Get("Users.Field.Access"), X = 1, Y = accessLabelY });
        _accessList.X = 1;
        _accessList.Y = accessListY;
        _accessList.Height = 3;
        _accessList.Width = Dim.Fill();
        Add(_accessList);
        _addAccess.Text = localizer.Get("Users.Access.Add");
        _addAccess.Y = accessButtonsY;
        _addAccess.OnAccept(() => AddAccess());
        _removeAccess.Text = localizer.Get("Users.Access.Remove");
        _removeAccess.Y = accessButtonsY;
        _removeAccess.OnAccept(() => RemoveAccess());
        Add(_addAccess, _removeAccess);

        Add(new Label { Text = localizer.Get("Users.ClusterRoles"), X = 1, Y = clusterLabelY });
        _clusterRoles = new ClusterRoleCheckboxes(localizer, 1, clusterLabelY + 1);
        _clusterRoles.AdministratorChanged += OnAdministratorChanged;
        foreach (var check in _clusterRoles.Checks)
        {
            Add(check);
        }

        if (_isEdit)
        {
            _additionalLabel.Text = localizer.Get("Users.Field.AdditionalRoles");
            _additionalLabel.Y = clusterLabelY + 4;
            _additionalList.Y = clusterLabelY + 5;
            _additionalList.Height = 2;
            _additionalLabel.Visible = true;
            _additionalList.Visible = true;
            Add(_additionalLabel);
            Add(_additionalList);
        }

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var save = new Button { Text = localizer.Get(_isEdit ? "Users.Edit" : "Users.Create"), IsDefault = true };
        save.OnAcceptAsync(SaveAsync);
        var cancel = new Button { Text = localizer.Get("Connections.Cancel") };
        cancel.OnAccept(() => _application.RequestStop(this));
        this.AddButtons(save, cancel);

        if (_isEdit && existingUser is not null)
        {
            LoadExisting(existingUser);
        }

        RefreshAccessList();
    }

    public string? CreatedPassword { get; private set; }
    public string? Username { get; private set; }
    public bool PasswordChanged { get; private set; }
    public bool Saved { get; private set; }

    private void LoadExisting(MongoUser user)
    {
        var isAdministrator = user.Roles.Any(role => role.Database == MongoRoleCatalog.AdminDatabase && role.Name == "root");
        if (isAdministrator)
        {
            _clusterRoles.Set(ClusterRole.Administrator, true);
        }

        foreach (var role in user.Roles)
        {
            if (MongoRoleCatalog.TryGetClusterRole(role, out var clusterRole) && !isAdministrator)
            {
                _clusterRoles.Set(clusterRole, true);
            }
            else if (MongoRoleCatalog.TryGetDatabaseAccess(role, out var access) && !isAdministrator)
            {
                _accesses.Add(access);
            }
            else if (!MongoRoleCatalog.IsManaged(role))
            {
                AddAdditional(role);
            }
        }

        RefreshAdditional();
    }

    private readonly List<MongoRole> _additional = new();
    private void AddAdditional(MongoRole role) => _additional.Add(role);
    private void RefreshAdditional()
    {
        if (!_isEdit) return;
        if (_additional.Count == 0)
        {
            _additionalLabel.Visible = false;
            _additionalList.Visible = false;
            return;
        }
        _additionalLabel.Visible = true;
        _additionalList.Visible = true;
        _additionalList.SetSource(new ObservableCollection<string>(_additional.Select(r => $"{r.Name}@{r.Database}").ToList()));
    }

    private void OnAdministratorChanged(bool isAdministrator)
    {
        if (isAdministrator)
        {
            _accesses.Clear();
            RefreshAccessList();
        }

        _accessList.Enabled = !isAdministrator;
        _addAccess.Enabled = !isAdministrator;
        _removeAccess.Enabled = !isAdministrator;
    }

    private void AddAccess()
    {
        using var dialog = new DatabaseAccessDialog(_localizer, _application, _connection.Databases.Select(db => db.Name).ToList());
        _application.Run(dialog);
        if (dialog.SelectedAccess is { } access)
        {
            _accesses.RemoveAll(a => a.Database.Value == access.Database.Value);
            _accesses.Add(access);
            RefreshAccessList();
        }
    }

    private void RemoveAccess()
    {
        if (_accessList.SelectedItem is int idx && idx >= 0 && idx < _accesses.Count)
        {
            _accesses.RemoveAt(idx);
            RefreshAccessList();
        }
    }

    private void RefreshAccessList()
    {
        var items = _accesses.Select(a => $"{a.Database.Value}: {a.Level switch { DatabaseAccessLevel.Read => "r", DatabaseAccessLevel.ReadWrite => "rw", _ => "dbOwner" }}").ToList();
        _accessList.SetSource(new ObservableCollection<string>(items));
    }

    private async Task SaveAsync()
    {
        _error.Text = string.Empty;
        try
        {
            var username = (_username.Text ?? string.Empty).Trim();
            var password = _password.Text ?? string.Empty;
            var clusterRoles = _clusterRoles.Selected();
            var access = new UserAccessSelection(_accesses, clusterRoles);

            if (string.IsNullOrWhiteSpace(username))
            {
                _error.Text = _localizer.Get("Users.UsernameRequired");
                return;
            }

            if (!_isEdit && _accesses.Count == 0 && clusterRoles.Count == 0)
            {
                _error.Text = _localizer.Get("Users.Access.Required");
                return;
            }

            if (_isEdit && _existing is not null)
            {
                PasswordChanged = password.Length > 0;
                await _users.UpdateAsync(_connection.Target, _scope, _existing, password, access, CancellationToken.None);
                Saved = true;
                _application.RequestStop(this);
            }
            else
            {
                if (string.IsNullOrEmpty(password))
                {
                    _error.Text = _localizer.Get("Users.PasswordRequired");
                    return;
                }
                CreatedPassword = password;
                Username = username;
                await _users.CreateAsync(_connection.Target, _scope, username, password, access, CancellationToken.None);
                Saved = true;
                _application.RequestStop(this);
            }
        }
        catch (Exception ex)
        {
            _error.Text = _localizer.GetErrorMessage(ex as MongoTui.Domain.Exceptions.DomainException ?? new MongoTui.Domain.Exceptions.DomainException(ex.Message));
        }
    }
}
