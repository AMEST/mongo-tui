using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Users;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Cluster role checkboxes with administrator exclusivity.</summary>
public sealed class ClusterRoleCheckboxes
{
    private static readonly (string Key, ClusterRole Role)[] Items =
    [
        ("Users.ClusterRoles.ReadAnyDatabase", ClusterRole.ReadAnyDatabase),
        ("Users.ClusterRoles.ReadWriteAnyDatabase", ClusterRole.ReadWriteAnyDatabase),
        ("Users.ClusterRoles.ClusterAdmin", ClusterRole.ClusterAdmin),
        ("Users.ClusterRoles.ClusterManager", ClusterRole.ClusterManager),
        ("Users.ClusterRoles.ClusterMonitor", ClusterRole.ClusterMonitor),
        ("Users.ClusterRoles.Administrator", ClusterRole.Administrator),
    ];

    private readonly Dictionary<CheckBox, ClusterRole> _roles = new();

    public ClusterRoleCheckboxes(ILocalizer localizer, int x, int y)
    {
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
    }

    public event Action<bool>? AdministratorChanged;

    public List<CheckBox> Checks { get; } = [];

    public void Set(ClusterRole role, bool value)
    {
        foreach (var (check, checkRole) in _roles)
        {
            if (checkRole == role)
            {
                check.Value = value ? CheckState.Checked : CheckState.UnChecked;
            }
        }
    }

    public HashSet<ClusterRole> Selected()
    {
        var set = new HashSet<ClusterRole>();
        foreach (var (check, role) in _roles)
        {
            if (check.Value == CheckState.Checked)
            {
                set.Add(role);
            }
        }

        return set;
    }

    private void OnChanged(CheckBox check)
    {
        if (_roles[check] != ClusterRole.Administrator)
        {
            return;
        }

        var isAdministrator = check.Value == CheckState.Checked;
        foreach (var other in Checks.Where(other => other != check))
        {
            if (isAdministrator)
            {
                other.Value = CheckState.UnChecked;
                other.Enabled = false;
            }
            else
            {
                other.Enabled = true;
            }
        }

        AdministratorChanged?.Invoke(isAdministrator);
    }
}
