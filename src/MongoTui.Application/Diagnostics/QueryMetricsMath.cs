using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

internal static class QueryMetricsMath
{
    public static QueryMetricsSummary Summarize(IReadOnlyList<QueryObservation> observations)
    {
        var durations = observations.Select(item => item.Duration.TotalMilliseconds).Order().ToArray();
        return new QueryMetricsSummary
        {
            Total = observations.Count, Failed = observations.Count(item => !item.Success),
            Slow = observations.Count(item => item.IsSlow), TimedOut = observations.Count(item => item.TimedOut),
            Cancelled = observations.Count(item => item.Cancelled),
            AverageDurationMs = durations.Length == 0 ? 0 : durations.Average(),
            Latency = new LatencyPercentiles(Percentile(durations, 0.50), Percentile(durations, 0.95),
                Percentile(durations, 0.99), durations.LastOrDefault()),
            ReturnedDocuments = observations.Sum(item => item.ReturnedCount ?? 0),
            DocumentsExamined = SumKnown(observations.Select(item => item.DocumentsExamined)),
            KeysExamined = SumKnown(observations.Select(item => item.KeysExamined)),
            Histogram = Histogram(durations),
        };
    }

    public static double Percentile(double[] sorted, double percentile) =>
        sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];

    private static long? SumKnown(IEnumerable<long?> values)
    {
        var known = values.Where(value => value.HasValue).ToArray();
        return known.Length == 0 ? null : known.Sum();
    }

    private static IReadOnlyList<LatencyBucket> Histogram(double[] sorted)
    {
        double[] boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000];
        var result = new List<LatencyBucket>();
        var position = 0;
        foreach (var boundary in boundaries)
        {
            var start = position;
            while (position < sorted.Length && sorted[position] <= boundary) position++;
            result.Add(new LatencyBucket(TimeSpan.FromMilliseconds(boundary), position - start));
        }
        result.Add(new LatencyBucket(TimeSpan.MaxValue, sorted.Length - position));
        return result;
    }
}
