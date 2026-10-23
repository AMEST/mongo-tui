namespace MongoTui.Domain.Users;

/// <summary>Cluster-wide roles that can be granted to a user.</summary>
public enum ClusterRole
{
    ReadAnyDatabase,
    ReadWriteAnyDatabase,
    ClusterAdmin,
    ClusterManager,
    ClusterMonitor,
    Administrator,
}
