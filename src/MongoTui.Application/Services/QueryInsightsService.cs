using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Services;

public sealed class QueryInsightsService(IQueryMetricsStore memory, IQueryMetricsRepository repository,
    DiagnosticsSettingsService settings)
{
    public async Task<IReadOnlyList<QueryObservation>> ReadAsync(QueryMetricsFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Source == QuerySource.ServerProfiler) return [];
        var live = memory.GetRecent(filter, QueryMetricsStore.Capacity).Where(IsClientObservation);
        if (!settings.Current.PersistMetrics) return live.ToArray();
        var saved = await repository.QueryAsync(filter, settings.Current.MaximumPersistentRows, cancellationToken).ConfigureAwait(false);
        return live.Concat(saved.Where(IsClientObservation)).DistinctBy(item => item.ObservationId)
            .OrderByDescending(item => item.StartedAt).ToArray();
    }

    private static bool IsClientObservation(QueryObservation observation) => observation.Source is QuerySource.Driver or QuerySource.Shell;
    public QueryMetricsSummary Summarize(IReadOnlyList<QueryObservation> observations) => QueryMetricsMath.Summarize(observations);
    public IReadOnlyList<QueryMetricGroup> Group(IReadOnlyList<QueryObservation> observations) => observations
        .GroupBy(row => (row.Database, row.Collection, row.Operation, row.Source))
        .Select(group =>
        {
            var summary = QueryMetricsMath.Summarize(group.ToArray());
            return new QueryMetricGroup { Database = group.Key.Database, Collection = group.Key.Collection,
                Operation = group.Key.Operation, Source = group.Key.Source, Count = summary.Total,
                Failed = summary.Failed, Slow = summary.Slow, AverageDurationMs = summary.AverageDurationMs,
                P95Ms = summary.Latency.P95Ms };
        }).OrderByDescending(group => group.Count).ToArray();
    public void ClearMemory() => memory.Clear();
    public Task ClearHistoryAsync(MongoTui.Domain.Primitives.ConnectionId? connectionId, CancellationToken cancellationToken) =>
        repository.ClearAsync(connectionId, cancellationToken);
}
