using MongoTui.Domain.Primitives;
using MongoTui.Application.Localization;
using MongoTui.Domain.Connections;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private bool _connectingProfile;

    private async Task ReloadProfilesAsync()
    {
        var profiles = await _connections.GetProfilesAsync(CancellationToken.None);
        foreach (var active in _tree.Connections.ToArray())
        {
            var profile = profiles.FirstOrDefault(profile => profile.Id == active.Profile.Id);
            if (profile != active.Profile) await DisconnectAsync(active);
        }
        _tree.SetProfiles(profiles);
        RebuildTree();
    }

    private async Task ConnectProfileAsync(ConnectionProfile profile)
    {
        if (_connectingProfile) return;
        _connectingProfile = true;
        try
        {
            var target = await _connections.ResolveTargetAsync(profile.Id, CancellationToken.None);
            await ConnectAsync(profile, target);
            SelectProfile(profile);
        }
        finally { _connectingProfile = false; }
    }

    private void SelectProfile(ConnectionProfile profile)
    {
        var index = _rows.FindIndex(row => row.Kind == TreeRowKind.Connection && row.Profile.Id == profile.Id);
        if (index >= 0) _treeList.SelectedItem = index;
    }

    private List<(string Title, Func<Task> Handler)> BuildConnectionActions(TreeRow row)
    {
        var actions = new List<(string Title, Func<Task> Handler)>();
        if (row.Owner is { } connection)
        {
            actions.Add((_localizer.Get(LocalizationKeys.Tree.Disconnect), () => DisconnectAsync(connection)));
            actions.Add((_localizer.Get(LocalizationKeys.Tree.CreateDatabase), () => CreateDatabaseAsync(connection)));
            actions.Add((_localizer.Get(LocalizationKeys.Tree.Refresh), () => RefreshConnectionAsync(connection)));
            actions.Add((_localizer.Get(LocalizationKeys.Tree.ManageUsersAdmin), () => OpenUserManagementAsync(connection, MongoSystemNames.AdminDatabase)));
        }
        else actions.Add((_localizer.Get(LocalizationKeys.Connection.Connect), () => ConnectProfileAsync(row.Profile)));
        actions.Add((_localizer.Get(LocalizationKeys.Connection.Edit), () => EditProfileAsync(row.Profile)));
        actions.Add((_localizer.Get(LocalizationKeys.Connection.Remove), () => DeleteProfileAsync(row.Profile)));
        return actions;
    }

    private async Task EditProfileAsync(ConnectionProfile profile)
    {
        if (Application is null) return;
        var secret = await _connections.GetSecretAsync(profile.Id, CancellationToken.None);
        using var dialog = new ConnectionFormDialog(_connections, _localizer, Application, profile, secret);
        Application.Run(dialog);
        if (dialog.Saved) await ReloadProfilesAsync();
        SelectProfile(profile);
    }

    private async Task DeleteProfileAsync(ConnectionProfile profile)
    {
        if (Application is null) return;
        using var dialog = new ConfirmDialog(_localizer, Application,
            _localizer.Get(LocalizationKeys.Connection.DeleteTitle),
            _localizer.Format(LocalizationKeys.Connection.DeleteHint, profile.Name));
        Application.Run(dialog);
        if (!dialog.Confirmed) return;
        await _connections.DeleteAsync(profile.Id, CancellationToken.None);
        await ReloadProfilesAsync();
    }
}
