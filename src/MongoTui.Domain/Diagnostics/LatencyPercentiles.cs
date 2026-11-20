namespace MongoTui.Domain.Diagnostics;

public sealed record LatencyPercentiles(
    double P50Ms, double P95Ms, double P99Ms, double MaxMs);

