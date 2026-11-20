namespace MongoTui.Domain.Diagnostics;

public sealed record QueryMetricsSummary
{
    public required long Total { get; init; }
    public required long Failed { get; init; }
    public required long Slow { get; init; }
    public required long TimedOut { get; init; }
    public required long Cancelled { get; init; }
    public required double AverageDurationMs { get; init; }
    public required LatencyPercentiles Latency { get; init; }
    public required long ReturnedDocuments { get; init; }
    public required long? DocumentsExamined { get; init; }
    public required long? KeysExamined { get; init; }
    public required IReadOnlyList<LatencyBucket> Histogram { get; init; }
}

