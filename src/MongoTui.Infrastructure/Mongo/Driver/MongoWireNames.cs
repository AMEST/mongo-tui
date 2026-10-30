namespace MongoTui.Infrastructure.Mongo.Driver;

/// <summary>Names in driver command documents and server responses.</summary>
internal static class MongoWireNames
{
    internal static class Commands
    {
        public const string DatabaseStats = "dbStats";
        public const string UsersInfo = "usersInfo";
        public const string CreateUser = "createUser";
        public const string UpdateUser = "updateUser";
        public const string DropUser = "dropUser";
        public const string ConnectionStatus = "connectionStatus";
    }

    internal static class Fields
    {
        public const string Name = "name";
        public const string Empty = "empty";
        public const string Collections = "collections";
        public const string Objects = "objects";
        public const string DataSize = "dataSize";
        public const string StorageSize = "storageSize";
        public const string Indexes = "indexes";
        public const string Users = "users";
        public const string User = "user";
        public const string Database = "db";
        public const string Roles = "roles";
        public const string Role = "role";
        public const string Password = "pwd";
        public const string IndexKeys = "key";
        public const string Unique = "unique";
        public const string Sparse = "sparse";
        public const string ShowPrivileges = "showPrivileges";
        public const string AuthInfo = "authInfo";
        public const string AuthenticatedUserPrivileges = "authenticatedUserPrivileges";
        public const string Resource = "resource";
    }
}
