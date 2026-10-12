using MongoDB.Bson;

namespace MongoTui.Domain.Queries;

/// <summary>Immutable outcome of a query execution.</summary>
public sealed record QueryResult
{
    public required QueryResultKind Kind { get; init; }

    public required IReadOnlyList<BsonDocument> Documents { get; init; }

    public TimeSpan Elapsed { get; init; }

    public long? AffectedCount { get; init; }

    public static QueryResult Empty(QueryResultKind kind) =>
        new() { Kind = kind, Documents = [] };
}
