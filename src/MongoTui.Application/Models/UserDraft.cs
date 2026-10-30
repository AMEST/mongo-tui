using System.Collections.Frozen;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;

namespace MongoTui.Application.Models;

/// <summary>Editable user permissions shared by clients, preserving unmanaged roles.</summary>
public sealed class UserDraft
{
    private readonly List<DatabaseAccess> _databases = [];
    private FrozenSet<ClusterRole> _clusterRoles = FrozenSet<ClusterRole>.Empty;

    public UserDraft(MongoUser? existing = null)
    {
        Existing = existing is null ? null : existing with { Roles = Array.AsReadOnly(existing.Roles.ToArray()) };
        Username = existing?.Username ?? string.Empty;
        Databases = _databases.AsReadOnly();
        AdditionalRoles = Array.AsReadOnly(Existing?.Roles.Where(role => !MongoRoleCatalog.IsManaged(role)).ToArray() ?? []);
        foreach (var role in Existing?.Roles ?? [])
        {
            if (MongoRoleCatalog.TryGetClusterRole(role, out var cluster)) SetClusterRole(cluster, true);
            else if (MongoRoleCatalog.TryGetDatabaseAccess(role, out var access)) LoadDatabaseAccess(access);
        }
    }

    public MongoUser? Existing { get; }
    public string Username { get; set; }
    public string Password { get; set; } = string.Empty;
    public IReadOnlyList<DatabaseAccess> Databases { get; }
    public IReadOnlySet<ClusterRole> ClusterRoles => _clusterRoles;
    public IReadOnlyList<MongoRole> AdditionalRoles { get; }
    public bool IsAdministrator => _clusterRoles.Contains(ClusterRole.Administrator);

    public void SetClusterRole(ClusterRole role, bool enabled)
    {
        if (enabled && IsAdministrator && role != ClusterRole.Administrator) return;
        var roles = _clusterRoles.ToHashSet();
        if (enabled && role == ClusterRole.Administrator)
        {
            _databases.Clear();
            roles.Clear();
        }
        if (enabled) roles.Add(role);
        else roles.Remove(role);
        _clusterRoles = roles.ToFrozenSet();
    }

    public void SetDatabaseAccess(string database, DatabaseAccessLevel level)
    {
        if (IsAdministrator) return;
        var name = DatabaseName.Create(database.Trim());
        RemoveDatabaseAccess(name);
        _databases.Add(new DatabaseAccess(name, level));
    }

    public void RemoveDatabaseAccess(DatabaseName database) => _databases.RemoveAll(access => access.Database == database);

    private void LoadDatabaseAccess(DatabaseAccess access)
    {
        if (_databases.Any(current => current.Database == access.Database && current.Level.CompareTo(access.Level) >= 0)) return;
        SetDatabaseAccess(access.Database.Value, access.Level);
    }

    /// <summary>Captures managed access independently of subsequent form changes.</summary>
    public UserAccessSelection Snapshot() => new(Array.AsReadOnly(_databases.ToArray()), _clusterRoles);
}
