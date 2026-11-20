using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

internal static class QueryObservationMapper
{
    public static QueryObservationRow ToRow(QueryObservation item) => new()
    {
        ObservationId = item.ObservationId.ToString("D"),
        ProcessId = item.ProcessId.ToString("D"),
        ConnectionId = item.ConnectionId.ToString(),
        Database = item.Database,
        Collection = item.Collection,
        Operation = (int)item.Operation,
        Source = (int)item.Source,
        StartedAt = item.StartedAt.UtcTicks,
        DurationTicks = item.Duration.Ticks,
        Success = item.Success,
        ErrorCode = item.ErrorCode,
        ReturnedCount = item.ReturnedCount,
        AffectedCount = item.AffectedCount,
        DocumentsExamined = item.DocumentsExamined,
        KeysExamined = item.KeysExamined,
        IsSlow = item.IsSlow,
        TimedOut = item.TimedOut,
        Cancelled = item.Cancelled,
    };
    public static QueryObservation FromRow(QueryObservationRow row) => new()
    {
        ObservationId = Guid.Parse(row.ObservationId),
        ProcessId = Guid.Parse(row.ProcessId),
        ConnectionId = ConnectionId.Parse(row.ConnectionId),
        Database = row.Database,
        Collection = row.Collection,
        Operation = (QueryOperationKind)row.Operation,
        Source = (QuerySource)row.Source,
        StartedAt = new DateTimeOffset(row.StartedAt, TimeSpan.Zero),
        Duration = TimeSpan.FromTicks(row.DurationTicks),
        Success = row.Success,
        ErrorCode = row.ErrorCode,
        ReturnedCount = row.ReturnedCount,
        AffectedCount = row.AffectedCount,
        DocumentsExamined = row.DocumentsExamined,
        KeysExamined = row.KeysExamined,
        IsSlow = row.IsSlow,
        TimedOut = row.TimedOut,
        Cancelled = row.Cancelled,
    };
}
