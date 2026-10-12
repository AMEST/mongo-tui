namespace MongoTui.Domain.Collections;

/// <summary>Read-only statistics of a collection.</summary>
public sealed record CollectionStats
{
    public required string Name { get; init; }

    public long DocumentCount { get; init; }

    public long? SizeBytes { get; init; }

    public int IndexCount { get; init; }
}
