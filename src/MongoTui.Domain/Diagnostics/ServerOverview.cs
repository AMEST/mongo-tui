namespace MongoTui.Domain.Diagnostics;

public sealed record ServerOverview(string? Version, long? UptimeSeconds, long? CurrentConnections,
    long? AvailableConnections, long? ResidentMemoryBytes, string? StorageEngine,
    IReadOnlyDictionary<string, long> OpCounters, IReadOnlyList<string> ReplicaMembers,
    IReadOnlyList<CurrentOperation> CurrentOperations, IReadOnlyList<string> Warnings);

public sealed record CurrentOperation(string Id, string? Namespace, string? Operation, double Seconds, bool Waiting);
public sealed record IndexUsage(string Name, long Operations, DateTimeOffset? Since);
public sealed record CollectionOverview(string Name, long? Documents, long? DataBytes, long? StorageBytes,
    long? IndexBytes, IReadOnlyList<IndexUsage> Indexes, string? Error);
public sealed record DatabaseOverview(string Database, long? Objects, long? DataBytes, long? StorageBytes,
    IReadOnlyList<CollectionOverview> Collections);
