using AwesomeAssertions;
using MongoTui.Application.Diagnostics;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class QueryMetricsStoreTests
{
    private readonly MetricsFixture _fixture = new();

    [Fact]
    public void Should_ReturnZeroSummaryAndUnknownServerCounts_WhenEmpty()
    {
        var result = _fixture.Store.Summarize(new());

        result.Total.Should().Be(0);
        result.Latency.Should().Be(new LatencyPercentiles(0, 0, 0, 0));
        result.DocumentsExamined.Should().BeNull();
        result.KeysExamined.Should().BeNull();
        result.Histogram.Should().HaveCount(13).And.OnlyContain(bucket => bucket.Count == 0);
    }

    [Fact]
    public void Should_UseNearestRankAndKeepHistogramDisjoint_WhenObservationsExist()
    {
        foreach (var duration in Enumerable.Range(1, 100)) _fixture.Store.Record(_fixture.Observation(duration));

        var result = _fixture.Store.Summarize(new());

        result.Latency.Should().Be(new LatencyPercentiles(50, 95, 99, 100));
        result.AverageDurationMs.Should().Be(50.5);
        result.Histogram.Select(bucket => bucket.Count).Should().Equal(1, 4, 5, 15, 25, 50, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void Should_CountEachBoundaryOnce_WhenLatenciesTouchEveryBucket()
    {
        double[] durations = [0, 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 10001];
        foreach (var duration in durations) _fixture.Store.Record(_fixture.Observation(duration));

        var result = _fixture.Store.Summarize(new());

        result.Histogram.Select(bucket => bucket.Count).Should().Equal(2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        result.Histogram.Last().UpperBound.Should().Be(TimeSpan.MaxValue);
    }

    [Fact]
    public void Should_KeepCancelledAndTimedOutSeparate_WhenSummarizingFailures()
    {
        _fixture.Store.Record(_fixture.Observation() with { Success = false, Cancelled = true });
        _fixture.Store.Record(_fixture.Observation() with { Success = false, TimedOut = true, IsSlow = true });
        _fixture.Store.Record(_fixture.Observation() with { ReturnedCount = 3, DocumentsExamined = 5, KeysExamined = 0 });

        var result = _fixture.Store.Summarize(new());

        result.Total.Should().Be(3);
        result.Failed.Should().Be(2);
        result.TimedOut.Should().Be(1);
        result.Cancelled.Should().Be(1);
        result.Slow.Should().Be(1);
        result.ReturnedDocuments.Should().Be(3);
        result.DocumentsExamined.Should().Be(5);
        result.KeysExamined.Should().Be(0);
    }

    [Fact]
    public void Should_ApplyAllFiltersTogether_WhenSelectingObservations()
    {
        var expected = _fixture.Observation(100) with { IsSlow = true };
        QueryObservation[] other = [expected with { ConnectionId = ConnectionId.New() },
            expected with { Database = "other" }, expected with { Collection = "other" },
            expected with { Operation = QueryOperationKind.Update }, expected with { Source = QuerySource.Shell },
            expected with { StartedAt = expected.StartedAt.AddSeconds(-1) },
            expected with { Duration = TimeSpan.Zero }, expected with { IsSlow = false }];
        foreach (var item in other.Append(expected)) _fixture.Store.Record(item);
        var filter = new QueryMetricsFilter { ConnectionId = expected.ConnectionId, Database = "shop",
            Collection = "orders", Operation = expected.Operation, Source = expected.Source,
            Since = expected.StartedAt, MinimumDuration = expected.Duration, SlowOnly = true };

        var result = _fixture.Store.GetRecent(filter, 10);

        result.Should().Equal(expected);
    }

    [Fact]
    public void Should_EvictOldestArrival_WhenCapacityIsExceeded()
    {
        for (var i = 0; i <= QueryMetricsStore.Capacity; i++) _fixture.Store.Record(_fixture.Observation(i));

        var result = _fixture.Store.GetRecent(new(), int.MaxValue);

        result.Should().HaveCount(QueryMetricsStore.Capacity);
        result.Should().NotContain(item => item.Duration == TimeSpan.Zero);
    }

    [Fact]
    public async Task Should_RespectRetentionAndSnapshots_WhenClockAdvancesAndBufferClears()
    {
        await _fixture.Settings.SaveAsync(new() { MemoryRetentionMinutes = 1 }, CancellationToken.None);
        _fixture.Store.Record(_fixture.Observation());
        var snapshot = _fixture.Store.GetRecent(new(), 1);
        _fixture.Clock.Advance(TimeSpan.FromMinutes(2));

        var result = _fixture.Store.Summarize(new());

        result.Total.Should().Be(0);
        _fixture.Store.Clear();
        snapshot.Should().HaveCount(1);
        _fixture.Store.GetRecent(new(), 1).Should().BeEmpty();
    }

    [Fact]
    public void Should_SeparateSources_WhenGroupingSameNamespace()
    {
        _fixture.Store.Record(_fixture.Observation(1));
        _fixture.Store.Record(_fixture.Observation(100) with { IsSlow = true });
        _fixture.Store.Record(_fixture.Observation(500) with { Source = QuerySource.ServerProfiler });

        var result = _fixture.Store.Group(new(), 10);

        result.Should().HaveCount(2);
        result[0].Count.Should().Be(2);
        result[0].P95Ms.Should().Be(100);
        result[0].Slow.Should().Be(1);
        result[1].Source.Should().Be(QuerySource.ServerProfiler);
    }

    [Fact]
    public async Task Should_PreserveAllObservations_WhenWritersRunConcurrently()
    {
        var observation = _fixture.Observation();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++) _fixture.Store.Record(observation with { ObservationId = Guid.NewGuid() });
        })));

        _fixture.Store.Summarize(new()).Total.Should().Be(8000);
        _fixture.Store.GetRecent(new(), 8000).Select(item => item.ObservationId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Should_SortByTimestamp_WhenCompletionOrderDiffers()
    {
        var first = _fixture.Observation();
        var second = first with { StartedAt = first.StartedAt.AddSeconds(-1) };
        _fixture.Store.Record(first);
        _fixture.Store.Record(second);

        var result = _fixture.Store.GetRecent(new(), 1);

        result.Should().Equal(first);
    }
}
