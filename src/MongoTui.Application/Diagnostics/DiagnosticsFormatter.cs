using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

public static class DiagnosticsFormatter
{
    public static string Overview(IReadOnlyList<QueryObservation> rows, Func<string, string> text)
    {
        var summary = QueryMetricsMath.Summarize(rows);
        var lines = new List<string> { $"{text("Diagnostics.Total")}: {summary.Total}   {text("Diagnostics.Failed")}: {summary.Failed}   {text("Diagnostics.Slow")}: {summary.Slow}",
            $"p50/p95/p99/max: {summary.Latency.P50Ms:0.0} / {summary.Latency.P95Ms:0.0} / {summary.Latency.P99Ms:0.0} / {summary.Latency.MaxMs:0.0} ms",
            $"{text("Diagnostics.Timeouts")}: {summary.TimedOut}  {text("Diagnostics.Cancelled")}: {summary.Cancelled}  {text("Diagnostics.Returned")}: {summary.ReturnedDocuments}" };
        var max = summary.Histogram.Max(bucket => bucket.Count);
        lines.AddRange(summary.Histogram.Select(bucket => $"{(bucket.UpperBound == TimeSpan.MaxValue ? ">10000" : bucket.UpperBound.TotalMilliseconds.ToString()),8} ms {new string('█', max == 0 ? 0 : (int)(bucket.Count * 30 / max))} {bucket.Count}"));
        lines.Add($"{text("Diagnostics.Database")}.{text("Diagnostics.Collection")}  {text("Diagnostics.Operation")}  {text("Diagnostics.Source")}  n / avg / p95");
        foreach (var group in rows.GroupBy(row => (row.Database, row.Collection, row.Operation, row.Source)).OrderByDescending(group => group.Count()))
        {
            var value = QueryMetricsMath.Summarize(group.ToArray());
            lines.Add($"{group.Key.Database}.{group.Key.Collection} {group.Key.Operation} {group.Key.Source}  {value.Total} / {value.AverageDurationMs:0.0} / {value.Latency.P95Ms:0.0}");
        }
        return string.Join('\n', lines);
    }
    public static string Observations(IEnumerable<QueryObservation> rows) => string.Join('\n', rows.Select(row =>
        $"{row.StartedAt:HH:mm:ss.fff} {row.Database}.{row.Collection} {row.Operation} {row.Source} {row.Duration.TotalMilliseconds:0.0} ms  {row.ReturnedCount}/{row.DocumentsExamined}/{row.KeysExamined}"));
    public static string Database(DatabaseOverview value, Func<string, string> text) =>
        $"{value.Database}: {value.Objects} / {value.DataBytes} / {value.StorageBytes}\n{text("Diagnostics.Size")}\n" + string.Join('\n', value.Collections.Select(collection =>
            $"{collection.Name}: {collection.Documents} / {collection.DataBytes} / {collection.StorageBytes} / {collection.IndexBytes} { (collection.Error is { } error ? text(error) : "") }\n"
            + string.Join('\n', collection.Indexes.Select(index => $"  {index.Name}: {index.Operations} ({index.Since:u}) {(index.Operations == 0 ? text("Diagnostics.Unused") : "")}"))));
    public static string Server(ServerOverview value, Func<string, string> text) =>
        $"{text("Diagnostics.Version")}: {value.Version}\n{text("Diagnostics.Uptime")}: {value.UptimeSeconds}\n{text("Diagnostics.Connections")}: {value.CurrentConnections}/{value.AvailableConnections}\n{text("Diagnostics.Memory")}: {value.ResidentMemoryBytes}\n{value.StorageEngine}\n"
        + string.Join('\n', value.OpCounters.Select(pair => $"{pair.Key}: {pair.Value}")) + "\n"
        + text("Diagnostics.Replication") + "\n" + string.Join('\n', value.ReplicaMembers) + "\n"
        + text("Diagnostics.CurrentOperations") + "\n" + string.Join('\n', value.CurrentOperations.Select(op => $"{op.Id} {op.Namespace} {op.Operation} {op.Seconds}s {op.Waiting}"))
        + "\n" + string.Join('\n', value.Warnings.Select(text));
}
