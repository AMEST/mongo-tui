using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Abstractions.Diagnostics;

/// <summary>Bounded, process-local observations. All operations are memory-only.</summary>
public interface IQueryMetricsStore
{
    void Record(QueryObservation observation);
    QueryMetricsSummary Summarize(QueryMetricsFilter filter);
    IReadOnlyList<QueryObservation> GetRecent(QueryMetricsFilter filter, int limit);
    IReadOnlyList<QueryMetricGroup> Group(QueryMetricsFilter filter, int limit);
    void Clear();
}
