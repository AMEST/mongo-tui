namespace MongoTui.Domain.Queries;

/// <summary>Paging and timeout knobs for a query execution.</summary>
public sealed record QueryExecutionOptions
{
    public int Skip { get; init; }

    public int Limit { get; init; } = 50;

    public bool AllowDiskUse { get; init; }

    public TimeSpan? Timeout { get; init; }
}
