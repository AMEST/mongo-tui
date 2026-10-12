using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Infrastructure.Security;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class FallbackKeyProviderTests
{
    private static readonly Argon2Parameters FastParameters = new(MemorySizeKb: 1024, Iterations: 1, DegreesOfParallelism: 1);

    [Fact]
    public async Task Should_UseKeyring_When_Available()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        A.CallTo(() => keyring.GetAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[32]);
        var sut = new FallbackKeyProvider(
            new KeyringKeyProvider(keyring),
            new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters));

        var kek = await sut.GetOrCreateKekAsync(CancellationToken.None);

        kek.Should().HaveCount(32);
        A.CallTo(() => keyring.GetAsync(A<string>._, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task Should_UseMasterPassword_When_KeyringUnavailable()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(false);
        var sut = new FallbackKeyProvider(
            new KeyringKeyProvider(keyring),
            new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters));

        var kek = await sut.UnlockWithMasterPasswordAsync("pw", CancellationToken.None);

        sut.IsKeyringAvailable.Should().BeFalse();
        kek.Should().HaveCount(32);
    }

    [Fact]
    public async Task ShouldThrow_NotSupported_When_KeyringUnavailableAndNoPassword()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(false);
        var sut = new FallbackKeyProvider(
            new KeyringKeyProvider(keyring),
            new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters));

        var act = async () => await sut.GetOrCreateKekAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
