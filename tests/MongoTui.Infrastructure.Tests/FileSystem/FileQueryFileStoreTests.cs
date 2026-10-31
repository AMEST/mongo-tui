using AwesomeAssertions;
using MongoTui.Infrastructure.FileSystem;

namespace MongoTui.Infrastructure.Tests.FileSystem;

public sealed class FileQueryFileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mongotui-{Guid.NewGuid():N}");
    private readonly FileQueryFileStore _sut = new();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Should_RoundTripContent_When_WritingAndReading()
    {
        var path = Path.Combine(_directory, "query.js");

        await _sut.WriteAsync(path, "db.users.find({})", CancellationToken.None);
        var content = await _sut.ReadAsync(path, CancellationToken.None);

        content.Should().Be("db.users.find({})");
    }

    [Fact]
    public async Task Should_CreateMissingDirectory_When_Writing()
    {
        var path = Path.Combine(_directory, "nested", "query.js");

        await _sut.WriteAsync(path, "db.users.find({})", CancellationToken.None);

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task Should_Throw_When_FileDoesNotExist()
    {
        var act = async () => await _sut.ReadAsync(Path.Combine(_directory, "missing.js"), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
    }
}
