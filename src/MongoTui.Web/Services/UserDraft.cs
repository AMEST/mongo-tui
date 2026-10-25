using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;

namespace MongoTui.Web.Services;

/// <summary>Editable managed permissions; the original user retains any additional roles.</summary>
public sealed class UserDraft
{
    public UserDraft(MongoUser? existing = null)
    {
        Existing = existing;
        Username = existing?.Username ?? string.Empty;
        foreach (var role in existing?.Roles ?? [])
        {
            if (MongoRoleCatalog.TryGetClusterRole(role, out var cluster)) ClusterRoles.Add(cluster);
            else if (MongoRoleCatalog.TryGetDatabaseAccess(role, out var access)) Databases.Add(access);
        }
        if (IsAdministrator) SetClusterRole(ClusterRole.Administrator, true);
    }

    public MongoUser? Existing { get; }
    public string Username { get; set; }
    public string Password { get; set; } = string.Empty;
    public List<DatabaseAccess> Databases { get; } = [];
    public HashSet<ClusterRole> ClusterRoles { get; } = [];
    public bool IsAdministrator => ClusterRoles.Contains(ClusterRole.Administrator);
    public IEnumerable<MongoRole> AdditionalRoles => Existing?.Roles.Where(role => !MongoRoleCatalog.IsManaged(role)) ?? [];

    public void SetClusterRole(ClusterRole role, bool enabled)
    {
        if (enabled && role == ClusterRole.Administrator)
        {
            Databases.Clear();
            ClusterRoles.Clear();
        }
        if (enabled && (!IsAdministrator || role == ClusterRole.Administrator)) ClusterRoles.Add(role);
        else ClusterRoles.Remove(role);
    }

    public void SetDatabaseAccess(string database, DatabaseAccessLevel level)
    {
        if (IsAdministrator) return;
        var name = DatabaseName.Create(database.Trim());
        Databases.RemoveAll(access => access.Database == name);
        Databases.Add(new DatabaseAccess(name, level));
    }

    public UserAccessSelection Snapshot() => new(Databases.ToArray(), ClusterRoles.ToHashSet());
}
