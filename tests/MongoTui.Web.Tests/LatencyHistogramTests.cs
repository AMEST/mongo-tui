using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Domain.Diagnostics;
using MongoTui.Web.Components.Shared;

namespace MongoTui.Web.Tests;

public sealed class LatencyHistogramTests
{
    [Fact]
    public async Task Should_RenderBucketsWithoutDivisionByZero_WhenCountsAreZero()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(services);

        await renderer.MountAsync<LatencyHistogram>(new() { [nameof(LatencyHistogram.Buckets)] = new[] { new LatencyBucket(TimeSpan.FromMilliseconds(100), 0) } });

        (await renderer.TextAsync()).Should().Contain("100 ms");
    }
}
