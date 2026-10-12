using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class KeyringKeyProviderTests
{
    [Fact]
    public async Task Should_GenerateAndStoreKek_When_Missing()
    {
        var keyring = A.Fake<IKeyring>();
        A.CallTo(() => keyring.IsAvailable).Returns(true);
        A.CallTo(() => keyring.GetAsync(A<string>._, A<CancellationToken>._)).Returns((byte[]?)null);
        var sut = new KeyringKeyProvider(keyring);

        var kek = await sut.GetOrCreateKekAsync(CancellationToken.None);

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
        var sut = new KeyringKeyProvider(keyring);

        var kek = await sut.GetOrCreateKekAsync(CancellationToken.None);

        kek.Should().BeSameAs(stored);
        A.CallTo(() => keyring.SetAsync(A<string>._, A<byte[]>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
