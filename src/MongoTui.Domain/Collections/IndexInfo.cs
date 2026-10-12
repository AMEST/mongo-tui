namespace MongoTui.Domain.Collections;

/// <summary>Read-only description of a collection index.</summary>
public sealed record IndexInfo
{
    public required string Name { get; init; }

    public required IReadOnlyList<IndexField> Fields { get; init; }

    public bool Unique { get; init; }

    public bool Sparse { get; init; }

    public long? SizeBytes { get; init; }
}

/// <summary>One key of an index.</summary>
public sealed record IndexField(string Path, IndexDirection Direction);
