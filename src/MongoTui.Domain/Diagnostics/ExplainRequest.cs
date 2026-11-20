using MongoDB.Bson;
using MongoTui.Domain.Collections;

namespace MongoTui.Domain.Diagnostics;

public sealed record ExplainRequest
{
    public required CollectionRef Collection { get; init; }
    public required ExplainOperation Operation { get; init; }
    public BsonDocument? Filter { get; init; }
    public IReadOnlyList<BsonDocument>? Pipeline { get; init; }
    public BsonDocument? Sort { get; init; }
    public BsonDocument? Projection { get; init; }
    public int? Skip { get; init; }
    public int? Limit { get; init; }
    public ExplainVerbosity Verbosity { get; init; } = ExplainVerbosity.ExecutionStats;
    public TimeSpan? MaxTime { get; init; }
}

