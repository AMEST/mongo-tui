using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Users;

/// <summary>Maps managed MongoDB role names to the access model used by the UI.</summary>
public static class MongoRoleCatalog
{
    public const string AdminDatabase = "admin";

    public static bool IsManaged(MongoRole role)
    {
        if (role.Database == AdminDatabase && role.Name is
            "root" or "readAnyDatabase" or "readWriteAnyDatabase" or "clusterAdmin" or "clusterManager" or "clusterMonitor")
        {
            return true;
        }

        return role.Name is "read" or "readWrite" or "dbOwner";
    }

    public static bool TryGetClusterRole(MongoRole role, out ClusterRole clusterRole)
    {
        clusterRole = role.Name switch
        {
            "readAnyDatabase" => ClusterRole.ReadAnyDatabase,
            "readWriteAnyDatabase" => ClusterRole.ReadWriteAnyDatabase,
            "clusterAdmin" => ClusterRole.ClusterAdmin,
            "clusterManager" => ClusterRole.ClusterManager,
            "clusterMonitor" => ClusterRole.ClusterMonitor,
            "root" => ClusterRole.Administrator,
            _ => default,
        };

        return role.Database == AdminDatabase && role.Name is
            "root" or "readAnyDatabase" or "readWriteAnyDatabase" or "clusterAdmin" or "clusterManager" or "clusterMonitor";
    }

    public static bool TryGetDatabaseAccess(MongoRole role, out DatabaseAccess access)
    {
        access = default!;
        var level = role.Name switch
        {
            "read" => DatabaseAccessLevel.Read,
            "readWrite" => DatabaseAccessLevel.ReadWrite,
            "dbOwner" => DatabaseAccessLevel.DbOwner,
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
