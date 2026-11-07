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
    internal static class Diagnostics
    {
        public const string WireDollargte = "$gte";
        public const string WireDollarindexStats = "$indexStats";
        public const string WireDollarmerge = "$merge";
        public const string WireDollarout = "$out";
        public const string WireCLUSTEREDIXSCAN = "CLUSTERED_IXSCAN";
        public const string WireCOLLSCAN = "COLLSCAN";
        public const string WireDISTINCTSCAN = "DISTINCT_SCAN";
        public const string WireIDHACK = "IDHACK";
        public const string WireIXSCAN = "IXSCAN";
        public const string WireAccesses = "accesses";
        public const string WireActive = "active";
        public const string WireAdmin = "admin";
        public const string WireAggregate = "aggregate";
        public const string WireAllPlansExecution = "allPlansExecution";
        public const string WireAvailable = "available";
        public const string WireCollStats = "collStats";
        public const string WireCommand = "command";
        public const string WireConnections = "connections";
        public const string WireCount = "count";
        public const string WireCurrent = "current";
        public const string WireCurrentOp = "currentOp";
        public const string WireCursor = "cursor";
        public const string WireDataSize = "dataSize";
        public const string WireDbStats = "dbStats";
        public const string WireDocsExamined = "docsExamined";
        public const string WireErrCode = "errCode";
        public const string WireExecutionStats = "executionStats";
        public const string WireExplain = "explain";
        public const string WireFilter = "filter";
        public const string WireFind = "find";
        public const string WireGetmore = "getmore";
        public const string WireInprog = "inprog";
        public const string WireInsert = "insert";
        public const string WireKeysExamined = "keysExamined";
        public const string WireLimit = "limit";
        public const string WireMaxTimeMS = "maxTimeMS";
        public const string WireMem = "mem";
        public const string WireMembers = "members";
        public const string WireMillis = "millis";
        public const string WireName = "name";
        public const string WireNreturned = "nreturned";
        public const string WireNs = "ns";
        public const string WireObjects = "objects";
        public const string WireOp = "op";
        public const string WireOpcounters = "opcounters";
        public const string WireOpid = "opid";
        public const string WireOps = "ops";
        public const string WirePipeline = "pipeline";
        public const string WirePlanSummary = "planSummary";
        public const string WireProfile = "profile";
        public const string WireProjection = "projection";
        public const string WireQuery = "query";
        public const string WireQueryPlanner = "queryPlanner";
        public const string WireRemove = "remove";
        public const string WireReplSetGetStatus = "replSetGetStatus";
        public const string WireResident = "resident";
        public const string WireSampleRate = "sampleRate";
        public const string WireScale = "scale";
        public const string WireSecsRunning = "secs_running";
        public const string WireServerStatus = "serverStatus";
        public const string WireSince = "since";
        public const string WireSize = "size";
        public const string WireSkip = "skip";
        public const string WireSlowms = "slowms";
        public const string WireSort = "sort";
        public const string WireStateStr = "stateStr";
        public const string WireStorageEngine = "storageEngine";
        public const string WireStorageSize = "storageSize";
        public const string WireSystemProfile = "system.profile";
        public const string WireTotalIndexSize = "totalIndexSize";
        public const string WireTs = "ts";
        public const string WireUnset = "unset";
        public const string WireUpdate = "update";
        public const string WireUptime = "uptime";
        public const string WireVerbosity = "verbosity";
        public const string WireVersion = "version";
        public const string WireWaitingForLock = "waitingForLock";
        public const string WireWas = "was";
    }
}
