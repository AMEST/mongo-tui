namespace MongoTui.Domain.Users;

/// <summary>Built-in role identifiers sent to and received from MongoDB.</summary>
public static class MongoRoleNames
{
    public const string Root = "root";
    public const string Read = "read";
    public const string ReadWrite = "readWrite";
    public const string DbOwner = "dbOwner";
    public const string ReadAnyDatabase = "readAnyDatabase";
    public const string ReadWriteAnyDatabase = "readWriteAnyDatabase";
    public const string ClusterAdmin = "clusterAdmin";
    public const string ClusterManager = "clusterManager";
    public const string ClusterMonitor = "clusterMonitor";
}
