using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class KeyringKekSourceTests
{
    [Fact]
    public async Task Should_GenerateAndStoreKek_When_Created()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        var sut = new KeyringKekSource(keyring);

        var kek = await sut.CreateAsync(null, CancellationToken.None);

        kek.Should().HaveCount(32);
        A.CallTo(() => keyring.SetAsync(A<string>._, A<byte[]>.That.Matches(value => value.Length == 32), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ReturnExistingKek_When_Present()
    {
        var stored = new byte[32];
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        A.CallTo(() => keyring.GetAsync(A<string>._, A<CancellationToken>._)).Returns(stored);
        var sut = new KeyringKekSource(keyring);

        var kek = await sut.TryGetKekAsync(CancellationToken.None);

        kek.Should().BeSameAs(stored);
        (await sut.HasMaterialAsync(CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnNull_When_KeyMissing()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        A.CallTo(() => keyring.GetAsync(A<string>._, A<CancellationToken>._)).Returns((byte[]?)null);
        var sut = new KeyringKekSource(keyring);

        (await sut.TryGetKekAsync(CancellationToken.None)).Should().BeNull();
        (await sut.HasMaterialAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_KeyLengthIsInvalid()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        A.CallTo(() => keyring.GetAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[8]);
        var sut = new KeyringKekSource(keyring);

        var act = async () => await sut.TryGetKekAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_ReportUnavailable_When_KeyringUnavailable()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(false);
        var sut = new KeyringKekSource(keyring);

        sut.Mode.Should().Be(SecurityMode.Keyring);
        sut.IsAvailable.Should().BeFalse();
        (await sut.HasMaterialAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_DeleteKek_When_Deleted()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        var sut = new KeyringKekSource(keyring);

        await sut.DeleteAsync(CancellationToken.None);

        A.CallTo(() => keyring.DeleteAsync(KeyringKekSource.KekKey, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_NotSupported_When_UnlockingWithPassword()
    {
        var sut = new KeyringKekSource(A.Fake<IKeyring>());

        var act = async () => await sut.UnlockAsync("password", CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
