using AwesomeAssertions;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Persistence.Sqlite;

namespace MongoTui.Infrastructure.Tests.Persistence;

public sealed class SqliteQueryMetricsRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"metrics-{Guid.NewGuid():N}.db");
    private readonly SqliteQueryMetricsRepository _repository;
    public SqliteQueryMetricsRepositoryTests()
    {
        var database = new SqliteDatabase(_path); database.Migrate(); _repository = new(database);
    }
    [Fact]
    public async Task Should_DeduplicateAndFilter_WhenBatchIsRetried()
    {
        var first = Observation(DateTimeOffset.UtcNow);
        var other = first with { ObservationId = Guid.NewGuid(), ConnectionId = ConnectionId.New() };
        await _repository.AddBatchAsync([first, other, first], CancellationToken.None);

        var rows = await _repository.QueryAsync(new() { ConnectionId = first.ConnectionId, Database = "shop", Source = QuerySource.Driver }, 10, CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].Should().Be(first);
    }
    [Fact]
    public async Task Should_PruneByAgeThenCount_WhenRetentionRuns()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(0, 10).Select(i => Observation(now.AddMinutes(-i))).ToArray();
        await _repository.AddBatchAsync(rows, CancellationToken.None);

        await _repository.PruneAsync(now.AddMinutes(-5), 3, CancellationToken.None);

        (await _repository.QueryAsync(new(), 100, CancellationToken.None)).Select(row => row.ObservationId)
            .Should().Equal(rows.Take(3).Select(row => row.ObservationId));
    }
    [Fact]
    public async Task Should_ClearOnlySelectedConnection_WhenHistoryIsDeleted()
    {
        var first = Observation(DateTimeOffset.UtcNow); var second = Observation(DateTimeOffset.UtcNow);
        await _repository.AddBatchAsync([first, second], CancellationToken.None);

        await _repository.ClearAsync(first.ConnectionId, CancellationToken.None);

        (await _repository.QueryAsync(new(), 10, CancellationToken.None)).Should().Equal(second);
    }
    private static QueryObservation Observation(DateTimeOffset time) => new()
    {
        ProcessId = Guid.NewGuid(), ConnectionId = ConnectionId.New(), StartedAt = time,
        Duration = TimeSpan.FromMilliseconds(123), Database = "shop", Collection = "orders",
        Operation = QueryOperationKind.Find, Source = QuerySource.Driver, Success = true, ReturnedCount = 1,
    };
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(_path); }
}
