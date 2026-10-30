using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Users;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Projects shared role selection into terminal checkboxes.</summary>
public sealed class ClusterRoleCheckboxes
{
    private static readonly (string Key, ClusterRole Role)[] Items =
    [
        (LocalizationKeys.Users.ClusterRolesReadAnyDatabase, ClusterRole.ReadAnyDatabase),
        (LocalizationKeys.Users.ClusterRolesReadWriteAnyDatabase, ClusterRole.ReadWriteAnyDatabase),
        (LocalizationKeys.Users.ClusterRolesClusterAdmin, ClusterRole.ClusterAdmin),
        (LocalizationKeys.Users.ClusterRolesClusterManager, ClusterRole.ClusterManager),
        (LocalizationKeys.Users.ClusterRolesClusterMonitor, ClusterRole.ClusterMonitor),
        (LocalizationKeys.Users.ClusterRolesAdministrator, ClusterRole.Administrator),
    ];

    private readonly Dictionary<CheckBox, ClusterRole> _roles = new();

    private readonly UserDraft _draft;
    private bool _refreshing;

    public ClusterRoleCheckboxes(ILocalizer localizer, UserDraft draft, int x, int y)
    {
        _draft = draft;
        for (var i = 0; i < Items.Length; i++)
        {
            var check = new CheckBox
            {
                Text = localizer.Get(Items[i].Key),
                X = x + (i % 2) * 40,
                Y = y + (i / 2),
            };
            _roles[check] = Items[i].Role;
            check.ValueChanged += (_, _) => OnChanged(check);
            Checks.Add(check);
        }
        Refresh();
    }

    public event Action<bool>? AdministratorChanged;

    public List<CheckBox> Checks { get; } = [];

    private void OnChanged(CheckBox check)
    {
        if (_refreshing) return;
        var wasAdministrator = _draft.IsAdministrator;
        _draft.SetClusterRole(_roles[check], check.Value == CheckState.Checked);
        Refresh();
        if (wasAdministrator != _draft.IsAdministrator) AdministratorChanged?.Invoke(_draft.IsAdministrator);
    }

    private void Refresh()
    {
        _refreshing = true;
        try
        {
            foreach (var (check, role) in _roles)
            {
                check.Value = _draft.ClusterRoles.Contains(role) ? CheckState.Checked : CheckState.UnChecked;
                check.Enabled = !_draft.IsAdministrator || role == ClusterRole.Administrator;
            }
        }
        finally { _refreshing = false; }
    }
}
