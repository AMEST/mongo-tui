namespace MongoTui.Domain.Diagnostics;

public sealed record ExplainNode
{
    public required string Stage { get; init; }
    public string? IndexName { get; init; }
    public long? ExecutionTimeMs { get; init; }
    public long? DocumentsExamined { get; init; }
    public long? KeysExamined { get; init; }
    public long? Returned { get; init; }
    public bool? UsedDisk { get; init; }
    public long? Spills { get; init; }
    public IReadOnlyList<ExplainNode> Children { get; init; } = [];
}

