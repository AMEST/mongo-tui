using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class QueryInsightsServiceTests
{
    [Fact]
    public async Task Should_ExcludeServerObservations_WhenSummarizingClientMetrics()
    {
        var fixture = new MetricsFixture();
        var repository = A.Fake<IQueryMetricsRepository>();
        var service = new QueryInsightsService(fixture.Store, repository, fixture.Settings);
        var driver = fixture.Observation();
        var shell = fixture.Observation(20) with { Source = QuerySource.Shell };
        var profiler = fixture.Observation(100) with { Source = QuerySource.ServerProfiler };
        fixture.Store.Record(driver);
        fixture.Store.Record(profiler);
        await fixture.Settings.SaveAsync(new() { PersistMetrics = true }, CancellationToken.None);
        A.CallTo(() => repository.QueryAsync(A<QueryMetricsFilter>._, A<int>._, A<CancellationToken>._))
            .Returns(new[] { profiler, driver, shell });

        var observations = await service.ReadAsync(new(), CancellationToken.None);

        observations.Should().BeEquivalentTo([driver, shell]);
        service.Summarize(observations).Total.Should().Be(2);
    }
}
