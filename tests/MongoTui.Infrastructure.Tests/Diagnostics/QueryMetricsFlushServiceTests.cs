using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Diagnostics;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class QueryMetricsFlushServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_FlushOrCountLoss_WhenRepositoryAcceptsOrRejectsBatch(bool fails)
    {
        var queue = new QueryMetricsQueue(); var repository = A.Fake<IQueryMetricsRepository>();
        var settings = new DiagnosticsSettingsService(A.Fake<ISettingsStore>());
        await settings.SaveAsync(new() { PersistMetrics = true }, CancellationToken.None);
        queue.Enqueue(new QueryObservation { ProcessId = Guid.NewGuid(), ConnectionId = ConnectionId.New(),
            StartedAt = DateTimeOffset.UtcNow, Duration = TimeSpan.Zero, Operation = QueryOperationKind.Find, Source = QuerySource.Driver, Success = true });
        if (fails) A.CallTo(() => repository.AddBatchAsync(A<IReadOnlyList<QueryObservation>>._, A<CancellationToken>._)).ThrowsAsync(new IOException());
        using var service = new QueryMetricsFlushService(queue, repository, settings, TimeProvider.System);

        await service.FlushAsync(CancellationToken.None);

        A.CallTo(() => repository.AddBatchAsync(A<IReadOnlyList<QueryObservation>>.That.Matches(batch => batch.Count == 1), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        queue.Lost.Should().Be(fails ? 1 : 0);
    }
}
