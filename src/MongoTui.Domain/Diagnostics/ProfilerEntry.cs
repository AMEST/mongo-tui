namespace MongoTui.Domain.Diagnostics;

public sealed record ProfilerEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required QueryOperationKind Operation { get; init; }
    public required TimeSpan Duration { get; init; }
    public long? DocumentsExamined { get; init; }
    public long? KeysExamined { get; init; }
    public long? ReturnedCount { get; init; }
    public string? PlanSummary { get; init; }
    public int? ErrorCode { get; init; }
}
