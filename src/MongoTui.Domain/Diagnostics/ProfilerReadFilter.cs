namespace MongoTui.Domain.Diagnostics;

public sealed record ProfilerReadFilter(
    DateTimeOffset? Since = null,
    TimeSpan MinimumDuration = default,
    string? Collection = null,
    QueryOperationKind? Operation = null,
    int? SlowThresholdMs = null);
