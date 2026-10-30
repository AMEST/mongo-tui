using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Users;

/// <summary>Maps managed MongoDB role names to the access model used by the UI.</summary>
public static class MongoRoleCatalog
{
    public const string AdminDatabase = MongoSystemNames.AdminDatabase;

    public static bool IsManaged(MongoRole role)
    {
        if (role.Database == AdminDatabase && role.Name is
            MongoRoleNames.Root or MongoRoleNames.ReadAnyDatabase or MongoRoleNames.ReadWriteAnyDatabase or MongoRoleNames.ClusterAdmin or MongoRoleNames.ClusterManager or MongoRoleNames.ClusterMonitor)
        {
            return true;
        }

        return role.Name is MongoRoleNames.Read or MongoRoleNames.ReadWrite or MongoRoleNames.DbOwner;
    }

    public static bool TryGetClusterRole(MongoRole role, out ClusterRole clusterRole)
    {
        clusterRole = role.Name switch
        {
            MongoRoleNames.ReadAnyDatabase => ClusterRole.ReadAnyDatabase,
            MongoRoleNames.ReadWriteAnyDatabase => ClusterRole.ReadWriteAnyDatabase,
            MongoRoleNames.ClusterAdmin => ClusterRole.ClusterAdmin,
            MongoRoleNames.ClusterManager => ClusterRole.ClusterManager,
            MongoRoleNames.ClusterMonitor => ClusterRole.ClusterMonitor,
            MongoRoleNames.Root => ClusterRole.Administrator,
            _ => default,
        };

        return role.Database == AdminDatabase && role.Name is
            MongoRoleNames.Root or MongoRoleNames.ReadAnyDatabase or MongoRoleNames.ReadWriteAnyDatabase or MongoRoleNames.ClusterAdmin or MongoRoleNames.ClusterManager or MongoRoleNames.ClusterMonitor;
    }

    public static bool TryGetDatabaseAccess(MongoRole role, out DatabaseAccess access)
    {
        access = default!;
        var level = role.Name switch
        {
            MongoRoleNames.Read => DatabaseAccessLevel.Read,
            MongoRoleNames.ReadWrite => DatabaseAccessLevel.ReadWrite,
            MongoRoleNames.DbOwner => DatabaseAccessLevel.DbOwner,
            _ => (DatabaseAccessLevel?)null,
        };

        if (level is null)
        {
            return false;
        }

        access = new DatabaseAccess(DatabaseName.Create(role.Database), level.Value);
        return true;
    }
}
