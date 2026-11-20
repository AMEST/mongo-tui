using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Domain.Diagnostics;
using MongoTui.Application.Services;

namespace MongoTui.Application.Diagnostics;

/// <summary>Thread-safe bounded memory window. Returned snapshots outlive Clear and eviction.</summary>
public sealed class QueryMetricsStore(TimeProvider timeProvider, DiagnosticsSettingsService settings, QueryMetricsQueue? queue = null) : IQueryMetricsStore
{
    public const int Capacity = 10_000;
    private readonly Lock _gate = new();
    private readonly Queue<QueryObservation> _observations = new();

    public void Record(QueryObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(observation));
        lock (_gate)
        {
            if (_observations.Count == Capacity) _observations.Dequeue();
            _observations.Enqueue(observation);
            if (settings.Current.PersistMetrics) queue?.Enqueue(observation);
        }
    }

    public QueryMetricsSummary Summarize(QueryMetricsFilter filter) => QueryMetricsMath.Summarize(Snapshot(filter));

    public IReadOnlyList<QueryObservation> GetRecent(QueryMetricsFilter filter, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        return Snapshot(filter).OrderByDescending(item => item.StartedAt).Take(limit).ToArray();
    }

    public IReadOnlyList<QueryMetricGroup> Group(QueryMetricsFilter filter, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        return Snapshot(filter).GroupBy(item => (item.Database, item.Collection, item.Operation, item.Source))
            .Select(group => new QueryMetricGroup
            {
                Database = group.Key.Database, Collection = group.Key.Collection,
                Operation = group.Key.Operation, Source = group.Key.Source,
                Count = group.LongCount(), Failed = group.LongCount(item => !item.Success),
                Slow = group.LongCount(item => item.IsSlow),
                AverageDurationMs = group.Average(item => item.Duration.TotalMilliseconds),
                P95Ms = QueryMetricsMath.Percentile(group.Select(item => item.Duration.TotalMilliseconds)
                    .Order().ToArray(), 0.95),
            }).OrderByDescending(group => group.Count).ThenBy(group => group.Database, StringComparer.Ordinal)
            .ThenBy(group => group.Collection, StringComparer.Ordinal).ThenBy(group => group.Operation)
            .ThenBy(group => group.Source).Take(limit).ToArray();
    }

    public void Clear()
    {
        lock (_gate) _observations.Clear();
    }

    private QueryObservation[] Snapshot(QueryMetricsFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var earliest = timeProvider.GetUtcNow().AddMinutes(-settings.Current.MemoryRetentionMinutes);
        lock (_gate)
            return _observations.Where(item => item.StartedAt >= earliest && Matches(item, filter)).ToArray();
    }

    private static bool Matches(QueryObservation item, QueryMetricsFilter filter) =>
        (filter.ConnectionId is null || item.ConnectionId == filter.ConnectionId)
        && (filter.Database is null || item.Database == filter.Database)
        && (filter.Collection is null || item.Collection == filter.Collection)
        && (filter.Operation is null || item.Operation == filter.Operation)
        && (filter.Source is null || item.Source == filter.Source)
        && (filter.Since is null || item.StartedAt >= filter.Since)
        && (filter.MinimumDuration is null || item.Duration >= filter.MinimumDuration)
        && (!filter.SlowOnly || item.IsSlow);
}
