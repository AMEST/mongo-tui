using AwesomeAssertions;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class FileKekFileStoreTests
{
    [Fact]
    public async Task Should_WriteReadAndDeleteKek_When_RoundTripped()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var sut = new FileKekFileStore(path);
        var kek = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

        await sut.WriteAsync(kek, CancellationToken.None);
        var read = await sut.TryReadAsync(CancellationToken.None);
        await sut.DeleteAsync(CancellationToken.None);

        read.Should().Equal(kek);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnNull_When_FileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var sut = new FileKekFileStore(path);

        (await sut.TryReadAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Should_RestrictPermissions_When_WrittenOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var sut = new FileKekFileStore(path);

        await sut.WriteAsync(new byte[32], CancellationToken.None);

        var mode = File.GetUnixFileMode(path);
        mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await sut.DeleteAsync(CancellationToken.None);
    }
}
