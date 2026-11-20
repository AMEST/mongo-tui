namespace MongoTui.Domain.Diagnostics;

/// <summary>Client collection and logging preferences; never enables a server profiler.</summary>
public sealed record ClientDiagnosticsSettings
{
    public bool PersistMetrics { get; init; }
    public int PersistentRetentionDays { get; init; } = 7;
    public int MaximumPersistentRows { get; init; } = 50_000;
    public bool MirrorServerSlowToApplicationLog { get; init; }
    public bool MetricsEnabled { get; init; } = true;
    public bool LogSlowQueries { get; init; }
    public int SlowThresholdMs { get; init; } = 100;
    public int MemoryRetentionMinutes { get; init; } = 60;
}
