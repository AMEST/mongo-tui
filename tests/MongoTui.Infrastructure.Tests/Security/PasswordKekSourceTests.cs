using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Security;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class PasswordKekSourceTests
{
    private static readonly Argon2Parameters FastParameters = new(MemorySizeKb: 1024, Iterations: 1, DegreesOfParallelism: 1);

    [Fact]
    public async Task Should_Return32ByteKey_When_PasswordProvided()
    {
        var sut = CreateSource(out _);

        var key = await sut.CreateAsync("p@ssw0rd", CancellationToken.None);

        key.Should().HaveCount(32);
    }

    [Fact]
    public async Task Should_ReturnSameKey_When_SamePasswordAndSalt()
    {
        var sut = CreateSource(out _);
        var first = await sut.CreateAsync("p@ssw0rd", CancellationToken.None);

        var second = await sut.UnlockAsync("p@ssw0rd", CancellationToken.None);

        second.Should().Equal(first);
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_DifferentPassword()
    {
        var sut = CreateSource(out _);
        await sut.CreateAsync("first", CancellationToken.None);

        var act = async () => await sut.UnlockAsync("second", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_UnlockWithNewPassword_When_PasswordChanged()
    {
        var sut = CreateSource(out _);
        var newKey = await sut.CreateAsync("new-pass", CancellationToken.None);

        var unlocked = await sut.UnlockAsync("new-pass", CancellationToken.None);

        unlocked.Should().Equal(newKey);
    }

    [Fact]
    public async Task Should_PersistSalt_When_Created()
    {
        var sut = CreateSource(out var store);

        await sut.CreateAsync("p@ssw0rd", CancellationToken.None);
        var settings = await store.GetAllAsync(CancellationToken.None);

        settings.Should().ContainKey(PasswordKekSource.SaltSettingKey);
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_EmptyPassword()
    {
        var sut = CreateSource(out _);

        var act = async () => await sut.UnlockAsync(string.Empty, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_ReportPasswordModeAndAvailability()
    {
        var sut = CreateSource(out _);

        sut.Mode.Should().Be(SecurityMode.Password);
        sut.IsAvailable.Should().BeTrue();
        (await sut.HasMaterialAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_DeletePasswordMaterial_When_Deleted()
    {
        var sut = CreateSource(out var store);
        await sut.CreateAsync("p@ssw0rd", CancellationToken.None);

        await sut.DeleteAsync(CancellationToken.None);

        (await sut.HasMaterialAsync(CancellationToken.None)).Should().BeFalse();
        (await store.GetAllAsync(CancellationToken.None)).Should().NotContainKey(PasswordKekSource.SaltSettingKey);
        (await store.GetAllAsync(CancellationToken.None)).Should().NotContainKey(PasswordKekSource.CheckSettingKey);
    }

    private static PasswordKekSource CreateSource(out InMemorySettingsStore store)
    {
        store = new InMemorySettingsStore();
        return new PasswordKekSource(store, new AesGcmCryptoService(), FastParameters);
    }
}
