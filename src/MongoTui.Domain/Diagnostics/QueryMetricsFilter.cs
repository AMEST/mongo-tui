using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Diagnostics;

public sealed record QueryMetricsFilter
{
    public ConnectionId? ConnectionId { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public QueryOperationKind? Operation { get; init; }
    public QuerySource? Source { get; init; }
    public DateTimeOffset? Since { get; init; }
    public TimeSpan? MinimumDuration { get; init; }
    public bool SlowOnly { get; init; }
}

