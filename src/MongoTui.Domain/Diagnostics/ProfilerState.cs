namespace MongoTui.Domain.Diagnostics;

public sealed record ProfilerState(
    string Database,
    ProfilingLevel Level,
    ProfilerOptions Options,
    DateTimeOffset? ProfileCollectionCreatedAt,
    bool IsAvailable,
    string? ErrorCode);

