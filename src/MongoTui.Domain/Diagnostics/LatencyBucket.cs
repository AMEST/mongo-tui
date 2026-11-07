namespace MongoTui.Domain.Diagnostics;

public sealed record LatencyBucket(TimeSpan UpperBound, long Count);

