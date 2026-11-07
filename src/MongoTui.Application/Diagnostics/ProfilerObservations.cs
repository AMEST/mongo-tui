using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Diagnostics;

public static class ProfilerObservations
{
    public static IReadOnlyList<QueryObservation> Select(IEnumerable<ProfilerEntry> entries, ConnectionId connectionId,
        QueryMetricsFilter filter, int slowThresholdMs) => entries
        .Where(row => (filter.Database is null || row.Database == filter.Database)
            && (filter.Collection is null || row.Collection == filter.Collection)
            && (filter.Operation is null || row.Operation == filter.Operation)
            && (filter.Source is null or QuerySource.ServerProfiler)
            && (filter.Since is null || row.Timestamp >= filter.Since)
            && (filter.MinimumDuration is null || row.Duration >= filter.MinimumDuration)
            && (!filter.SlowOnly || row.Duration.TotalMilliseconds > slowThresholdMs))
        .Select(row => new QueryObservation
        {
            ProcessId = Guid.Empty, ConnectionId = connectionId, Source = QuerySource.ServerProfiler,
            Database = row.Database, Collection = row.Collection, Operation = row.Operation,
            StartedAt = row.Timestamp, Duration = row.Duration, Success = row.ErrorCode is null,
            ErrorCode = row.ErrorCode, DocumentsExamined = row.DocumentsExamined, KeysExamined = row.KeysExamined,
            ReturnedCount = row.ReturnedCount, PlanSummary = row.PlanSummary,
            IsSlow = row.Duration.TotalMilliseconds > slowThresholdMs,
        }).OrderByDescending(row => row.StartedAt).ToArray();
}
