namespace MongoTui.Domain.Diagnostics;

public sealed record QueryMetricGroup
{
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required QueryOperationKind Operation { get; init; }
    public required QuerySource Source { get; init; }
    public required long Count { get; init; }
    public required long Failed { get; init; }
    public required long Slow { get; init; }
    public required double AverageDurationMs { get; init; }
    public required double P95Ms { get; init; }
}
