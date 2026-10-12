using AwesomeAssertions;
using MongoTui.Infrastructure.Persistence.Sqlite;

namespace MongoTui.Infrastructure.Tests.Persistence;

public sealed class SqliteSettingsStoreTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"mongotui-{Guid.NewGuid():N}.db");

    private readonly SqliteSettingsStore _sut;

    public SqliteSettingsStoreTests()
    {
        var database = new SqliteDatabase(_databasePath);
        database.Migrate();
        _sut = new SqliteSettingsStore(database);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task Should_ReturnValue_When_KeyExists()
    {
        await _sut.SetAsync("language", "en", CancellationToken.None);

        var value = await _sut.GetAsync("language", CancellationToken.None);

        value.Should().Be("en");
    }

    [Fact]
    public async Task Should_ReturnNull_When_KeyMissing()
    {
        var value = await _sut.GetAsync("missing", CancellationToken.None);

        value.Should().BeNull();
    }

    [Fact]
    public async Task Should_OverwriteValue_When_KeyExists()
    {
        await _sut.SetAsync("language", "en", CancellationToken.None);
        await _sut.SetAsync("language", "ru", CancellationToken.None);

        var value = await _sut.GetAsync("language", CancellationToken.None);

        value.Should().Be("ru");
    }

    [Fact]
    public async Task Should_ReturnAllEntries_When_MultipleKeys()
    {
        await _sut.SetAsync("a", "1", CancellationToken.None);
        await _sut.SetAsync("b", "2", CancellationToken.None);

        var all = await _sut.GetAllAsync(CancellationToken.None);

        all.Should().HaveCount(2);
        all["a"].Should().Be("1");
        all["b"].Should().Be("2");
    }
}
