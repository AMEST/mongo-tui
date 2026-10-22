using AwesomeAssertions;
using FakeItEasy;
using Latchkey;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class LatchkeyKeyringTests
{
    [Fact]
    public async Task Should_ReturnStoredBytes_When_KeyExists()
    {
        var store = A.Fake<ILatchkey>();
        var value = new byte[] { 1, 2, 3 };
        A.CallTo(() => store.GetBytesAsync("kek", A<CancellationToken>._))
            .Returns(ValueTask.FromResult<byte[]?>(value));
        var sut = new LatchkeyKeyring(store, isAvailable: true);

        var result = await sut.GetAsync("kek", CancellationToken.None);

        result.Should().Equal(value);
        sut.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Should_WrapBackendFailure_When_KeyringIsInaccessible()
    {
        var store = A.Fake<ILatchkey>();
        A.CallTo(() => store.GetBytesAsync("kek", A<CancellationToken>._))
            .Throws(new InvalidOperationException("locked"));
        var sut = new LatchkeyKeyring(store, isAvailable: true);

        var act = async () => await sut.GetAsync("kek", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_PreserveCancellation_When_OperationIsCancelled()
    {
        var store = A.Fake<ILatchkey>();
        A.CallTo(() => store.GetBytesAsync("kek", A<CancellationToken>._))
            .Throws(new OperationCanceledException());
        var sut = new LatchkeyKeyring(store, isAvailable: true);

        var act = async () => await sut.GetAsync("kek", CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
