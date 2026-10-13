namespace MongoTui.Domain.Databases;

/// <summary>Read-only statistics of a database.</summary>
public sealed record DatabaseStats
{
    public required string Name { get; init; }

    public int CollectionCount { get; init; }

    public long ObjectCount { get; init; }

    public long? DataSizeBytes { get; init; }

    public long? StorageSizeBytes { get; init; }

    public int IndexCount { get; init; }
}
