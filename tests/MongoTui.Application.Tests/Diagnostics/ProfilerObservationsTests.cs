using AwesomeAssertions;
using MongoTui.Application.Diagnostics;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class ProfilerObservationsTests
{
    [Fact]
    public void Should_ApplyAllFilters_WhenReadingProfilerEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new ProfilerEntry { Timestamp = now, Database = "shop", Collection = "orders",
            Operation = QueryOperationKind.Find, Duration = TimeSpan.FromMilliseconds(200), DocumentsExamined = 300,
            KeysExamined = 10, ReturnedCount = 5, ErrorCode = 13, PlanSummary = "IXSCAN" };
        var entries = new[] { entry, entry with { Database = "other" }, entry with { Collection = "other" },
            entry with { Operation = QueryOperationKind.Aggregate }, entry with { Timestamp = now.AddHours(-2) },
            entry with { Duration = TimeSpan.FromMilliseconds(100) } };
        var id = ConnectionId.New();
        var filter = new QueryMetricsFilter { Database = "shop", Collection = "orders", Operation = QueryOperationKind.Find,
            Since = now.AddHours(-1), MinimumDuration = TimeSpan.FromMilliseconds(50), SlowOnly = true };

        var rows = ProfilerObservations.Select(entries, id, filter, 100);

        var row = rows.Should().ContainSingle().Subject;
        row.ConnectionId.Should().Be(id); row.Source.Should().Be(QuerySource.ServerProfiler);
        row.IsSlow.Should().BeTrue(); row.Success.Should().BeFalse(); row.ErrorCode.Should().Be(13);
        row.DocumentsExamined.Should().Be(300); row.KeysExamined.Should().Be(10); row.ReturnedCount.Should().Be(5);
    }

    [Fact]
    public void Should_ExcludeProfilerRows_WhenClientSourceIsSelected()
    {
        var entry = new ProfilerEntry { Timestamp = DateTimeOffset.UtcNow, Operation = QueryOperationKind.Find, Duration = TimeSpan.FromSeconds(1) };

        var rows = ProfilerObservations.Select([entry], ConnectionId.New(), new() { Source = QuerySource.Driver }, 100);

        rows.Should().BeEmpty();
    }
}
