using MongoTui.Domain.Collections;

namespace MongoTui.Domain.Ai;

/// <summary>A single inferred field of a collection schema.</summary>
public sealed record SchemaField(string Path, string Type);

/// <summary>Inferred schema of a collection, used to build AI prompts.</summary>
public sealed record CollectionSchema
{
    public required CollectionRef Collection { get; init; }

    /// <summary>Names of the collections in the database, for query generation context.</summary>
    public IReadOnlyList<string> Collections { get; init; } = [];

    public required IReadOnlyList<SchemaField> Fields { get; init; }
}
