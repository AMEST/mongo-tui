using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Testing;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class SlowQueryLoggerTests
{
    [Fact]
    public void Should_LogOnlySafeScalars_WhenObservationContainsPlanDetails()
    {
        var sink = new LoggingTestSink();
        using var factory = sink.CreateFactory();
        var logger = new SlowQueryLogger(factory.CreateLogger<SlowQueryLogger>());
        var observation = new QueryObservation { ConnectionId = ConnectionId.New(), ProcessId = Guid.NewGuid(),
            StartedAt = DateTimeOffset.UtcNow, Duration = TimeSpan.FromSeconds(1), Success = false,
            Database = "shop", Collection = "orders", Operation = QueryOperationKind.Find, Source = QuerySource.Driver,
            PlanSummary = "private-filter-value", IsSlow = true };

        logger.Write(observation);

        var entry = sink.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeNull();
        entry.Properties.Should().NotContainKey("PlanSummary");
        sink.Text.Should().NotContain("private-filter-value");
        entry.Properties["Operation"].Should().Be(QueryOperationKind.Find);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 1)]
    public async Task Should_LogOnlyOptedInSlowCalls_WhenRecording(bool enabled, bool slow, int expected)
    {
        var sink = new LoggingTestSink();
        using var factory = sink.CreateFactory();
        var settings = new DiagnosticsSettingsService(A.Fake<ISettingsStore>());
        await settings.SaveAsync(new() { MetricsEnabled = false, LogSlowQueries = enabled,
            SlowThresholdMs = slow ? 0 : 600000 }, CancellationToken.None);
        var recorder = new QueryMetricsRecorder(A.Fake<IQueryMetricsStore>(), settings,
            new SlowQueryLogger(factory.CreateLogger<SlowQueryLogger>()), TimeProvider.System);

        await recorder.ObserveAsync(ConnectionId.New(), "shop", "orders", QueryOperationKind.Find,
            QuerySource.Driver, QueryPurpose.User, () => Task.FromResult("private-document"));

        sink.Entries.Should().HaveCount(expected);
        sink.Text.Should().NotContain("private-document");
    }
}
