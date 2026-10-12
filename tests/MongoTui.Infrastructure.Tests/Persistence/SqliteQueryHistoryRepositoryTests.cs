using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Persistence.Sqlite;

namespace MongoTui.Infrastructure.Tests.Persistence;

public sealed class SqliteQueryHistoryRepositoryTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"mongotui-{Guid.NewGuid():N}.db");

    private readonly SqliteQueryHistoryRepository _sut;

    public SqliteQueryHistoryRepositoryTests()
    {
        var database = new SqliteDatabase(_databasePath);
        database.Migrate();
        _sut = new SqliteQueryHistoryRepository(database);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task Should_ReturnNewestFirst_When_Multiple()
    {
        var connectionId = ConnectionId.New();
        var baseTime = DateTimeOffset.UtcNow;
        await _sut.AddAsync(NewEntry(connectionId, "old", baseTime), CancellationToken.None);
        await _sut.AddAsync(NewEntry(connectionId, "new", baseTime.AddMinutes(1)), CancellationToken.None);

        var recent = await _sut.GetRecentAsync(connectionId, 10, CancellationToken.None);

        recent.Should().HaveCount(2);
        recent[0].Script.Should().Be("new");
    }

    [Fact]
    public async Task Should_RespectLimit_When_LimitGiven()
    {
        var connectionId = ConnectionId.New();
        await _sut.AddAsync(NewEntry(connectionId, "a", DateTimeOffset.UtcNow), CancellationToken.None);
        await _sut.AddAsync(NewEntry(connectionId, "b", DateTimeOffset.UtcNow.AddSeconds(1)), CancellationToken.None);

        var recent = await _sut.GetRecentAsync(connectionId, 1, CancellationToken.None);

        recent.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_IsolateByConnection_When_MultipleConnections()
    {
        var first = ConnectionId.New();
        var second = ConnectionId.New();
        await _sut.AddAsync(NewEntry(first, "a", DateTimeOffset.UtcNow), CancellationToken.None);
        await _sut.AddAsync(NewEntry(second, "b", DateTimeOffset.UtcNow), CancellationToken.None);

        var recent = await _sut.GetRecentAsync(first, 10, CancellationToken.None);

        recent.Should().ContainSingle();
        recent[0].Script.Should().Be("a");
    }

    [Fact]
    public async Task Should_ClearHistory_When_Called()
    {
        var connectionId = ConnectionId.New();
        await _sut.AddAsync(NewEntry(connectionId, "a", DateTimeOffset.UtcNow), CancellationToken.None);

        await _sut.ClearAsync(connectionId, CancellationToken.None);
        var recent = await _sut.GetRecentAsync(connectionId, 10, CancellationToken.None);

        recent.Should().BeEmpty();
    }

    private static QueryHistoryEntry NewEntry(ConnectionId connectionId, string script, DateTimeOffset ranAt) =>
        new()
        {
            ConnectionId = connectionId,
            Database = "app",
            Collection = "users",
            Script = script,
            RanAt = ranAt,
            Success = true,
            ElapsedMs = 12,
        };
}
