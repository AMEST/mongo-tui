using MongoDB.Bson;
using MongoTui.Domain.Collections;

namespace MongoTui.Domain.Diagnostics;

public sealed record ExplainResult
{
    public required ExplainVerbosity Verbosity { get; init; }
    public required string? Namespace { get; init; }
    public required ExplainNode Plan { get; init; }
    public required long? ExecutionTimeMs { get; init; }
    public required long? DocumentsExamined { get; init; }
    public required long? KeysExamined { get; init; }
    public required long? Returned { get; init; }
    public required IReadOnlyList<ExplainWarning> Warnings { get; init; }
    public required BsonDocument Raw { get; init; }
}
