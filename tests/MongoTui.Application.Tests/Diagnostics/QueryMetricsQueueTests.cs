using AwesomeAssertions;
using MongoTui.Application.Diagnostics;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class QueryMetricsQueueTests
{
    [Fact]
    public void Should_DropOldestAndReportLoss_WhenQueueOverflows()
    {
        var fixture = new MetricsFixture(); var queue = new QueryMetricsQueue();
        for (var i = 0; i < 5000; i++) queue.Enqueue(fixture.Observation(i));

        var rows = queue.Drain();

        rows.Should().HaveCount(4096);
        rows[0].Duration.TotalMilliseconds.Should().Be(904);
        queue.Lost.Should().Be(904);
        queue.Drain().Should().BeEmpty();
    }
}
