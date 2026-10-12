using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Collections;

/// <summary>Reference to a collection within a database.</summary>
public sealed record CollectionRef(DatabaseName Database, CollectionName Collection)
{
    public override string ToString() => $"{Database}.{Collection}";
}
