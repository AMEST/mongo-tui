using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class FileKekSourceTests
{
    [Fact]
    public async Task Should_CreateAndReadKek_When_RoundTripped()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var sut = new FileKekSource(new FileKekFileStore(path));

        var created = await sut.CreateAsync(null, CancellationToken.None);
        var read = await sut.TryGetKekAsync(CancellationToken.None);

        created.Should().HaveCount(32);
        read.Should().Equal(created);
        sut.Mode.Should().Be(SecurityMode.None);
        sut.IsAvailable.Should().BeTrue();
        await sut.DeleteAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Should_ReportMissingMaterial_When_FileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var sut = new FileKekSource(new FileKekFileStore(path));

        (await sut.HasMaterialAsync(CancellationToken.None)).Should().BeFalse();
        (await sut.TryGetKekAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_FileHasInvalidLength()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var store = new FileKekFileStore(path);
        await store.WriteAsync(new byte[8], CancellationToken.None);
        var sut = new FileKekSource(store);

        var act = async () => await sut.TryGetKekAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ShouldThrow_NotSupported_When_UnlockingWithPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mongotui-kek-{Guid.NewGuid():N}.key");
        var sut = new FileKekSource(new FileKekFileStore(path));

        var act = async () => await sut.UnlockAsync("password", CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
