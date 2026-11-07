using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Diagnostics;

internal sealed class MetricsFixture
{
    public MetricsClock Clock { get; } = new();
    public ISettingsStore Preferences { get; } = A.Fake<ISettingsStore>();
    public DiagnosticsSettingsService Settings { get; }
    public QueryMetricsStore Store { get; }
    public QueryMetricsRecorder Recorder { get; }
    public ConnectionId Connection { get; } = ConnectionId.New();

    public MetricsFixture()
    {
        Settings = new DiagnosticsSettingsService(Preferences);
        Store = new QueryMetricsStore(Clock, Settings);
        Recorder = new QueryMetricsRecorder(Store, Settings,
            new SlowQueryLogger(NullLogger<SlowQueryLogger>.Instance), Clock);
    }

    public QueryObservation Observation(double milliseconds = 1) => new()
    {
        ProcessId = Guid.NewGuid(), ConnectionId = Connection, StartedAt = Clock.GetUtcNow(),
        Duration = TimeSpan.FromMilliseconds(milliseconds), Operation = QueryOperationKind.Find,
        Source = QuerySource.Driver, Success = true, Database = "shop", Collection = "orders",
    };
}

internal sealed class MetricsClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private long _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override DateTimeOffset GetUtcNow() => _now;
    public override long GetTimestamp() => _timestamp;
    public void Advance(TimeSpan duration) { _now += duration; _timestamp += duration.Ticks; }
}
